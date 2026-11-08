param([string]$OriginalRoot='')
$ErrorActionPreference='Stop'
$stockRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$stockManifest=Get-Content -LiteralPath (Join-Path $stockRoot 'docs/core-source-manifest.json') -Raw | ConvertFrom-Json
function Source-Hash([string]$Path,[string]$Mode){
 if($Mode -eq 'Utf8Lf'){$body=[IO.File]::ReadAllText($Path).Replace("`r`n","`n");return [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($body)))}
 return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash
}
foreach($stockFile in $stockManifest){
 $stockActual=Source-Hash (Join-Path $stockRoot $stockFile.Path) $stockFile.HashMode
 if($stockActual -ne $stockFile.Sha256){throw ('Core source changed: '+$stockFile.Path)}
 if($OriginalRoot){$stockOriginal=Source-Hash (Join-Path $OriginalRoot $stockFile.Path) $stockFile.HashMode;if($stockOriginal -ne $stockFile.Sha256){throw ('Original source differs: '+$stockFile.Path)}}
}
Write-Output ('Verified core source files: '+$stockManifest.Count)
