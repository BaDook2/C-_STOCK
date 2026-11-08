param([string]$Output='artifacts/live-public')
$ErrorActionPreference='Stop'
Push-Location (Join-Path $PSScriptRoot '..')
try {
 dotnet run --project Stock.Desktop.Tests -c Release -- $Output --live-public
 if($LASTEXITCODE -ne 0){throw 'Actual public-data verification failed; do not substitute fixture results'}
} finally {Pop-Location}
