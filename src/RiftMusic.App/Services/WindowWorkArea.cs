using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace RiftMusic.App.Services;

/// <summary>Keep borderless maximized windows inside their current monitor's native work area.</summary>
public static class WindowWorkArea
{
    [StructLayout(LayoutKind.Sequential)] private struct Point { public int X,Y; }
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left,Top,Right,Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct MonitorInfo { public int Size; public Rect Monitor,Work; public uint Flags; }
    [StructLayout(LayoutKind.Sequential)] private struct MinMaxInfo { public Point Reserved,MaxSize,MaxPosition,MinTrackSize,MaxTrackSize; }
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr hwnd,uint flags);
    [DllImport("user32.dll")] private static extern bool GetMonitorInfo(IntPtr monitor,ref MonitorInfo info);
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr hwnd);
    public static void Attach(Window window)
    {
        window.SourceInitialized+=(_,_)=>{
            var source=HwndSource.FromHwnd(new WindowInteropHelper(window).Handle);
            if(source is null)return;
            IntPtr Hook(IntPtr hwnd,int message,IntPtr wParam,IntPtr lParam,ref bool handled)
            {
                if(message!=0x24)return IntPtr.Zero; // WM_GETMINMAXINFO uses physical pixels, not WPF DIPs.
                var monitor=new MonitorInfo{Size=Marshal.SizeOf<MonitorInfo>()};
                if(!GetMonitorInfo(MonitorFromWindow(hwnd,2),ref monitor))return IntPtr.Zero;
                var info=Marshal.PtrToStructure<MinMaxInfo>(lParam);
                info.MaxPosition=new(){X=monitor.Work.Left-monitor.Monitor.Left,Y=monitor.Work.Top-monitor.Monitor.Top};
                info.MaxSize=new(){X=monitor.Work.Right-monitor.Work.Left,Y=monitor.Work.Bottom-monitor.Work.Top};
                var scale=Math.Max(96,GetDpiForWindow(hwnd))/96d;
                info.MinTrackSize=new(){X=Math.Min(info.MaxSize.X,(int)Math.Ceiling(window.MinWidth*scale)),Y=Math.Min(info.MaxSize.Y,(int)Math.Ceiling(window.MinHeight*scale))};
                Marshal.StructureToPtr(info,lParam,false);handled=true;return IntPtr.Zero;
            }
            source.AddHook(Hook);
            window.Closed+=(_,_)=>source.RemoveHook(Hook);
        };
    }
}
