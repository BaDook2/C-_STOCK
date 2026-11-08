param([Parameter(Mandatory)][int]$WpfProcess,[Parameter(Mandatory)][int]$AvaloniaProcess,[Parameter(Mandatory)][string]$Helpers,[string]$Output=(Join-Path $PSScriptRoot '../artifacts/native-expander-audit'))
# Click only the known legacy parameter EXPANDER headers, never ordinary action buttons.
. $Helpers
$Output=[IO.Path]::GetFullPath($Output);New-Item -ItemType Directory -Path $Output -Force|Out-Null
$allowed=@('시점별 검증 자료 (고급)','백테스트 전용','공통 전략 조건','모형 계좌 · 체결 비용','일봉 감지 가정','성과 통계','스크리닝 후보','종목 선별 · 급락','지지 · 이동평균선','지지 · 과거 고점','지지 · 근사 매물대','지지 군집','반등 확인','분할매수','손절','익절','추적 손절','보유 · 재진입')
$records=[Collections.Generic.List[object]]::new()
function Caption($button){$name=$button.Current.Name;if(-not $name){$name=($button.FindAll([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ControlTypeProperty,[System.Windows.Automation.ControlType]::Text))|ForEach-Object {$_.Current.Name}) -join ''};return ($name -replace '\s*·\s*\d+개$','').Trim()}
function Headers($window){return @($window.FindAll([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ControlTypeProperty,[System.Windows.Automation.ControlType]::Button))|Where-Object {$_.Current.AutomationId -in @('HeaderSite','PART_ToggleButton') -and (Caption $_) -in $allowed})}
try{
 foreach($app in @(@($WpfProcess,'wpf'),@($AvaloniaProcess,'avalonia'))){
  $pv=$app[0];$label=$app[1];$w=Show-StockWindow $pv;Click-StockControl (Find-StockControl $w '전략 설정' '' ([System.Windows.Automation.ControlType]::Text))
  $pane=$w.FindAll([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ControlTypeProperty,[System.Windows.Automation.ControlType]::Pane))|Where-Object {$_.Current.BoundingRectangle.Width -gt 1000 -and $_.Current.BoundingRectangle.Height -gt 200}|Select-Object -First 1
  if(-not $pane){throw 'Parameter scroll viewport not found'};$clip=$pane.Current.BoundingRectangle
  [StockUiAudit]::SetCursorPos([int]($clip.Right-30),[int]($clip.Y+50))|Out-Null;[StockUiAudit]::Wheel(2400);Start-Sleep -Milliseconds 200
  # Earlier interactive runs may leave the viewer far below the first section.
  # Reset only scroll position; every toggle below still uses the actual mouse.
  [object]$scrollPattern=$null
  if($pane.TryGetCurrentPattern([System.Windows.Automation.ScrollPattern]::Pattern,[ref]$scrollPattern)){$scrollPattern.SetScrollPercent(-1,0);Start-Sleep -Milliseconds 150}
  $seen=[Collections.Generic.HashSet[string]]::new()
  for($page=0;$page -lt 12 -and $seen.Count -lt $allowed.Count;$page++){
   $visible=@(Headers $w|Where-Object {$r=$_.Current.BoundingRectangle;$r.Y -ge $clip.Y+2 -and $r.Bottom -le $clip.Bottom-4}|ForEach-Object {Caption $_})
   foreach($caption in $visible){
    if($seen.Contains($caption)){continue}
    $button=Headers $w|Where-Object {(Caption $_) -eq $caption}|Select-Object -First 1
    $pattern=[System.Windows.Automation.TogglePattern]$button.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern)
    foreach($offset in @(2,8,14,40)){
     if([StockUiAudit]::ForegroundPid() -ne $pv){throw 'Expander target lost foreground'}
     $before=$pattern.Current.ToggleState;$r=$button.Current.BoundingRectangle
     [StockUiAudit]::Click([int]($r.X+$offset),[int]($r.Y+$r.Height/2));Start-Sleep -Milliseconds 90
     $after=$pattern.Current.ToggleState;$changed=$before -ne $after
     $records.Add([pscustomobject]@{App=$label;Header=$caption;Offset=$offset;Before=$before.ToString();After=$after.ToString();Changed=$changed;RealMouse=$true})
     if(-not $changed){throw ('Expander click missed: '+$label+' / '+$caption+' / '+$offset)}
     $r=$button.Current.BoundingRectangle;[StockUiAudit]::Click([int]($r.X+$offset),[int]($r.Y+$r.Height/2));Start-Sleep -Milliseconds 90
    }
    $seen.Add($caption)|Out-Null
   }
   [StockUiAudit]::SetCursorPos([int]($clip.Right-30),[int]($clip.Bottom-50))|Out-Null;[StockUiAudit]::Wheel(-360);Start-Sleep -Milliseconds 180
  }
  Write-Output ($label+' actual expander headers='+$seen.Count+'/'+$allowed.Count)
  if($seen.Count -ne $allowed.Count){throw ('Unvisited headers: '+(($allowed|Where-Object {-not $seen.Contains($_)}) -join ', '))}
  Save-StockClient $pv (Join-Path $Output ($label+'-parameters.png'))|Out-Null
 }
 @{Success=$true;RealMouse=$true;AllowedHeaderNames=$allowed;Records=$records}|ConvertTo-Json -Depth 6|Set-Content -LiteralPath (Join-Path $Output 'result.json') -Encoding utf8
}finally{
 @{Records=$records}|ConvertTo-Json -Depth 6|Set-Content -LiteralPath (Join-Path $Output 'progress.json') -Encoding utf8
 foreach($pv in @($WpfProcess,$AvaloniaProcess)){$h=[StockUiAudit]::Window($pv);if($h -ne [IntPtr]::Zero){[StockUiAudit]::SetWindowPos($h,[IntPtr](-2),0,0,0,0,0x13)|Out-Null}}
}
