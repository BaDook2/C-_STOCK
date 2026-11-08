param([string]$Executable=(Join-Path $PSScriptRoot '../Stock.Avalonia.exe'),[string]$Output=(Join-Path $PSScriptRoot '../artifacts/native-search'))
# Windows desktop interaction: launches the normal app, types real keyboard events,
# clicks its actual search button and captures SCREEN pixels, never RenderTargetBitmap.
# Run in an interactive Windows session. Only processes created here are closed.
$ErrorActionPreference='Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Drawing
Add-Type -TypeDefinition @'
using System; using System.Runtime.InteropServices;
public static class StockNativeSearch {
 public delegate bool Visit(IntPtr h,IntPtr l);
 [DllImport("user32.dll")] static extern bool EnumWindows(Visit v,IntPtr p);
 [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h,out uint p);
 [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
 [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
 [DllImport("user32.dll")] static extern bool SetCursorPos(int x,int y);
 [DllImport("user32.dll")] static extern void mouse_event(uint flags,uint x,uint y,uint data,UIntPtr extra);
 [StructLayout(LayoutKind.Sequential)] public struct Mouse {public int x,y;public uint data,flags,time;public UIntPtr extra;}
 [StructLayout(LayoutKind.Sequential)] public struct KeyData {public ushort vk,scan;public uint flags,time;public UIntPtr extra;}
 [StructLayout(LayoutKind.Explicit)] public struct Union {[FieldOffset(0)] public Mouse mouse;[FieldOffset(0)] public KeyData key;}
 [StructLayout(LayoutKind.Sequential)] public struct Input {public uint type;public Union value;}
 [DllImport("user32.dll",SetLastError=true)] static extern uint SendInput(uint count,Input[] inputs,int size);
 public static IntPtr Window(int pid){IntPtr found=IntPtr.Zero;EnumWindows((h,l)=>{uint p;GetWindowThreadProcessId(h,out p);if(p==pid&&IsWindowVisible(h)){found=h;return false;}return true;},IntPtr.Zero);return found;}
 public static void Click(int x,int y){SetCursorPos(x,y);mouse_event(2,0,0,0,UIntPtr.Zero);mouse_event(4,0,0,0,UIntPtr.Zero);}
 public static void Key(ushort key){Send(new KeyData{vk=key},new KeyData{vk=key,flags=2});}
 public static void Type(string value){foreach(char ch in value)Send(new KeyData{scan=ch,flags=4},new KeyData{scan=ch,flags=6});}
 static void Send(KeyData down,KeyData up){var inputs=new[]{new Input{type=1,value=new Union{key=down}},new Input{type=1,value=new Union{key=up}}};if(SendInput(2,inputs,Marshal.SizeOf<Input>())!=2)throw new InvalidOperationException("Native keyboard input not delivered");}
}
'@
$Executable=[IO.Path]::GetFullPath($Executable)
$Output=[IO.Path]::GetFullPath($Output)
New-Item -ItemType Directory -Path $Output -Force | Out-Null
$cases=[Collections.Generic.List[object]]::new()
$owned=$null
$window=$null
function Find-Control([string]$Name,[string]$Id='',[System.Windows.Automation.ControlType]$Type=$null){
 $condition=if($Id){[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::AutomationIdProperty,$Id)}else{[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty,$Name)}
 if($Type){$condition=[System.Windows.Automation.AndCondition]::new($condition,[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ControlTypeProperty,$Type))}
 return $window.FindFirst([System.Windows.Automation.TreeScope]::Descendants,$condition)
}
function Capture-Candles([string]$Label){
 $bounds=$window.Current.BoundingRectangle
 $hover=Find-Control '' 'HoverInfo'
 $volume=Find-Control '거래량' '' ([System.Windows.Automation.ControlType]::Text)
 if(-not $hover -or -not $volume){throw 'Visible chart area not found'}
 $plot=$hover.Current.BoundingRectangle
 $left=[Math]::Max(0,[int]($plot.X-$bounds.X+8));$right=[Math]::Min([int]$bounds.Width,[int]($plot.Right-$bounds.X-80))
 $top=[Math]::Max(0,[int]($plot.Bottom-$bounds.Y+8));$bottom=[Math]::Min([int]$bounds.Height,[int]($volume.Current.BoundingRectangle.Y-$bounds.Y-8))
 $bitmap=[Drawing.Bitmap]::new([int]$bounds.Width,[int]$bounds.Height)
 $graphics=[Drawing.Graphics]::FromImage($bitmap)
 try{
  $graphics.CopyFromScreen([int]$bounds.X,[int]$bounds.Y,0,0,$bitmap.Size)
  $picture=Join-Path $Output ($Label+'.png');$bitmap.Save($picture,[Drawing.Imaging.ImageFormat]::Png)
  $red=0;$blue=0
  for($y=$top;$y -lt $bottom;$y+=3){for($x=$left;$x -lt $right;$x+=3){$p=$bitmap.GetPixel($x,$y);if($p.R -gt 200 -and $p.G -lt 120 -and $p.B -lt 150){$red++};if($p.B -gt 180 -and $p.R -lt 120 -and $p.G -gt 70 -and $p.G -lt 180){$blue++}}}
  # Loading progress and header text cannot pass both checks in this chart-only rectangle.
  return @{Picture=$picture;RedCandleSamples=$red;BlueCandleSamples=$blue;VisibleCandles=($red -ge 30 -and $blue -ge 30)}
 }finally{$graphics.Dispose();$bitmap.Dispose()}
}
function Search-Stock([string]$Query,[string]$Symbol,[string]$Label,[bool]$Enter=$false){
 $clock=[Diagnostics.Stopwatch]::StartNew()
 $field=Find-Control '' 'StockSearch';$field.SetFocus()
 $value=[System.Windows.Automation.ValuePattern]$field.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern)
 $value.SetValue('');[StockNativeSearch]::Type($Query);Start-Sleep -Milliseconds 150
 if($value.Current.Value -ne $Query){throw 'Actual native TextBox input mismatch'}
 if($Enter){[StockNativeSearch]::Key(13)}else{
  $button=Find-Control '이름으로 검색' '' ([System.Windows.Automation.ControlType]::Button)
  if(-not $button.Current.IsEnabled){throw 'Search button disabled before search'}
  $rect=$button.Current.BoundingRectangle;[StockNativeSearch]::Click([int]($rect.X+$rect.Width/2),[int]($rect.Y+$rect.Height/2))
 }
 while($clock.Elapsed.TotalSeconds -lt 50){
  $code=Find-Control $Symbol '' ([System.Windows.Automation.ControlType]::Text)
  $button=Find-Control '이름으로 검색' '' ([System.Windows.Automation.ControlType]::Button)
  if($code -and $button.Current.IsEnabled){break};Start-Sleep -Milliseconds 100
 }
 Start-Sleep -Milliseconds 600
 $capture=Capture-Candles $Label
 $ok=($null -ne (Find-Control $Symbol '' ([System.Windows.Automation.ControlType]::Text))) -and $button.Current.IsEnabled -and $capture.VisibleCandles
 $cases.Add([pscustomobject]@{Query=$Query;Symbol=$Symbol;ActualKeyboard=$true;SearchInput=$(if($Enter){'Enter'}else{'Mouse click'});NormalApp=$true;VisibleCandles=$capture.VisibleCandles;RedSamples=$capture.RedCandleSamples;BlueSamples=$capture.BlueCandleSamples;ElapsedMs=$clock.ElapsedMilliseconds;Picture=$capture.Picture;Success=$ok})
 if(-not $ok){throw ('Actual screen retained no visible candles after '+$Query+'; data/automation state alone is not success')}
 Write-Output ('PASS native keyboard/'+$(if($Enter){'Enter'}else{'mouse'})+'/'+$Query+' visible candles: red='+$capture.RedCandleSamples+' blue='+$capture.BlueCandleSamples)
}
try{
 for($restart=0;$restart -lt 2;$restart++){
  $owned=Start-Process -FilePath $Executable -WorkingDirectory ([IO.Path]::GetDirectoryName($Executable)) -WindowStyle Hidden -PassThru
  $startup=[Diagnostics.Stopwatch]::StartNew();$handle=[IntPtr]::Zero
  while($handle -eq [IntPtr]::Zero -and $startup.Elapsed.TotalSeconds -lt 15){$handle=[StockNativeSearch]::Window($owned.Id);if($handle -eq [IntPtr]::Zero){Start-Sleep -Milliseconds 100}}
  if($handle -eq [IntPtr]::Zero){throw 'Normal app did not create a visible window'}
  $window=[System.Windows.Automation.AutomationElement]::FromHandle($handle)
  [StockNativeSearch]::SetForegroundWindow($handle) | Out-Null
  Search-Stock '삼성전자' '005930' ('samsung-run'+$restart)
  if($restart -eq 0){Search-Stock 'SK하이닉스' '000660' 'sk-enter' $true;Search-Stock '삼성전자' '005930' 'samsung-repeat'}
  ([System.Windows.Automation.WindowPattern]$window.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern)).Close()
  if(-not $owned.WaitForExit(5000)){throw 'Own app did not exit cleanly'};$owned=$null;$window=$null
 }
 @{Success=$true;Executable=$Executable;Sha256=(Get-FileHash -LiteralPath $Executable -Algorithm SHA256).Hash;NormalApplication=$true;ScreenPixels=$true;NoRenderTargetBitmap=$true;RealPublicData=$true;Cases=$cases;NoAccountInteraction=$true} | ConvertTo-Json -Depth 7 | Set-Content -LiteralPath (Join-Path $Output 'result.json') -Encoding utf8
}catch{
 @{Success=$false;Executable=$Executable;Error=$_.Exception.Message;Cases=$cases;NormalApplication=$true;ScreenPixels=$true} | ConvertTo-Json -Depth 7 | Set-Content -LiteralPath (Join-Path $Output 'result.json') -Encoding utf8
 throw
}finally{
 if($owned -and -not $owned.HasExited -and $window){([System.Windows.Automation.WindowPattern]$window.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern)).Close();$owned.WaitForExit(5000) | Out-Null}
}
