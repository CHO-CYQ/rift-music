param([Parameter(Mandatory=$true)][string]$Executable, [Parameter(Mandatory=$true)][string]$Report, [switch]$CheckMaximizedWorkArea)
$ErrorActionPreference='Stop'
$appPath=(Resolve-Path -LiteralPath $Executable).Path
if(Get-Process RiftMusic -ErrorAction SilentlyContinue | Where-Object {$_.Path -eq $appPath}) {throw 'Exit this application before the cold-start test.'}
Add-Type @'
using System;
using System.Text;
using System.Runtime.InteropServices;
public static class InstalledWindowProbe {
 [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left,Top,Right,Bottom; }
 [StructLayout(LayoutKind.Sequential)] public struct MonitorInfo { public int Size; public Rect Monitor,Work; public uint Flags; }
 public delegate bool Callback(IntPtr h,IntPtr l);
 [DllImport("user32.dll")] public static extern bool EnumWindows(Callback c,IntPtr l);
 [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h,out uint p);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr h,StringBuilder s,int n);
 [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
 [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h);
 [DllImport("user32.dll")] public static extern bool IsZoomed(IntPtr h);
 [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h,out Rect bounds);
 [DllImport("user32.dll")] public static extern IntPtr MonitorFromWindow(IntPtr h,uint flags);
 [DllImport("user32.dll")] public static extern bool GetMonitorInfo(IntPtr h,ref MonitorInfo info);
 [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr h);
 [DllImport("user32.dll")] public static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
 [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h,uint m,IntPtr w,IntPtr l);
 [DllImport("user32.dll")] public static extern bool ShowWindowAsync(IntPtr h,int command);
 [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
}
'@
# Native rectangles must not be virtualized by Windows PowerShell's DPI-unaware host.
[void][InstalledWindowProbe]::SetThreadDpiAwarenessContext([IntPtr]::new(-4))
function Find-AppWindow([int]$ProcessId) {
 $matches=[System.Collections.Generic.List[object]]::new()
 [void][InstalledWindowProbe]::EnumWindows({param($h,$l)
  [uint32]$owner=0;[void][InstalledWindowProbe]::GetWindowThreadProcessId($h,[ref]$owner)
  if($owner -eq $ProcessId){$title=[System.Text.StringBuilder]::new(512);[void][InstalledWindowProbe]::GetWindowText($h,$title,512);if($title.ToString().StartsWith('峡谷序曲 · 音乐馆')){$matches.Add($h)}}
  return $true
 },[IntPtr]::Zero)
 if($matches.Count -gt 0){return $matches[0]}
 return [IntPtr]::Zero
}
$primary=Start-Process -FilePath $appPath -PassThru -WindowStyle Normal
$deadline=(Get-Date).AddSeconds(15)
do {$handle=Find-AppWindow $primary.Id;if($handle -ne [IntPtr]::Zero -and [InstalledWindowProbe]::IsWindowVisible($handle)){break};Start-Sleep -Milliseconds 100} while((Get-Date) -lt $deadline)
if($handle -eq [IntPtr]::Zero -or -not [InstalledWindowProbe]::IsWindowVisible($handle)){throw 'Cold start did not display the installed main window.'}
$checks=[System.Collections.Generic.List[string]]::new();$checks.Add('installed executable cold start displays its actual native main window')
foreach($cycle in 1..3){
 if($cycle -eq 2){[void][InstalledWindowProbe]::ShowWindowAsync($handle,6)}else{[void][InstalledWindowProbe]::PostMessage($handle,0x10,[IntPtr]::Zero,[IntPtr]::Zero)}
 $deadline=(Get-Date).AddSeconds(5)
 do {$hidden=($cycle -eq 2 -and [InstalledWindowProbe]::IsIconic($handle)) -or ($cycle -ne 2 -and -not [InstalledWindowProbe]::IsWindowVisible($handle));if($hidden){break};Start-Sleep -Milliseconds 100} while((Get-Date) -lt $deadline)
 if(-not $hidden){throw "Cycle $cycle did not hide/minimize the original window."}
 $second=Start-Process -FilePath $appPath -PassThru -WindowStyle Normal
 if(-not $second.WaitForExit(15000) -or $second.ExitCode -ne 0){throw "Cycle $cycle failed secondary activation."}
 $deadline=(Get-Date).AddSeconds(5)
 do {$restored=[InstalledWindowProbe]::IsWindowVisible($handle) -and -not [InstalledWindowProbe]::IsIconic($handle);if($restored){break};Start-Sleep -Milliseconds 100} while((Get-Date) -lt $deadline)
 $owned=@(Get-Process RiftMusic -ErrorAction SilentlyContinue | Where-Object {$_.Path -eq $appPath})
 if(-not $restored -or $owned.Count -ne 1 -or $owned[0].Id -ne $primary.Id){throw "Cycle $cycle did not restore the single original window."}
 $checks.Add("installed executable reopen cycle $cycle restores the same window with one process")
}
$maximizedBounds=$null
if($CheckMaximizedWorkArea) {
 [void][InstalledWindowProbe]::ShowWindowAsync($handle,3)
 $deadline=(Get-Date).AddSeconds(5)
 do {if([InstalledWindowProbe]::IsZoomed($handle)){break};Start-Sleep -Milliseconds 100} while((Get-Date) -lt $deadline)
 if(-not [InstalledWindowProbe]::IsZoomed($handle)){throw 'Installed window did not maximize.'}
 $monitor=[InstalledWindowProbe+MonitorInfo]::new();$monitor.Size=[Runtime.InteropServices.Marshal]::SizeOf($monitor)
 $bounds=[InstalledWindowProbe+Rect]::new();$settled=$false
 do {
  if(-not [InstalledWindowProbe]::GetMonitorInfo([InstalledWindowProbe]::MonitorFromWindow($handle,2),[ref]$monitor) -or -not [InstalledWindowProbe]::GetWindowRect($handle,[ref]$bounds)){throw 'Cannot read physical installed window bounds.'}
  $settled=[Math]::Abs($bounds.Bottom-$monitor.Work.Bottom) -le 1 -and [Math]::Abs($bounds.Top-$monitor.Work.Top) -le 1 -and [Math]::Abs($bounds.Left-$monitor.Work.Left) -le 1 -and [Math]::Abs($bounds.Right-$monitor.Work.Right) -le 1
  if($settled){break};Start-Sleep -Milliseconds 100
 } while((Get-Date) -lt $deadline)
 if(-not $settled){throw 'Maximized installed window does not settle to the complete monitor work area.'}
 $maximizedBounds=[ordered]@{window=@($bounds.Left,$bounds.Top,$bounds.Right,$bounds.Bottom);work_area=@($monitor.Work.Left,$monitor.Work.Top,$monitor.Work.Right,$monitor.Work.Bottom);dpi=[InstalledWindowProbe]::GetDpiForWindow($handle)}
 $checks.Add('installed maximized native window fits the current monitor work area above its taskbar')
}
[void][InstalledWindowProbe]::SetForegroundWindow($handle)
$record=[ordered]@{time=(Get-Date).ToString('o');executable=$appPath;version=(Get-Item -LiteralPath $appPath).VersionInfo.ProductVersion;process_id=$primary.Id;main_window_handle=$handle.ToInt64();main_window_title='峡谷序曲 · 音乐馆 · v0.1 测试版';visible=[InstalledWindowProbe]::IsWindowVisible($handle);minimized=[InstalledWindowProbe]::IsIconic($handle);passed=$checks.ToArray()}
if($maximizedBounds){$record['maximized_work_area']=$maximizedBounds}
$record | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $Report -Encoding utf8
$record | ConvertTo-Json -Depth 4
