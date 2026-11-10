#!/usr/bin/env bash
# Local Rider workflow; reuse the release bundler without creating a release.
set -euo pipefail
STOCK_PROJECT_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
STOCK_ACTION="${1:---open}"
case "$STOCK_ACTION" in --open|--build-only) ;; *) echo '사용법: build-and-run-macos.sh [--open|--build-only]' >&2; exit 2;; esac
if [[ "$(uname -s)" != Darwin ]]; then echo 'Mac에서 실행하세요.' >&2; exit 1; fi

# Finder-launched IDEs may not inherit the Terminal PATH.
if ! command -v dotnet >/dev/null 2>&1; then
  for STOCK_SDK_BIN in /opt/homebrew/bin /usr/local/share/dotnet /usr/local/bin; do
    if [[ -x "$STOCK_SDK_BIN/dotnet" ]]; then export PATH="$STOCK_SDK_BIN:$PATH"; break; fi
  done
fi
if ! command -v dotnet >/dev/null 2>&1; then echo '.NET 10 SDK를 찾지 못했습니다.' >&2; exit 1; fi

# Keep app bundles outside synced/File Provider workspaces: those can attach
# FinderInfo again after codesign and invalidate strict bundle verification.
STOCK_DESTINATION="${STOCK_DEV_OUTPUT_DIR:-/Applications}"
mkdir -p "$STOCK_DESTINATION"
STOCK_DESTINATION="$(cd "$STOCK_DESTINATION" && pwd)"
STOCK_APP="$STOCK_DESTINATION/Stock Development.app"
STOCK_LOCK="$STOCK_DESTINATION/.stock-app-build.lock"
if ! mkdir "$STOCK_LOCK" 2>/dev/null; then
  echo '다른 .app 빌드가 진행 중입니다. 먼저 진행 중인 빌드를 종료하세요.' >&2; exit 1
fi
STOCK_STAGING=""
STOCK_REPLACED=false
stock_cleanup() {
  local STOCK_EXIT=$?
  # Restore the previous app if replacement failed after it was moved aside.
  if [[ "$STOCK_REPLACED" == true && ! -e "$STOCK_APP" && -d "$STOCK_STAGING/previous.app" ]]; then
    mv "$STOCK_STAGING/previous.app" "$STOCK_APP"
  fi
  if [[ -n "$STOCK_STAGING" && -d "$STOCK_STAGING" ]]; then rm -rf "$STOCK_STAGING"; fi
  rmdir "$STOCK_LOCK" 2>/dev/null || true
  return "$STOCK_EXIT"
}
trap stock_cleanup EXIT
trap 'exit 130' INT
trap 'exit 143' TERM

stock_check_app() {
  if [[ -e "$STOCK_APP" ]]; then
    local STOCK_IDENTIFIER
    STOCK_IDENTIFIER="$(/usr/libexec/PlistBuddy -c 'Print :CFBundleIdentifier' "$STOCK_APP/Contents/Info.plist" 2>/dev/null || true)"
    if [[ "$STOCK_IDENTIFIER" != com.stock.desktop.avalonia ]]; then
      echo "$STOCK_APP: 이 프로젝트에서 만든 앱이 아니므로 교체하지 않습니다." >&2; exit 1
    fi
    if /usr/sbin/lsof -t "$STOCK_APP/Contents/MacOS/Stock.Avalonia" >/dev/null 2>&1; then
      echo 'Stock.app이 실행 중입니다. 앱에서 정상 종료한 뒤 다시 실행하세요.' >&2; exit 1
    fi
  fi
}
stock_check_app
STOCK_STAGING="$(mktemp -d /private/tmp/stock-app-build.XXXXXX)"
bash "$STOCK_PROJECT_ROOT/avalonia-scripts/publish-macos.sh" "" "$STOCK_STAGING/package"
stock_check_app
if [[ -e "$STOCK_APP" ]]; then mv "$STOCK_APP" "$STOCK_STAGING/previous.app"; STOCK_REPLACED=true; fi
mv "$STOCK_STAGING/package/Stock.app" "$STOCK_APP"
codesign --verify --deep --strict "$STOCK_APP"
printf '\n최신 개발 앱: %s\n' "$STOCK_APP"
if [[ "$STOCK_ACTION" == --open ]]; then /usr/bin/open "$STOCK_APP"; fi
