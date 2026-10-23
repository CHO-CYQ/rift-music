using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;

namespace RiftMusic.App.Services;

internal static class InstanceScope
{
    // Local\ already separates Windows sessions. Desktops within that session
    // also need independent windows; an invisible desktop must not absorb launches.
    internal static string Name()
    {
        using var currentUser = WindowsIdentity.GetCurrent();
        var identity = currentUser.User?.Value
            ?? Environment.UserDomainName + "\\" + Environment.UserName;
        var desktop = ObjectName(GetProcessWindowStation()) + "\\" + ObjectName(GetThreadDesktop(GetCurrentThreadId()));
        using var process = System.Diagnostics.Process.GetCurrentProcess();
        return "RiftOverture-Desktop-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity + "|" + process.SessionId + "|" + desktop)))[..24];
    }
    private static string ObjectName(IntPtr handle)
    {
        var value = new StringBuilder(256);
        if (!GetUserObjectInformation(handle, 2, value, value.Capacity * 2, out _))
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "无法识别当前 Windows 桌面。");
        return value.ToString();
    }
    [DllImport("user32.dll")] private static extern IntPtr GetProcessWindowStation();
    [DllImport("user32.dll")] private static extern IntPtr GetThreadDesktop(uint thread);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("user32.dll", CharSet=CharSet.Unicode, SetLastError=true)]
    [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetUserObjectInformation(IntPtr handle, int index, StringBuilder value, int length, out int needed);
}
