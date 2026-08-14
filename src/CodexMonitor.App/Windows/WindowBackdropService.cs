using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace LuoIsHere.CodexMonitor.App.Windows;

internal static class WindowBackdropService
{
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaWindowCornerPreference = 33;
    private const int DwmwaSystemBackdropType = 38;
    private const int DwmWindowCornerPreferenceRound = 2;
    private const int DwmSystemBackdropTypeTransientWindow = 3;
    private const int WindowCompositionAttributeAccentPolicy = 19;
    private const int AccentEnableAcrylicBlurBehind = 4;
    private const int DarkAcrylicGradientColor = unchecked((int)0x30000000);

    public static void ApplyDarkAcrylic(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        if (HwndSource.FromHwnd(handle) is { CompositionTarget: not null } source)
        {
            source.CompositionTarget.BackgroundColor = Colors.Transparent;
        }

        var margins = new Margins(-1, -1, -1, -1);
        _ = DwmExtendFrameIntoClientArea(handle, ref margins);

        var darkMode = 1;
        _ = DwmSetWindowAttribute(
            handle,
            DwmwaUseImmersiveDarkMode,
            ref darkMode,
            Marshal.SizeOf<int>());

        if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
        {
            var cornerPreference = DwmWindowCornerPreferenceRound;
            _ = DwmSetWindowAttribute(
                handle,
                DwmwaWindowCornerPreference,
                ref cornerPreference,
                Marshal.SizeOf<int>());
        }

        if (!TryApplyAcrylicBlur(handle) &&
            OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22621))
        {
            var backdropType = DwmSystemBackdropTypeTransientWindow;
            _ = DwmSetWindowAttribute(
                handle,
                DwmwaSystemBackdropType,
                ref backdropType,
                Marshal.SizeOf<int>());
        }
    }

    private static bool TryApplyAcrylicBlur(IntPtr handle)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17134))
        {
            return false;
        }

        try
        {
            return ApplyAcrylicBlur(handle);
        }
        catch (DllNotFoundException)
        {
            return false;
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }
    }

    private static bool ApplyAcrylicBlur(IntPtr handle)
    {
        var accentPolicy = new AccentPolicy
        {
            AccentState = AccentEnableAcrylicBlurBehind,
            AccentFlags = 2,
            GradientColor = DarkAcrylicGradientColor,
        };
        var accentPolicySize = Marshal.SizeOf<AccentPolicy>();
        var accentPolicyPointer = Marshal.AllocHGlobal(accentPolicySize);

        try
        {
            Marshal.StructureToPtr(accentPolicy, accentPolicyPointer, fDeleteOld: false);
            var data = new WindowCompositionAttributeData
            {
                Attribute = WindowCompositionAttributeAccentPolicy,
                Data = accentPolicyPointer,
                SizeOfData = accentPolicySize,
            };
            return SetWindowCompositionAttribute(handle, ref data) != 0;
        }
        finally
        {
            Marshal.FreeHGlobal(accentPolicyPointer);
        }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmExtendFrameIntoClientArea(IntPtr windowHandle, ref Margins margins);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        IntPtr windowHandle,
        int attribute,
        ref int attributeValue,
        int attributeSize);

    [DllImport("user32.dll")]
    private static extern int SetWindowCompositionAttribute(
        IntPtr windowHandle,
        ref WindowCompositionAttributeData data);

    [StructLayout(LayoutKind.Sequential)]
    private struct Margins(int left, int right, int top, int bottom)
    {
        public int Left = left;
        public int Right = right;
        public int Top = top;
        public int Bottom = bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AccentPolicy
    {
        public int AccentState;
        public int AccentFlags;
        public int GradientColor;
        public int AnimationId;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowCompositionAttributeData
    {
        public int Attribute;
        public IntPtr Data;
        public int SizeOfData;
    }
}
