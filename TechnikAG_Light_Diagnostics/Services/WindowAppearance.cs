using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace TechnikAG_Light_Diagnostics.Services;

public static class WindowAppearance
{
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaUseImmersiveDarkModeLegacy = 19;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        IntPtr windowHandle,
        int attribute,
        ref int attributeValue,
        int attributeSize);

    public static void UseLightTitleBar(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        var disabled = 0;
        var result = DwmSetWindowAttribute(
            handle,
            DwmwaUseImmersiveDarkMode,
            ref disabled,
            sizeof(int));

        if (result != 0)
        {
            DwmSetWindowAttribute(
                handle,
                DwmwaUseImmersiveDarkModeLegacy,
                ref disabled,
                sizeof(int));
        }
    }
}
