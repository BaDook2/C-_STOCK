param([Parameter(Mandatory)][int]$WpfProcess,[Parameter(Mandatory)][int]$AvaloniaProcess,[Parameter(Mandatory)][string]$Helpers,[string]$Output=(Join-Path $PSScriptRoot '../artifacts/native-control-audit'))
# Hover-only sweep of ordinary buttons in two already running offline apps.
# Actual mouse clicks are restricted to known tab labels and ComboBox controls.
# Never presses ordinary buttons: no reset/save/delete/connect/stop/order command.
. $Helpers
$Output=[IO.Path]::GetFullPath($Output);New-Item -ItemType Directory -Path $Output -Force|Out-Null
$records=[Collections.Generic.List[object]]::new()
$tabs=@('종목 · 차트','스크리닝','전략 설정','계좌 · 주문','테스트 기록','시장 현황','관심종목')
function In-Client($element,$bounds){$r=$element.Current.BoundingRectangle;return -not $element.Current.IsOffscreen -and $r.Width -gt 3 -and $r.Height -gt 3 -and $r.Y -ge $bounds.Y -and $r.Bottom -le $bounds.Bottom -and $r.X -ge $bounds.X -and $r.Right -le $bounds.Right}
function Capture-Rect($rect,[string]$path){$bmp=[Drawing.Bitmap]::new([Math]::Max(1,[int]$rect.Width),[Math]::Max(1,[int]$rect.Height));$g=[Drawing.Graphics]::FromImage($bmp);try{$g.CopyFromScreen([int]$rect.X,[int]$rect.Y,0,0,$bmp.Size);$bmp.Save($path,[Drawing.Imaging.ImageFormat]::Png)}finally{$g.Dispose();$bmp.Dispose()}}
try{
 foreach($app in @(@($WpfProcess,'wpf'),@($AvaloniaProcess,'avalonia'))){
  $pidValue=$app[0];$label=$app[1];$window=Show-StockWindow $pidValue
  for($tabIndex=0;$tabIndex -lt $tabs.Count;$tabIndex++){
   $tab=$tabs[$tabIndex];$window=Show-StockWindow $pidValue
   Click-StockControl (Find-StockControl $window $tab '' ([System.Windows.Automation.ControlType]::Text));Start-Sleep -Milliseconds 150
   $bounds=$window.Current.BoundingRectangle
   $all=$window.FindAll([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.Condition]::TrueCondition)
   $buttons=@($all|Where-Object {$_.Current.ControlType -eq [System.Windows.Automation.ControlType]::Button -and $_.Current.Name -ne '' -and $_.Current.AutomationId -notin @('HeaderSite','PART_ToggleButton','PART_Button','PART_PageUpButton','PART_PageDownButton') -and (In-Client $_ $bounds)})
   $index=0
   foreach($button in $buttons){
    if([StockUiAudit]::ForegroundPid() -ne $pidValue){throw 'Hover target lost foreground'}
    $r=$button.Current.BoundingRectangle;$prefix=$label+'-'+$tabIndex+'-button'+$index
    Hover-StockControl $button;Capture-Rect $r (Join-Path $Output ($prefix+'-hover.png'))
    $records.Add([pscustomobject]@{App=$label;Tab=$tab;Kind='Button';Name=$button.Current.Name;Enabled=$button.Current.IsEnabled;Hovered=$true;Clicked=$false;ForegroundVerified=$true;Image=$prefix+'-hover.png'})
    $index++
   }
   $combos=@($all|Where-Object {$_.Current.ControlType -eq [System.Windows.Automation.ControlType]::ComboBox -and $_.Current.IsEnabled -and (In-Client $_ $bounds)})
   $index=0
   foreach($combo in $combos){
    Click-StockControl $combo;$r=$combo.Current.BoundingRectangle
    [StockUiAudit]::SetCursorPos([int]($r.X+25),[int]($r.Bottom+24))|Out-Null;Start-Sleep -Milliseconds 120
    Save-StockClient $pidValue (Join-Path $Output ($label+'-'+$tabIndex+'-combo'+$index+'-option1.png'))|Out-Null
    [StockUiAudit]::SetCursorPos([int]($r.X+25),[int]($r.Bottom+62))|Out-Null;Start-Sleep -Milliseconds 100
    Save-StockClient $pidValue (Join-Path $Output ($label+'-'+$tabIndex+'-combo'+$index+'-option2.png'))|Out-Null
    [StockUiAudit]::Key(27);Start-Sleep -Milliseconds 100
    $closed=([System.Windows.Automation.ExpandCollapsePattern]$combo.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern)).Current.ExpandCollapseState -eq [System.Windows.Automation.ExpandCollapseState]::Collapsed
    if(-not $closed){throw 'Dropdown did not close after Escape'}
    $records.Add([pscustomobject]@{App=$label;Tab=$tab;Kind='ComboBox';OpenedWithMouse=$true;PointerProbeOffsets=@(24,62);DismissedWithEscape=$closed;ForegroundVerified=$true})
    $index++
   }
   Save-StockClient $pidValue (Join-Path $Output ($label+'-'+$tabIndex+'-surface.png'))|Out-Null
   @{Records=$records}|ConvertTo-Json -Depth 6|Set-Content -LiteralPath (Join-Path $Output 'progress.json') -Encoding utf8
   Write-Output ($label+' / '+$tab+' / button-hover='+$buttons.Count+' dropdowns='+$combos.Count)
  }
 }
 @{Success=$true;RealMouse=$true;NormalApplications=$true;ForegroundVerified=$true;ButtonCommandsInvoked=$false;Records=$records}|ConvertTo-Json -Depth 6|Set-Content -LiteralPath (Join-Path $Output 'result.json') -Encoding utf8
}finally{
 foreach($pidValue in @($WpfProcess,$AvaloniaProcess)){$h=[StockUiAudit]::Window($pidValue);if($h -ne [IntPtr]::Zero){[StockUiAudit]::SetWindowPos($h,[IntPtr](-2),0,0,0,0,0x13)|Out-Null}}
}
