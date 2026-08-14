using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;

namespace LuoIsHere.CodexMonitor.App.Windows;

internal static class WindowBackdropService
{
    private const string PersonalizeRegistryPath =
        @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const int WindowLongStyle = -16;
    private const long WindowStyleSystemMenu = 0x00080000L;
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaWindowCornerPreference = 33;
    private const int DwmwaBorderColor = 34;
    private const int DwmwaSystemBackdropType = 38;
    private const int DwmWindowCornerPreferenceRound = 2;
    private const int DwmColorNone = unchecked((int)0xFFFFFFFE);
    private const int DwmSystemBackdropTypeTransientWindow = 3;
    private const int WindowCompositionAttributeAccentPolicy = 19;
    private const int AccentEnableAcrylicBlurBehind = 4;
    private const int DarkAcrylicGradientColor = unchecked((int)0x30000000);

    public static bool RequiresLayeredTransparencyFallback()
        => !IsSystemTransparencyEnabled();

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

        if (window.AllowsTransparency)
        {
            // WPF layered windows already compose their alpha channel. Applying
            // ACCENT_ENABLE_ACRYLICBLURBEHIND here turns the surface opaque on
            // current Windows 11 builds, so keep the native blur path only for
            // non-layered windows.
            return;
        }

        RemoveNativeCaptionControls(handle);

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

            var borderColor = DwmColorNone;
            _ = DwmSetWindowAttribute(
                handle,
                DwmwaBorderColor,
                ref borderColor,
                Marshal.SizeOf<int>());
        }

        if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22621))
        {
            var backdropType = DwmSystemBackdropTypeTransientWindow;
            _ = DwmSetWindowAttribute(
                handle,
                DwmwaSystemBackdropType,
                ref backdropType,
                Marshal.SizeOf<int>());
        }
        else
        {
            _ = TryApplyAcrylicBlur(handle);
        }
    }

    private static bool IsSystemTransparencyEnabled()
    {
        try
        {
            using var personalizeKey = Registry.CurrentUser.OpenSubKey(PersonalizeRegistryPath);
            return personalizeKey?.GetValue("EnableTransparency") is not int value || value != 0;
        }
        catch (Exception exception) when (exception is System.Security.SecurityException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static void RemoveNativeCaptionControls(IntPtr handle)
    {
        var currentStyle = GetWindowStyle(handle);
        _ = SetWindowStyle(handle, currentStyle & ~WindowStyleSystemMenu);
    }

    private static long GetWindowStyle(IntPtr handle)
        => IntPtr.Size == 8
            ? GetWindowLongPtr64(handle, WindowLongStyle).ToInt64()
            : GetWindowLong32(handle, WindowLongStyle);

    private static IntPtr SetWindowStyle(IntPtr handle, long style)
        => IntPtr.Size == 8
            ? SetWindowLongPtr64(handle, WindowLongStyle, new IntPtr(style))
            : new IntPtr(SetWindowLong32(handle, WindowLongStyle, unchecked((int)style)));

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
            AccentFlags = 0,
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
    private static extern int DwmSetWindowAttribute(
        IntPtr windowHandle,
        int attribute,
        ref int attributeValue,
        int attributeSize);

    [DllImport("user32.dll")]
    private static extern int SetWindowCompositionAttribute(
        IntPtr windowHandle,
        ref WindowCompositionAttributeData data);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowLong32(IntPtr windowHandle, int index);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr64(IntPtr windowHandle, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
    private static extern int SetWindowLong32(IntPtr windowHandle, int index, int value);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr64(IntPtr windowHandle, int index, IntPtr value);

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
