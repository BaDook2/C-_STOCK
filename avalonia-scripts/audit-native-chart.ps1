param([Parameter(Mandatory)][int]$WpfProcess,[Parameter(Mandatory)][int]$AvaloniaProcess,[string]$Output=(Join-Path $PSScriptRoot '../artifacts/chart-native-comparison'))
# Normal apps and actual chart gestures only. No account/key/order/save commands.
. (Join-Path $PSScriptRoot 'NativeUiAuditHelpers.ps1')
$Output=[IO.Path]::GetFullPath($Output);New-Item -ItemType Directory -Path $Output -Force|Out-Null
$records=[Collections.Generic.List[object]]::new()
function VisibleText($window,[string]$name,[switch]$Optional){$condition=[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty,$name);$target=$window.FindAll([System.Windows.Automation.TreeScope]::Descendants,$condition)|Where-Object {-not $_.Current.IsOffscreen}|Select-Object -First 1;if(-not $target -and -not $Optional){throw ('Visible period/tab control missing: '+$name)};return $target}
try{
 foreach($app in @(@($WpfProcess,'wpf'),@($AvaloniaProcess,'avalonia'))){
  $pv=$app[0];$label=$app[1];$w=Show-StockWindow $pv;Click-StockControl (VisibleText $w '종목 · 차트')
  foreach($period in @('일봉','주봉','월봉')){
   $periodControl=VisibleText $w $period -Optional
   if(-not $periodControl){$records.Add(@{App=$label;Period=$period;Skipped=$true;Reason='Not exposed in current offline mode; do not bypass account requirements'});Write-Output ($label+' '+$period+' not exposed in current mode; skipped');continue}
   # WPF keeps an existing zoom if the same DisplayBarCount is assigned again.
   # Change the range first, so every comparison begins with a truly reset viewport.
   Click-StockControl $periodControl;Click-StockControl (VisibleText $w '1개월');Click-StockControl (VisibleText $w '3개월');Start-Sleep -Milliseconds 180
   foreach($position in @(@('left',520),@('middle',800),@('right',1160))){
    $part=$position[0];$x=$position[1];[StockUiAudit]::SetCursorPos($x,580)|Out-Null;Start-Sleep -Milliseconds 100
    Save-StockClient $pv (Join-Path $Output ($label+'-'+$period+'-'+$part+'-hover.png'))|Out-Null
    [StockUiAudit]::Wheel(120);Start-Sleep -Milliseconds 120
    Save-StockClient $pv (Join-Path $Output ($label+'-'+$period+'-'+$part+'-zoom.png'))|Out-Null
    [StockUiAudit]::Wheel(-120);Start-Sleep -Milliseconds 80
    $records.Add(@{App=$label;Period=$period;Position=$part;RealHover=$true;WheelInOut=$true;ForegroundVerified=([StockUiAudit]::ForegroundPid() -eq $pv)})
   }
   [StockUiAudit]::SetCursorPos(800,580)|Out-Null;[StockUiAudit]::Wheel(120);Start-Sleep -Milliseconds 100;[StockUiAudit]::Down();foreach($x in @(820,850,880,920)){[StockUiAudit]::SetCursorPos($x,580)|Out-Null;Start-Sleep -Milliseconds 35};[StockUiAudit]::Up();Start-Sleep -Milliseconds 100
   Save-StockClient $pv (Join-Path $Output ($label+'-'+$period+'-drag-right.png'))|Out-Null
   [StockUiAudit]::SetCursorPos(920,580)|Out-Null;[StockUiAudit]::Down();foreach($x in @(890,850,820,800)){[StockUiAudit]::SetCursorPos($x,580)|Out-Null;Start-Sleep -Milliseconds 35};[StockUiAudit]::Up();Start-Sleep -Milliseconds 100
   [StockUiAudit]::SetCursorPos(800,900)|Out-Null;Start-Sleep -Milliseconds 120
   Save-StockClient $pv (Join-Path $Output ($label+'-'+$period+'-volume-hover.png'))|Out-Null
   [StockUiAudit]::SetCursorPos(1200,180)|Out-Null;Start-Sleep -Milliseconds 100
   Save-StockClient $pv (Join-Path $Output ($label+'-'+$period+'-leave.png'))|Out-Null
   Write-Output ($label+' '+$period+' left/middle/right hover, wheel in/out, drag right/left, volume hover and exit captured')
  }
 }
 @{Completed=$true;NormalApplications=$true;RealMouse=$true;Records=$records;OrdersInvoked=$false}|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $Output 'result.json') -Encoding utf8
}finally{[StockUiAudit]::Up();foreach($pv in @($WpfProcess,$AvaloniaProcess)){[StockUiAudit]::SetWindowPos([StockUiAudit]::Window($pv),[IntPtr](-2),0,0,0,0,0x13)|Out-Null}}
