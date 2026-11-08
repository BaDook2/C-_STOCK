"""Native Apple Silicon release audit. Isolated probes; never sends broker orders."""
import argparse
import hashlib
import json
import os
import platform
import plistlib
import subprocess
from pathlib import Path


def run(*args, timeout=60):
    try:
        result = subprocess.run(args, check=True, text=True, capture_output=True, timeout=timeout)
    except subprocess.CalledProcessError as error:
        print(error.stdout or "", error.stderr or "", flush=True)
        raise
    return result.stdout.strip()


def check_bundle(app, version):
    app = app.resolve()
    info = plistlib.loads((app / "Contents/Info.plist").read_bytes())
    assert info["CFBundleExecutable"] == "Stock.Avalonia", "Wrong apphost"
    assert info["CFBundlePackageType"] == "APPL"
    assert info["CFBundleIdentifier"] == "com.stock.desktop.avalonia"
    assert info["CFBundleShortVersionString"] == version
    assert info["CFBundleVersion"] == version
    assert info["LSMinimumSystemVersion"] == "14.0"
    binaries = app / "Contents/MacOS"
    apphost = binaries / info["CFBundleExecutable"]
    assert os.access(apphost, os.X_OK), "Apphost lost its execute permission"
    for name in ["libhostfxr.dylib", "libcoreclr.dylib", "libAvaloniaNative.dylib", "libSkiaSharp.dylib", "libe_sqlite3.dylib"]:
        assert (binaries / name).is_file(), f"Missing native dependency: {name}"
    runtime = json.loads((binaries / "Stock.Avalonia.runtimeconfig.json").read_text())
    assert any(item["name"] == "Microsoft.NETCore.App" for item in runtime["runtimeOptions"]["includedFrameworks"]), "Runtime is not self-contained"
    native_count = 0
    private_files = []
    for path in app.rglob("*"):
        if not path.is_file():
            continue
        if (path.suffix.lower() in [".db", ".protected", ".log", ".pfx", ".p12"]
                or path.name in ["default-broker.txt", "secrets.json", "appsettings.Local.json"]
                or any(part.lower() in ["credentials", "broker-keys", "order-guards"] for part in path.relative_to(app).parts)
                or path.name.endswith((".db-wal", ".db-shm"))
                or path.name.startswith(("Stock.Desktop.Tests", "Stock.Portable.Tests"))):
            private_files.append(str(path.relative_to(app)))
        if "Mach-O" in run("file", "-b", str(path)):
            run("lipo", str(path), "-verify_arch", "arm64")
            native_count += 1
    assert not private_files, "Private/test material must not be bundled"
    run("codesign", "--verify", "--deep", "--strict", "--verbose=2", str(app))
    entitlements = plistlib.loads(run("codesign", "-d", "--entitlements", ":-", str(apphost)).encode())
    assert entitlements.get("com.apple.security.cs.allow-jit"), "Missing .NET JIT entitlement"
    assert entitlements.get("com.apple.security.cs.disable-library-validation"), "Native libraries cannot load"
    return {"App": str(app), "NativeArm64Files": native_count, "ExecutePermission": True,
            "SignatureIntegrity": True, "SelfContained": True, "PrivateFiles": 0}


def probe(app, destination, public=False):
    destination.mkdir(parents=True, exist_ok=False)
    mode = "--verify-public" if public else "--startup-smoke"
    # LaunchServices uses the .app manifest exactly as a Finder launch does.
    run("open", "-n", "-W", str(app), "--args", mode, str(destination), timeout=220 if public else 60)
    result = json.loads((destination / "result.json").read_text())
    assert result.get("Success") and result.get("NoBrokerCalls"), f"Native probe failed: {result}"
    assert result.get("Runtime", "").startswith("osx-arm64"), "Native probe ran under the wrong architecture"
    if public:
        assert result.get("RealPublicData") and result.get("IsolatedStorage")
        assert len(result["Rows"]) == 3 and all(item["Success"] and item["Bars"] > 0 for item in result["Rows"])
    else:
        assert result.get("SyntheticOnly") and result["Candles"] > 0 and result["Quotes"] >= 2
    return result


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("output", type=Path)
    parser.add_argument("release_tag")
    parser.add_argument("name")
    args = parser.parse_args()
    assert platform.system() == "Darwin" and platform.machine() == "arm64", "An actual ARM64 Mac host is required"
    root = args.output.resolve()
    audit = root / "audit"
    audit.mkdir(exist_ok=False)
    version = args.release_tag.removeprefix("v")
    result = {"Success": False, "OS": platform.platform(), "Machine": platform.machine(),
              "Version": version, "ActualMoneyOrders": 0, "DeveloperIdSigned": False, "Notarized": False}
    try:
        app = root / "publish/Stock.app"
        result["OriginalBundle"] = check_bundle(app, version)
        result["NativeStartup"] = probe(app, audit / "native-startup")
        extracted = audit / "unzipped"
        run("ditto", "-xk", str(root / (args.name + ".zip")), str(extracted))
        extracted_app = extracted / "Stock.app"
        result["ZipBundle"] = check_bundle(extracted_app, version)
        result["ZipStartup"] = probe(extracted_app, audit / "zip-startup")
        result["PublicCharts"] = probe(extracted_app, audit / "public-charts", public=True)
        mount = audit / "mounted"
        mount.mkdir()
        run("hdiutil", "attach", "-readonly", "-nobrowse", "-mountpoint", str(mount), str(root / (args.name + ".dmg")))
        try:
            mounted_app = mount / "Stock.app"
            assert (mount / "Applications").is_symlink(), "Missing drag-to-Applications shortcut"
            assert (mount / "먼저 읽어주세요.txt").is_file(), "Missing installation/security instructions"
            result["DmgBundle"] = check_bundle(mounted_app, version)
            result["DmgStartup"] = probe(mounted_app, audit / "dmg-startup")
        finally:
            run("hdiutil", "detach", str(mount))
        result["Files"] = []
        for suffix in [".dmg", ".zip"]:
            path = root / (args.name + suffix)
            hasher = hashlib.sha256()
            with path.open("rb") as stream:
                for chunk in iter(lambda: stream.read(1024 * 1024), b""):
                    hasher.update(chunk)
            digest = hasher.hexdigest()
            checksum = (root / (args.name + suffix + ".sha256")).read_text().split()[0]
            assert digest == checksum, "Archive checksum mismatch"
            result["Files"].append({"Name": path.name, "Bytes": path.stat().st_size, "Sha256": digest})
        result["Success"] = True
    finally:
        (audit / "verification.json").write_text(json.dumps(result, indent=2, ensure_ascii=False))
    print(json.dumps(result, indent=2, ensure_ascii=False))


if __name__ == "__main__":
    main()
