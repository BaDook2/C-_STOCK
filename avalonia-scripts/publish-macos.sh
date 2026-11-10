#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
if [[ "$(uname -s)" != Darwin ]]; then
  echo '실행 권한·서명·앱 번들은 macOS에서 생성해야 합니다.' >&2
  exit 1
fi
RID="${1:-}"
if [[ -z "$RID" ]]; then
  case "$(uname -m)" in arm64|aarch64) RID=osx-arm64;; x86_64) RID=osx-x64;; *) echo 'Mac CPU를 확인하고 osx-arm64 또는 osx-x64를 지정하세요.' >&2; exit 1;; esac
fi
case "$RID" in osx-arm64|osx-x64) ;; *) echo '지원 RID: osx-arm64 / osx-x64' >&2; exit 1;; esac
OUTPUT="${2:-$ROOT/artifacts/macos-$RID-$(date +%Y%m%d-%H%M%S)}"
if [[ -e "$OUTPUT" ]]; then echo '기존 배포 결과를 덮어쓰지 않습니다.' >&2; exit 1; fi
VERSION="$(dotnet msbuild "$ROOT/Stock.Avalonia/Stock.Avalonia.csproj" -nologo -getProperty:Version | tr -d '\r')"
if [[ ! "$VERSION" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]; then echo '프로젝트 Version을 확인하세요.' >&2; exit 1; fi
APP="$OUTPUT/Stock.app"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"
PUBLISH_ARGS=(-c Release -r "$RID" --self-contained true -p:UseAppHost=true -p:PublishSingleFile=false -p:PublishTrimmed=false -p:DebugType=None -p:UseSharedCompilation=false "-p:Version=$VERSION" -o "$APP/Contents/MacOS")
if [[ -n "${STOCK_SOURCE_REVISION:-}" ]]; then PUBLISH_ARGS+=("-p:SourceRevisionId=$STOCK_SOURCE_REVISION"); fi
dotnet publish "$ROOT/Stock.Avalonia/Stock.Avalonia.csproj" "${PUBLISH_ARGS[@]}"
cp "$ROOT/packaging/macos/Info.plist" "$APP/Contents/Info.plist"
/usr/libexec/PlistBuddy -c "Set :CFBundleVersion $VERSION" "$APP/Contents/Info.plist"
/usr/libexec/PlistBuddy -c "Set :CFBundleShortVersionString $VERSION" "$APP/Contents/Info.plist"
plutil -lint "$APP/Contents/Info.plist" "$ROOT/packaging/macos/Stock.entitlements"
chmod +x "$APP/Contents/MacOS/Stock.Avalonia"
# Finder metadata may be inherited from local source/NuGet files. These two
# attributes are forbidden by codesign; remove them from the generated app only.
xattr -dr com.apple.FinderInfo "$APP" 2>/dev/null || true
xattr -dr com.apple.ResourceFork "$APP" 2>/dev/null || true
while IFS= read -r -d '' STOCK_CODE; do
  # In the flat Avalonia bundle, managed DLLs and data are nested code too.
  # Seal every leaf first; signing the CFBundleExecutable can validate the
  # whole bundle, so leave that executable for the final bundle operation.
  if [[ "$STOCK_CODE" != "$APP/Contents/MacOS/Stock.Avalonia" ]]; then
    codesign --force --sign - "$STOCK_CODE"
  fi
done < <(find "$APP/Contents/MacOS" -type f -print0)
codesign --force --options runtime --identifier com.stock.desktop.avalonia --entitlements "$ROOT/packaging/macos/Stock.entitlements" --sign - "$APP"
codesign --verify --deep --strict --verbose=2 "$APP"
printf '\nSelf-contained macOS 앱: %s\n' "$APP"
printf 'Developer ID 서명/공증은 포함하지 않았습니다. 실제 Mac에서 실행·차트·저장·파일 선택을 검증하세요.\n'
