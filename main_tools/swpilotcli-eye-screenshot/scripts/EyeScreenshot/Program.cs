// Usage:
//   dotnet run --project EyeScreenshot.csproj
//       -> tries SolidWorks window first (even if minimized/behind), falls back to full screen
//       -> output: ./temp/screenshot/screenshot.png

using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace EyeScreenshot;

internal static class Program
{
    [DllImport("user32.dll")]
    private static extern bool SetProcessDPIAware();

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);

    // PW_RENDERFULLCONTENT=2: captures hardware-accelerated content (needed for SW viewport)
    [DllImport("user32.dll")]
    private static extern bool PrintWindow(IntPtr hwnd, IntPtr hdc, uint flags);

    [DllImport("user32.dll")]
    private static extern bool IsWindow(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hwnd);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    private const uint PW_RENDERFULLCONTENT = 2;

    [STAThread]
    private static int Main(string[] args)
    {
        SetProcessDPIAware();

        try
        {
            var projectRoot = FindProjectRoot();
            var outputDir = Path.Combine(projectRoot, "temp", "screenshot");
            Directory.CreateDirectory(outputDir);
            var outputPath = Path.Combine(outputDir, "screenshot.png");

            var swHwnd = FindSolidWorksHwnd();
            if (swHwnd != IntPtr.Zero)
            {
                // Minimized check — PrintWindow cannot render a minimized window.
                // Exit with code 2 so the caller (AI) can notify the user to restore SW first.
                if (IsIconic(swHwnd))
                {
                    Console.Error.WriteLine("MINIMIZED: SolidWorks is currently minimized.");
                    Console.Error.WriteLine("Please restore the SolidWorks window and run this tool again.");
                    return 2;
                }

                if (TryCaptureWindow(swHwnd, outputPath, out var w, out var h))
                {
                    Console.WriteLine($"Screenshot saved: {outputPath}");
                    Console.WriteLine($"Source: SolidWorks window");
                    Console.WriteLine($"Size: {w}x{h}");
                    return 0;
                }
                Console.Error.WriteLine("PrintWindow failed, falling back to full screen.");
            }
            else
            {
                Console.WriteLine("SolidWorks not found, capturing full screen.");
            }

            // Fallback: full screen
            var bounds = Screen.PrimaryScreen!.Bounds;
            using var bitmap = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
            using var g = Graphics.FromImage(bitmap);
            g.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);
            bitmap.Save(outputPath, ImageFormat.Png);

            Console.WriteLine($"Screenshot saved: {outputPath}");
            Console.WriteLine($"Source: full screen (fallback)");
            Console.WriteLine($"Size: {bounds.Width}x{bounds.Height}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    private static IntPtr FindSolidWorksHwnd()
    {
        foreach (var proc in Process.GetProcessesByName("SLDWORKS"))
        {
            if (proc.MainWindowHandle != IntPtr.Zero)
                return proc.MainWindowHandle;
        }
        return IntPtr.Zero;
    }

    private static bool TryCaptureWindow(IntPtr hwnd, string outputPath, out int width, out int height)
    {
        width = height = 0;
        if (!GetWindowRect(hwnd, out var rect)) return false;

        width  = rect.Right  - rect.Left;
        height = rect.Bottom - rect.Top;
        if (width <= 0 || height <= 0) return false;

        using var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bitmap);
        var hdc = g.GetHdc();
        bool ok = PrintWindow(hwnd, hdc, PW_RENDERFULLCONTENT);
        g.ReleaseHdc(hdc);
        if (!ok) return false;

        bitmap.Save(outputPath, ImageFormat.Png);
        return true;
    }

    private static string FindProjectRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir, "CLAUDE.md")))
                return dir;
            dir = Path.GetDirectoryName(dir);
        }
        return AppContext.BaseDirectory;
    }
}
