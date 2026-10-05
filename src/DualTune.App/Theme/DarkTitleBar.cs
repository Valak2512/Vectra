using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace DualTune.App.Theme;

/// <summary>Passe la barre de titre Windows en mode sombre (Windows 10 20H1+ / 11).</summary>
public static class DarkTitleBar
{
    private const int UseImmersiveDarkMode = 20;
    private const int UseImmersiveDarkModeBefore20H1 = 19;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    public static void Apply(Window window)
    {
        void Set()
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero) return;
            int on = 1;
            if (DwmSetWindowAttribute(hwnd, UseImmersiveDarkMode, ref on, sizeof(int)) != 0)
                DwmSetWindowAttribute(hwnd, UseImmersiveDarkModeBefore20H1, ref on, sizeof(int));
        }

        if (new WindowInteropHelper(window).Handle != IntPtr.Zero) Set();
        else window.SourceInitialized += (_, _) => Set();
    }
}
