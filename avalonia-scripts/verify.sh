#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
dotnet build STOCK.Avalonia.sln -c Release -m:1 -nr:false -p:UseSharedCompilation=false
dotnet run --project Stock.Portable.Tests -c Release --no-build
dotnet run --project Stock.Desktop.Tests -c Release --no-build -- artifacts/avalonia-ui
dotnet run --project Stock.Desktop.Tests -c Release --no-build -- artifacts/design-reference --design
