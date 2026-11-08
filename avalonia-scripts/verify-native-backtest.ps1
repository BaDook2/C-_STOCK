param([Parameter(Mandatory)][int]$ProcessId,[string]$Output=(Join-Path $PSScriptRoot '../artifacts/native-backtest'),[switch]$CheckOutcomeColors)
# Actual desktop mouse, public-data diagnostic backtest, chart labels and date
# popup. Never opens order controls or changes strategy parameters/credentials.
. (Join-Path $PSScriptRoot 'NativeUiAuditHelpers.ps1')
$Output=[IO.Path]::GetFullPath($Output);New-Item -ItemType Directory -Path $Output -Force|Out-Null
$result=[ordered]@{NormalApplication=$true;ActualMouse=$true;PublicData=$true;NoOrders=$true;Success=$false}
function Wait-Ui([scriptblock]$Condition,[int]$Seconds=30){$clock=[Diagnostics.Stopwatch]::StartNew();do{$value=& $Condition;if($value){return $value};Start-Sleep -Milliseconds 100}while($clock.Elapsed.TotalSeconds -lt $Seconds);throw 'Native UI condition timed out'}
function VisibleControls($Root,$Property,$Value){@($Root.FindAll([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.PropertyCondition]::new($Property,$Value))|Where-Object {-not $_.Current.IsOffscreen})}
function Chart-Hover($Window,[int]$X,[int]$Y){[StockUiAudit]::SetCursorPos($X,$Y)|Out-Null;Start-Sleep -Milliseconds 120;return (Find-StockControl $Window '' 'HoverInfo').Current.Name}
try {
 $w=Show-StockWindow $ProcessId
 Click-StockControl (Find-StockControl $w '이름으로 검색' '' ([System.Windows.Automation.ControlType]::Button))
 Wait-Ui {Find-StockControl $w '005930' '' ([System.Windows.Automation.ControlType]::Text)}|Out-Null
 Wait-Ui {(Find-StockControl $w '이름으로 검색' '' ([System.Windows.Automation.ControlType]::Button)).Current.IsEnabled}|Out-Null
 Click-StockControl (Find-StockControl $w '전략 백테스트' '' ([System.Windows.Automation.ControlType]::TabItem))
 $dates=VisibleControls $w ([System.Windows.Automation.AutomationElement]::AutomationIdProperty) 'PART_TextBox'
 if($dates.Count -ne 2){throw 'Backtest date editors were not found'}
 $values=@('2026-08-01','2026-10-30')
 for($i=0;$i -lt 2;$i++){$dates[$i].SetFocus();([System.Windows.Automation.ValuePattern]$dates[$i].GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern)).SetValue($values[$i]);[StockUiAudit]::Key(9)}
 $strategy=(VisibleControls $w ([System.Windows.Automation.AutomationElement]::ControlTypeProperty) ([System.Windows.Automation.ControlType]::ComboBox)|Where-Object {$_.Current.BoundingRectangle.X -gt 1300}|Select-Object -Last 1)
 Click-StockControl $strategy
 $preset=Wait-Ui {VisibleControls $w ([System.Windows.Automation.AutomationElement]::ControlTypeProperty) ([System.Windows.Automation.ControlType]::ListItem)|Where-Object {$_.Current.Name -match '백테스트 검증.*1일 보유'}|Select-Object -First 1}
 Click-StockControl $preset
 Click-StockControl (Find-StockControl $w '백테스트' '' ([System.Windows.Automation.ControlType]::Button))
 Wait-Ui {(Find-StockControl $w '백테스트' '' ([System.Windows.Automation.ControlType]::Button)).Current.IsEnabled}|Out-Null
 $summary=Wait-Ui {VisibleControls $w ([System.Windows.Automation.AutomationElement]::ControlTypeProperty) ([System.Windows.Automation.ControlType]::Text)|Where-Object {$_.Current.Name -match '^완결 거래 [1-9][0-9,]*회'}|Select-Object -First 1}
 $result.BacktestSummary=$summary.Current.Name
 if($CheckOutcomeColors){Wait-Ui {@(VisibleControls $w ([System.Windows.Automation.AutomationElement]::ControlTypeProperty) ([System.Windows.Automation.ControlType]::Text)|Where-Object {$_.Current.Name -match '^#\d+.*(익절|손절)'}).Count -ge 2}|Out-Null;Start-Sleep -Milliseconds 200}
 Save-StockClient $ProcessId (Join-Path $Output 'backtest-trades.png')|Out-Null
 if($CheckOutcomeColors){
  $captions=VisibleControls $w ([System.Windows.Automation.AutomationElement]::ControlTypeProperty) ([System.Windows.Automation.ControlType]::Text)|Where-Object {$_.Current.Name -match '^#\d+.*(익절|손절)'}
  $origin=[StockUiAudit+Point]::new();[StockUiAudit]::ClientToScreen([StockUiAudit]::Window($ProcessId),[ref]$origin)|Out-Null;$screen=[Drawing.Bitmap]::new((Join-Path $Output 'backtest-trades.png'));$colorCases=@()
  try{foreach($caption in $captions){$rect=$caption.Current.BoundingRectangle;$red=0;$blue=0;for($cy=[Math]::Max(0,[int]($rect.Top-$origin.y));$cy -lt [Math]::Min($screen.Height,[int]($rect.Bottom-$origin.y));$cy++){for($cx=[Math]::Max(0,[int]($rect.Left-$origin.x));$cx -lt [Math]::Min($screen.Width,[int]($rect.Right-$origin.x));$cx++){$pixel=$screen.GetPixel($cx,$cy);if($pixel.R -gt 205 -and $pixel.G -lt 125 -and $pixel.B -lt 140){$red++};if($pixel.R -lt 110 -and $pixel.G -gt 90 -and $pixel.G -lt 180 -and $pixel.B -gt 210){$blue++}}};$profit=$caption.Current.Name.Contains('익절');$ok=if($profit){$red -ge 5}else{$blue -ge 5};$colorCases+=@{Caption=$caption.Current.Name;Expected=$(if($profit){'red'}else{'blue'});RedPixels=$red;BluePixels=$blue;Success=$ok};if(-not $ok){throw ('Actual caption color mismatch: '+$caption.Current.Name)}}}finally{$screen.Dispose()}
  if(-not($colorCases.Expected -contains 'red') -or -not($colorCases.Expected -contains 'blue')){throw 'Native outcome-color check requires both a winning and a losing exit'};$result.OutcomeColors=$colorCases
 }
 $area=(Find-StockControl $w '' 'MainPriceChart').Current.BoundingRectangle;$x=[int]($area.X+$area.Width*.55);$y=[int]($area.Y+$area.Height*.40)
 $before=Chart-Hover $w $x $y
 [StockUiAudit]::Down();foreach($delta in @(30,70,110,160)){[StockUiAudit]::SetCursorPos($x+$delta,$y)|Out-Null;Start-Sleep -Milliseconds 35};[StockUiAudit]::Up();Start-Sleep -Milliseconds 120
 $after=Chart-Hover $w $x $y
 if($before -notmatch '^\d{4}\.\d{2}\.\d{2}' -or $before.Substring(0,10) -eq $after.Substring(0,10)){throw ('Drag did not move the candle date: '+$before+' -> '+$after)}
 $result.DragBefore=$before;$result.DragAfter=$after;$result.DragChangedDate=$true
 Save-StockClient $ProcessId (Join-Path $Output 'drag-older.png')|Out-Null
 [StockUiAudit]::Down();foreach($delta in @(-30,-70,-110,-160)){[StockUiAudit]::SetCursorPos($x+$delta,$y)|Out-Null;Start-Sleep -Milliseconds 35};[StockUiAudit]::Up();Start-Sleep -Milliseconds 120
 $returned=Chart-Hover $w $x $y;if($returned.Substring(0,10) -ne $before.Substring(0,10)){throw ('Opposite drag did not restore the viewport: '+$returned)};$result.OppositeDragRestoredDate=$true
 [StockUiAudit]::Wheel(120);Start-Sleep -Milliseconds 160;Save-StockClient $ProcessId (Join-Path $Output 'wheel-at-pointer.png')|Out-Null;[StockUiAudit]::Wheel(-120);Start-Sleep -Milliseconds 160
 $badge=Wait-Ui {VisibleControls $w ([System.Windows.Automation.AutomationElement]::ControlTypeProperty) ([System.Windows.Automation.ControlType]::Text)|Where-Object {$_.Current.Name -match '^#\d+.*(매수|매도)'}|Select-Object -First 1}
 $result.Caption=$badge.Current.Name;Click-StockControl $badge
 $notice=Wait-Ui {Find-StockControl $w '안내' '' ([System.Windows.Automation.ControlType]::Window)}
 $info=VisibleControls $notice ([System.Windows.Automation.AutomationElement]::ControlTypeProperty) ([System.Windows.Automation.ControlType]::Text)|Where-Object {$_.Current.Name -match '^거래 #'}|Select-Object -First 1
 if(-not $info){throw 'Single caption click did not show execution details'};$result.TradeDetail=$info.Current.Name;$result.SingleClickDetails=$true
 # Show-StockWindow pins the owner above other applications for screen capture.
 # A newly owned modal window must also be raised, or its automation tree exists
 # behind the pinned owner and a physical confirmation click cannot reach it.
 [StockUiAudit]::SetWindowPos([IntPtr]$notice.Current.NativeWindowHandle,[IntPtr](-1),0,0,0,0,0x43)|Out-Null
 [StockUiAudit]::SetForegroundWindow([IntPtr]$notice.Current.NativeWindowHandle)|Out-Null;Start-Sleep -Milliseconds 180
 Save-StockWindow $notice (Join-Path $Output 'single-click-detail.png')|Out-Null;Click-StockControl (Find-StockControl $notice '확인' '' ([System.Windows.Automation.ControlType]::Button))
 Wait-Ui {-not (Find-StockControl $w '안내' '' ([System.Windows.Automation.ControlType]::Window))} 5|Out-Null
 $w=Show-StockWindow $ProcessId
 Click-StockControl (Find-StockControl $w '상세 결과 / 판단 기록' '' ([System.Windows.Automation.ControlType]::Button))
 $detail=Wait-Ui {VisibleControls $w ([System.Windows.Automation.AutomationElement]::ControlTypeProperty) ([System.Windows.Automation.ControlType]::Window)|Where-Object {$_.Current.Name -like '삼성전자*'}|Select-Object -First 1}
 [StockUiAudit]::SetWindowPos([IntPtr]$detail.Current.NativeWindowHandle,[IntPtr](-1),0,0,0,0,0x43)|Out-Null;[StockUiAudit]::SetForegroundWindow([IntPtr]$detail.Current.NativeWindowHandle)|Out-Null;Start-Sleep -Milliseconds 180
 $detailBadge=Wait-Ui {VisibleControls $detail ([System.Windows.Automation.AutomationElement]::ControlTypeProperty) ([System.Windows.Automation.ControlType]::Text)|Where-Object {$_.Current.Name -match '^#\d+.*(매수|매도)'}|Select-Object -First 1};$result.DetailCaption=$detailBadge.Current.Name;Click-StockControl $detailBadge
 # Avalonia DataGrid12.1.2 exposes rows/cells but no UIA Selection provider.
 # Verify the original execution detail AND its selected row's actual screen
 # color instead of treating unsupported automation as an application failure.
 $tradeGrid=Find-StockControl $detail '' 'Trades';$editor=VisibleControls $detail ([System.Windows.Automation.AutomationElement]::ControlTypeProperty) ([System.Windows.Automation.ControlType]::Edit)|Select-Object -Last 1
 $detailText=([System.Windows.Automation.ValuePattern]$editor.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern)).Current.Value
 $firstLine=($detailText -split '\r?\n')[0];if($firstLine -notmatch '^거래 #(\d+) · (매수|매도) · (.+)$'){throw 'Chart click did not select an execution detail'}
 $number=$Matches[1];$side=$Matches[2];$time=$Matches[3];if($result.DetailCaption -notmatch ('^#'+$number+'(~|\s)')){throw 'Chart caption and selected execution number differ'}
 $matchingRow=VisibleControls $tradeGrid ([System.Windows.Automation.AutomationElement]::ControlTypeProperty) ([System.Windows.Automation.ControlType]::DataItem)|Where-Object {$texts=@(VisibleControls $_ ([System.Windows.Automation.AutomationElement]::ControlTypeProperty) ([System.Windows.Automation.ControlType]::Text)|ForEach-Object {$_.Current.Name});$texts -contains $number -and $texts -contains $side -and $texts -contains $time}|Select-Object -First 1
 if(-not $matchingRow){throw 'Selected execution has no matching visible grid row'}
 $picture=Join-Path $Output 'detail-selected-trade.png';Save-StockWindow $detail $picture|Out-Null;$image=[Drawing.Bitmap]::new($picture)
 try{$rowBounds=$matchingRow.Current.BoundingRectangle;$dialogBounds=$detail.Current.BoundingRectangle;$paint=$image.GetPixel([int]($rowBounds.Left-$dialogBounds.Left+10),[int]($rowBounds.Top-$dialogBounds.Top+8));$selectedColor=($paint.R -eq 240 -and $paint.G -eq 240 -and $paint.B -eq 240) -or ($paint.R -eq 0 -and $paint.G -eq 120 -and $paint.B -eq 215);if(-not $selectedColor){throw ('Execution row is not painted as selected: '+$paint)}}finally{$image.Dispose()}
 $result.DetailGridSelection=$true;$result.SelectedExecution=$firstLine;$result.GridSelectionEvidence='Execution text + matching selected-color screen pixels; UIA Selection is unsupported'
 ([System.Windows.Automation.WindowPattern]$detail.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern)).Close();$w=Show-StockWindow $ProcessId
 $dateButton=Find-StockControl $w '' 'PART_Button' ([System.Windows.Automation.ControlType]::Button);Click-StockControl $dateButton
 $calendar=Wait-Ui {Find-StockControl $w '' 'PART_Calendar' ([System.Windows.Automation.ControlType]::Calendar)};$r=$calendar.Current.BoundingRectangle
 $header=Find-StockControl $calendar '' 'PART_HeaderButton' ([System.Windows.Automation.ControlType]::Button);$h=$header.Current.BoundingRectangle
 if($r.Width -ne 202 -or $r.Height -ne 201 -or $h.Left -lt $r.Left -or $h.Right -gt $r.Right){throw 'Compact calendar/header bounds are clipped'}
 $result.CalendarSize=@{Width=$r.Width;Height=$r.Height};$result.CalendarHeader=$header.Current.Name
 Save-StockClient $ProcessId (Join-Path $Output 'calendar-month.png')|Out-Null;Click-StockControl $header;Save-StockClient $ProcessId (Join-Path $Output 'calendar-year.png')|Out-Null;[StockUiAudit]::Key(27)
 $result.Success=$true;Write-Output ('PASS native backtest/drag/label/calendar: '+$result.BacktestSummary)
}catch{$result.Error=$_.Exception.Message;throw}
finally{$result|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $Output 'result.json') -Encoding utf8;[StockUiAudit]::SetWindowPos([StockUiAudit]::Window($ProcessId),[IntPtr](-2),0,0,0,0,0x13)|Out-Null}
