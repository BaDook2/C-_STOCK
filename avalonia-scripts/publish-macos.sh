#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
RID="${1:-}"
if [[ -z "$RID" ]]; then
  case "$(uname -m)" in arm64|aarch64) RID=osx-arm64;; x86_64) RID=osx-x64;; *) echo 'Mac CPU를 확인하고 osx-arm64 또는 osx-x64를 지정하세요.' >&2; exit 1;; esac
fi
case "$RID" in osx-arm64|osx-x64) ;; *) echo '지원 RID: osx-arm64 / osx-x64' >&2; exit 1;; esac
OUTPUT="$ROOT/artifacts/macos-$RID-$(date +%Y%m%d-%H%M%S)"
APP="$OUTPUT/Stock.Avalonia.app"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"
dotnet publish "$ROOT/Stock.Avalonia/Stock.Avalonia.csproj" -c Release -r "$RID" --self-contained true -p:UseAppHost=true -p:PublishSingleFile=false -p:PublishTrimmed=false -p:DebugType=None -o "$APP/Contents/MacOS"
cp "$ROOT/packaging/macos/Info.plist" "$APP/Contents/Info.plist"
chmod +x "$APP/Contents/MacOS/Stock.Avalonia"
printf '\n로컬 개발용 앱: %s\n' "$APP"
printf 'Developer ID 서명/공증은 포함하지 않았습니다. 실제 Mac에서 실행·차트·저장·파일 선택을 검증하세요.\n'
