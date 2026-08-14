using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace LuoIsHere.CodexMonitor.App.Windows;

internal static class WindowClickThroughService
{
    private const int WindowLongExtendedStyle = -20;
    private const long WindowStyleExtendedTransparent = 0x00000020L;
    private const long WindowStyleExtendedNoActivate = 0x08000000L;

    public static void SetClickThrough(Window window, bool enabled)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        var style = GetWindowStyle(handle);
        var updatedStyle = enabled
            ? style | WindowStyleExtendedTransparent | WindowStyleExtendedNoActivate
            : style & ~(WindowStyleExtendedTransparent | WindowStyleExtendedNoActivate);

        if (updatedStyle != style)
        {
            _ = SetWindowStyle(handle, updatedStyle);
        }
    }

    private static long GetWindowStyle(IntPtr handle)
        => IntPtr.Size == 8
            ? GetWindowLongPtr64(handle, WindowLongExtendedStyle).ToInt64()
            : GetWindowLong32(handle, WindowLongExtendedStyle);

    private static IntPtr SetWindowStyle(IntPtr handle, long style)
        => IntPtr.Size == 8
            ? SetWindowLongPtr64(handle, WindowLongExtendedStyle, new IntPtr(style))
            : new IntPtr(SetWindowLong32(handle, WindowLongExtendedStyle, unchecked((int)style)));

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowLong32(IntPtr windowHandle, int index);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr64(IntPtr windowHandle, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
    private static extern int SetWindowLong32(IntPtr windowHandle, int index, int value);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr64(IntPtr windowHandle, int index, IntPtr value);
}
