$ErrorActionPreference='Stop'
$stockRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$stockOutput=Join-Path $stockRoot 'artifacts/publish/win-x64'
dotnet publish (Join-Path $stockRoot 'Stock.Avalonia/Stock.Avalonia.csproj') -c Release -r win-x64 --self-contained true -p:UseAppHost=true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishTrimmed=false -p:DebugType=None -o $stockOutput
if($LASTEXITCODE -ne 0){throw 'Windows publish failed'}
Write-Output (Join-Path $stockOutput 'Stock.Avalonia.exe')
