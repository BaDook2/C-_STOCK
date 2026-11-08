#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
dotnet run --project Stock.Desktop.Tests -c Release -- "${1:-artifacts/live-public}" --live-public
