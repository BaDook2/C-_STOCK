param(
 [Parameter(Mandatory)][ValidatePattern('^v\d+\.\d+\.\d+$')][string]$Tag,
 [Parameter(Mandatory)][long]$RunId,
 [Parameter(Mandatory)][ValidatePattern('^[0-9a-fA-F]{40}$')][string]$SourceCommit,
 [string]$Output=(Join-Path $PSScriptRoot '../artifacts/release-download')
)
# Read-only GitHub verification. Writes only to a new audit directory and starts
# the isolated startup probe. Never promotes an EXE or reads production account data.
$ErrorActionPreference='Stop'
$stockAuditRoot=[IO.Path]::GetFullPath($Output)
if(Test-Path -LiteralPath $stockAuditRoot){throw 'Choose a new audit directory; existing evidence is preserved.'}
$stockHeaders=@{'User-Agent'='Stock-release-verification';'Accept'='application/vnd.github+json';'Cache-Control'='no-cache'}
$stockApi='https://api.github.com/repos/BaDook2/C-_STOCK'
$stockRun=Invoke-RestMethod -Uri "$stockApi/actions/runs/$RunId" -Headers $stockHeaders
if($stockRun.status -ne 'completed' -or $stockRun.conclusion -ne 'success' -or $stockRun.head_sha -ne $SourceCommit -or $stockRun.head_branch -ne $Tag){throw 'The expected source/tag CI run has not succeeded.'}
$stockRelease=Invoke-RestMethod -Uri "$stockApi/releases/tags/$Tag" -Headers $stockHeaders
if($stockRelease.tag_name -ne $Tag -or $stockRelease.draft -or $stockRelease.prerelease){throw 'A final public release for this tag is required.'}
$stockExeAsset=@($stockRelease.assets|Where-Object name -eq 'Stock.exe')
$stockHashAsset=@($stockRelease.assets|Where-Object name -eq 'Stock.exe.sha256')
if($stockExeAsset.Count -ne 1 -or $stockHashAsset.Count -ne 1){throw 'The unique EXE and checksum assets are required.'}
foreach($stockAsset in @($stockExeAsset[0],$stockHashAsset[0])){
 $stockAssetUri=[Uri]$stockAsset.browser_download_url
 if($stockAssetUri.Scheme -ne 'https' -or $stockAssetUri.Host -ne 'github.com' -or !$stockAssetUri.AbsolutePath.StartsWith("/BaDook2/C-_STOCK/releases/download/$Tag/",[StringComparison]::Ordinal)){throw 'Unexpected release asset location.'}
}
New-Item -ItemType Directory -Path $stockAuditRoot|Out-Null
$stockExePath=Join-Path $stockAuditRoot 'Stock.exe'
$stockHashPath=Join-Path $stockAuditRoot 'Stock.exe.sha256'
Invoke-WebRequest -Uri $stockExeAsset[0].browser_download_url -OutFile $stockExePath -TimeoutSec 180
Invoke-WebRequest -Uri $stockHashAsset[0].browser_download_url -OutFile $stockHashPath -TimeoutSec 45
$stockHashLine=[IO.File]::ReadAllText($stockHashPath).Trim()
$stockHashMatch=[regex]::Match($stockHashLine,'\A([a-fA-F0-9]{64})[ \t]+\*?Stock\.exe\z')
if(!$stockHashMatch.Success){throw 'Invalid release checksum file.'}
$stockActualHash=(Get-FileHash -LiteralPath $stockExePath -Algorithm SHA256).Hash.ToLowerInvariant()
if($stockActualHash -ne $stockHashMatch.Groups[1].Value.ToLowerInvariant()){throw 'Downloaded EXE checksum mismatch; it will not be executed.'}
if($stockExeAsset[0].digest -and $stockExeAsset[0].digest -ne "sha256:$stockActualHash"){throw 'GitHub asset digest mismatch.'}
$stockExeInfo=Get-Item -LiteralPath $stockExePath
if($stockExeInfo.Length -ne $stockExeAsset[0].size){throw 'Downloaded EXE size mismatch.'}
$stockExpectedVersion=$Tag.Substring(1)+'.0'
if($stockExeInfo.VersionInfo.FileVersion -ne $stockExpectedVersion){throw 'EXE version differs from the release tag.'}
$stockSmokePath=Join-Path $stockAuditRoot 'startup-smoke'
$stockProbeProcess=Start-Process -FilePath $stockExePath -ArgumentList @('--startup-smoke',('"'+$stockSmokePath+'"')) -WindowStyle Hidden -PassThru
if(!$stockProbeProcess.WaitForExit(45000)){throw "Isolated startup probe exceeded45seconds; owned process ID $($stockProbeProcess.Id)."}
if($stockProbeProcess.ExitCode -ne 0){throw "Isolated startup probe failed: $($stockProbeProcess.ExitCode)."}
$stockSmoke=Get-Content -LiteralPath (Join-Path $stockSmokePath 'result.json') -Raw|ConvertFrom-Json
if(!$stockSmoke.Success -or !$stockSmoke.NoBrokerCalls){throw 'The public EXE startup probe failed or attempted a broker call.'}
$stockEvidence=[ordered]@{
 Tag=$Tag;SourceCommit=$SourceCommit;ActionsRun=$RunId;ActionsUrl=$stockRun.html_url;ReleaseUrl=$stockRelease.html_url
 DownloadUrl=$stockExeAsset[0].browser_download_url;Executable=$stockExePath;Bytes=$stockExeInfo.Length
 FileVersion=$stockExeInfo.VersionInfo.FileVersion;ProductVersion=$stockExeInfo.VersionInfo.ProductVersion
 Sha256=$stockActualHash;GitHubDigest=$stockExeAsset[0].digest;SmokeExitCode=$stockProbeProcess.ExitCode
 StartupSuccess=$stockSmoke.Success;NoBrokerCalls=$stockSmoke.NoBrokerCalls;VerifiedAtUtc=[DateTimeOffset]::UtcNow.ToString('O')
}
$stockEvidence|ConvertTo-Json -Depth 4|Set-Content -LiteralPath (Join-Path $stockAuditRoot 'verification.json') -Encoding utf8
$stockEvidence|ConvertTo-Json -Depth 4
