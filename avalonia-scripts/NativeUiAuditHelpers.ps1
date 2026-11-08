$ErrorActionPreference='Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Drawing
if(-not ('StockUiAudit' -as [type])){
Add-Type -TypeDefinition @'
using System;using System.Runtime.InteropServices;
public static class StockUiAudit {
 [StructLayout(LayoutKind.Sequential)] public struct Rect {public int left,top,right,bottom;}
 [StructLayout(LayoutKind.Sequential)] public struct Point {public int x,y;}
 [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr h,out Rect r);
 [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h,out Rect r);
 [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr h,ref Point p);
 public delegate bool Visit(IntPtr h,IntPtr l);
 [DllImport("user32.dll")] static extern bool EnumWindows(Visit v,IntPtr p);
 [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h,out uint p);
 [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
 public static int ForegroundPid(){uint p;GetWindowThreadProcessId(GetForegroundWindow(),out p);return (int)p;}
 [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
 [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
 [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h,int cmd);
 [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h,IntPtr after,int x,int y,int w,int ht,uint flags);
 [DllImport("user32.dll")] public static extern bool SetCursorPos(int x,int y);
 [DllImport("user32.dll")] public static extern void mouse_event(uint flags,uint x,uint y,uint data,UIntPtr extra);
 [DllImport("user32.dll")] static extern void keybd_event(byte vk,byte scan,uint flags,UIntPtr extra);
 public static void Key(byte key){keybd_event(key,0,0,UIntPtr.Zero);keybd_event(key,0,2,UIntPtr.Zero);}
 public static void Wheel(int delta){mouse_event(0x800,0,0,unchecked((uint)delta),UIntPtr.Zero);}
 public static void Down(){mouse_event(2,0,0,0,UIntPtr.Zero);}
 public static void Up(){mouse_event(4,0,0,0,UIntPtr.Zero);}
 public static IntPtr Window(int pid){IntPtr found=IntPtr.Zero;long largest=0;EnumWindows((h,l)=>{uint p;GetWindowThreadProcessId(h,out p);if(p==pid&&IsWindowVisible(h)){Rect r;GetWindowRect(h,out r);long area=(long)(r.right-r.left)*(r.bottom-r.top);if(area>largest){found=h;largest=area;}}return true;},IntPtr.Zero);return found;}
 public static void Click(int x,int y){SetCursorPos(x,y);mouse_event(2,0,0,0,UIntPtr.Zero);mouse_event(4,0,0,0,UIntPtr.Zero);}
}
'@
}
function Get-StockWindow([int]$ProcessId){$h=[StockUiAudit]::Window($ProcessId);if($h -eq [IntPtr]::Zero){throw ('No visible app window for '+$ProcessId)};return [System.Windows.Automation.AutomationElement]::FromHandle($h)}
function Show-StockWindow([int]$ProcessId){$clock=[Diagnostics.Stopwatch]::StartNew();while($clock.Elapsed.TotalSeconds -lt 12){$h=[StockUiAudit]::Window($ProcessId);if($h -eq [IntPtr]::Zero){Start-Sleep -Milliseconds 100;continue};$client=[StockUiAudit+Rect]::new();if(-not [StockUiAudit]::GetClientRect($h,[ref]$client) -or $client.right -lt 300 -or $client.bottom -lt 200){Start-Sleep -Milliseconds 100;continue};[StockUiAudit]::ShowWindow($h,9)|Out-Null;[StockUiAudit]::SetWindowPos($h,[IntPtr](-1),180,0,1540,1040,0x40)|Out-Null;[StockUiAudit]::SetForegroundWindow($h)|Out-Null;$p=[StockUiAudit+Point]::new();$p.x=20;$p.y=82;if(-not [StockUiAudit]::ClientToScreen($h,[ref]$p)){Start-Sleep -Milliseconds 100;continue};[StockUiAudit]::Click($p.x,$p.y);Start-Sleep -Milliseconds 250;if([StockUiAudit]::ForegroundPid() -eq $ProcessId){return Get-StockWindow $ProcessId};Start-Sleep -Milliseconds 100};throw ('Target window is not foreground: '+$ProcessId)}
function Find-StockControl($Window,[string]$Name,[string]$Id='',[System.Windows.Automation.ControlType]$Type=$null){$condition=if($Id){[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::AutomationIdProperty,$Id)}else{[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty,$Name)};if($Type){$condition=[System.Windows.Automation.AndCondition]::new($condition,[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ControlTypeProperty,$Type))};return $Window.FindFirst([System.Windows.Automation.TreeScope]::Descendants,$condition)}
function Click-StockControl($Element,[double]$FractionX=.5){if(-not $Element -or $Element.Current.IsOffscreen){throw 'Target is missing or offscreen'};$r=$Element.Current.BoundingRectangle;[StockUiAudit]::Click([int]($r.X+$r.Width*$FractionX),[int]($r.Y+$r.Height/2));Start-Sleep -Milliseconds 250}
function Hover-StockControl($Element){$r=$Element.Current.BoundingRectangle;[StockUiAudit]::SetCursorPos([int]($r.X+$r.Width/2),[int]($r.Y+$r.Height/2))|Out-Null;Start-Sleep -Milliseconds 200}
function Save-StockWindow($Window,[string]$Path){$r=$Window.Current.BoundingRectangle;$bmp=[Drawing.Bitmap]::new([int]$r.Width,[int]$r.Height);$g=[Drawing.Graphics]::FromImage($bmp);try{$g.CopyFromScreen([int]$r.X,[int]$r.Y,0,0,$bmp.Size);$bmp.Save($Path,[Drawing.Imaging.ImageFormat]::Png)}finally{$g.Dispose();$bmp.Dispose()};Write-Output $Path}
function Save-StockClient([int]$ProcessId,[string]$Path){if([StockUiAudit]::ForegroundPid() -ne $ProcessId){throw 'Screenshot target is not foreground'};$h=[StockUiAudit]::Window($ProcessId);$r=[StockUiAudit+Rect]::new();$p=[StockUiAudit+Point]::new();[StockUiAudit]::GetClientRect($h,[ref]$r)|Out-Null;[StockUiAudit]::ClientToScreen($h,[ref]$p)|Out-Null;$bmp=[Drawing.Bitmap]::new($r.right,$r.bottom);$g=[Drawing.Graphics]::FromImage($bmp);try{$g.CopyFromScreen($p.x,$p.y,0,0,$bmp.Size);$bmp.Save($Path,[Drawing.Imaging.ImageFormat]::Png)}finally{$g.Dispose();$bmp.Dispose()};Write-Output $Path}
function List-StockControls($Window,[int]$Limit=160){$all=$Window.FindAll([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.Condition]::TrueCondition);for($i=0;$i -lt [Math]::Min($all.Count,$Limit);$i++){$e=$all[$i];if(-not $e.Current.IsOffscreen){Write-Output ($i.ToString()+' | '+$e.Current.ControlType.ProgrammaticName+' | '+$e.Current.AutomationId+' | '+$e.Current.Name+' | Enabled='+$e.Current.IsEnabled+' | '+$e.Current.BoundingRectangle.ToString())}}}
