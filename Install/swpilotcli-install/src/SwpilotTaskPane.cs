using SolidWorks.Interop.sldworks;
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace SwpilotCLIAddin
{
    public class SwpilotTaskPane
    {
        private static readonly int[] TaskPaneIconSizes = { 20, 32, 40, 64, 96, 128 };

        private ITaskpaneView taskpaneView;
        private TaskPaneControl control;
        private ISldWorks swApp;

        public SwpilotTaskPane(ISldWorks app)
        {
            swApp = app;
        }

        public bool Create()
        {
            try
            {
                string[] iconPaths = ExtractTaskPaneIcons();
                taskpaneView = (ITaskpaneView)swApp.CreateTaskpaneView3(iconPaths, "SwpilotCLI");

                if (taskpaneView == null)
                    taskpaneView = (ITaskpaneView)swApp.CreateTaskpaneView2(iconPaths[1], "SwpilotCLI");

                if (taskpaneView == null)
                    throw new InvalidOperationException("Failed to create Task Pane view.");

                control = new TaskPaneControl();
                control.CreateControl();
                taskpaneView.DisplayWindowFromHandlex64(control.Handle.ToInt64());

                return true;
            }
            catch (Exception ex)
            {
                swApp.SendMsgToUser2("SwpilotCLI create failed: " + ex.Message, 0, 0);
                return false;
            }
        }

        private string[] ExtractTaskPaneIcons()
        {
            string outputDir = Path.Combine(Path.GetTempPath(), "SwpilotCLI_taskpane_icons");
            Directory.CreateDirectory(outputDir);

            var asm = Assembly.GetExecutingAssembly();
            using (var stream = asm.GetManifestResourceStream("SwpilotCLIAddin.SwpilotICO2.png"))
            {
                if (stream == null)
                    throw new FileNotFoundException("Embedded resource not found: SwpilotICO2.png");

                using (var src = new Bitmap(stream))
                {
                    var iconPaths = new string[TaskPaneIconSizes.Length];
                    for (int i = 0; i < TaskPaneIconSizes.Length; i++)
                    {
                        int size = TaskPaneIconSizes[i];
                        string iconPath = Path.Combine(outputDir, "SwpilotCLI_taskpane_" + size + ".png");
                        iconPaths[i] = iconPath;

                        using (var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb))
                        using (var g = Graphics.FromImage(bmp))
                        {
                            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                            g.SmoothingMode = SmoothingMode.HighQuality;
                            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                            g.Clear(Color.Transparent);

                            Rectangle dest = GetFitRect(size, size, src.Width, src.Height);
                            g.DrawImage(src, dest);
                            MakeNearWhiteTransparent(bmp, 245);

                            bmp.Save(iconPath, ImageFormat.Png);
                        }
                    }

                    return iconPaths;
                }
            }
        }

        private static Rectangle GetFitRect(int canvasW, int canvasH, int srcW, int srcH)
        {
            if (srcW <= 0 || srcH <= 0)
                return new Rectangle(0, 0, canvasW, canvasH);

            float scale = Math.Min((float)canvasW / srcW, (float)canvasH / srcH);
            int w = Math.Max(1, (int)Math.Round(srcW * scale));
            int h = Math.Max(1, (int)Math.Round(srcH * scale));
            int x = (canvasW - w) / 2;
            int y = (canvasH - h) / 2;

            return new Rectangle(x, y, w, h);
        }

        private static void MakeNearWhiteTransparent(Bitmap bmp, int threshold)
        {
            for (int y = 0; y < bmp.Height; y++)
            {
                for (int x = 0; x < bmp.Width; x++)
                {
                    Color c = bmp.GetPixel(x, y);
                    if (c.R >= threshold && c.G >= threshold && c.B >= threshold)
                        bmp.SetPixel(x, y, Color.FromArgb(0, c.R, c.G, c.B));
                }
            }
        }

        public void Remove()
        {
            try
            {
                if (control != null)
                {
                    control.Dispose();
                    control = null;
                }
                if (taskpaneView != null)
                {
                    taskpaneView.DeleteView();
                    Marshal.ReleaseComObject(taskpaneView);
                    taskpaneView = null;
                }
            }
            catch { }
        }
    }
}
