$ErrorActionPreference='Stop'
Push-Location (Join-Path $PSScriptRoot '..')
try {
 dotnet build STOCK.Avalonia.sln -c Release -m:1 -nr:false -p:UseSharedCompilation=false
 if($LASTEXITCODE -ne 0){throw 'Build failed'}
 dotnet run --project Stock.Portable.Tests -c Release --no-build -- --verify-manifest docs/core-source-manifest.json
 if($LASTEXITCODE -ne 0){throw 'Core manifest failed'}
 dotnet run --project Stock.Portable.Tests -c Release --no-build
 if($LASTEXITCODE -ne 0){throw 'Core checks failed'}
 dotnet run --project Stock.Bins.Tests -c Release --no-build -- artifacts/bins-tests
 if($LASTEXITCODE -ne 0){throw 'Bin strategy checks failed'}
 dotnet run --project Stock.Desktop.Tests -c Release --no-build -- artifacts/avalonia-ui
 if($LASTEXITCODE -ne 0){throw 'Desktop checks failed'}
 dotnet run --project Stock.Desktop.Tests -c Release --no-build -- artifacts/design-reference --design
 if($LASTEXITCODE -ne 0){throw 'Visual interaction checks failed'}
} finally { Pop-Location }
