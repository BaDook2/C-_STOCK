#!/usr/bin/env bash
set -euo pipefail
SCRIPTS="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(cd "$SCRIPTS/.." && pwd)"
OUTPUT="${1:?새 출력 경로가 필요합니다}"
RELEASE_TAG="${2:?릴리즈 태그가 필요합니다}"
BUILD_TAG="${3:?macos 빌드 태그가 필요합니다}"
[[ "$(uname -s)" == Darwin && "$(uname -m)" == arm64 ]] || { echo 'Apple Silicon Mac 빌드 호스트가 필요합니다.' >&2; exit 1; }
[[ "$RELEASE_TAG" =~ ^v[0-9]+\.[0-9]+\.[0-9]+$ ]] || { echo '릴리즈 태그 형식 오류' >&2; exit 1; }
[[ "$BUILD_TAG" =~ ^macos-(v[0-9]+\.[0-9]+\.[0-9]+)-r[0-9]+$ && "${BASH_REMATCH[1]}" == "$RELEASE_TAG" ]] || { echo 'macOS 빌드 태그 형식 오류' >&2; exit 1; }
[[ ! -e "$OUTPUT" ]] || { echo '기존 출력을 덮어쓰지 않습니다.' >&2; exit 1; }
mkdir -p "$OUTPUT"
bash "$SCRIPTS/publish-macos.sh" osx-arm64 "$OUTPUT/publish"
APP="$OUTPUT/publish/Stock.app"
VERSION="$(/usr/libexec/PlistBuddy -c 'Print :CFBundleShortVersionString' "$APP/Contents/Info.plist")"
[[ "$RELEASE_TAG" == "v$VERSION" ]] || { echo '앱 버전과 릴리즈 버전이 다릅니다.' >&2; exit 1; }
NAME="Stock-AppleSilicon-${BUILD_TAG#macos-}"
mkdir -p "$OUTPUT/disk"
ditto "$APP" "$OUTPUT/disk/Stock.app"
ln -s /Applications "$OUTPUT/disk/Applications"
cp "$ROOT/packaging/macos/README_KO.txt" "$OUTPUT/disk/먼저 읽어주세요.txt"
hdiutil create -volname "STOCK $VERSION" -srcfolder "$OUTPUT/disk" -format UDZO "$OUTPUT/$NAME.dmg"
ditto -c -k --sequesterRsrc --keepParent "$APP" "$OUTPUT/$NAME.zip"
(
  cd "$OUTPUT"
  shasum -a 256 "$NAME.dmg" > "$NAME.dmg.sha256"
  shasum -a 256 "$NAME.zip" > "$NAME.zip.sha256"
)
printf '%s\n' "MACOS_OUTPUT=$OUTPUT" "MACOS_APP=$APP" "MACOS_NAME=$NAME" >> "${GITHUB_ENV:-/dev/null}"
printf '생성: %s.dmg / .zip / SHA256\n' "$NAME"
