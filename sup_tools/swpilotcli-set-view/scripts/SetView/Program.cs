using System;
using System.Runtime.InteropServices;
using Microsoft.VisualBasic;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace SolidWorksConsole;

// Set view orientation and/or viewport layout of the active document.
// Usage: SetView [orientation] [layout]
//   orientation: front|back|left|right|top|bottom|iso|dimetric|trimetric
//   layout:      single|two-h|two-v|four
// Either argument is optional. Examples:
//   SetView front
//   SetView iso four
//   SetView four
internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        SldWorks? swApp = null; ModelDoc2? swDoc = null;
        try
        {
            swApp = Interaction.GetObject(string.Empty, "SldWorks.Application") as SldWorks;
            swDoc = swApp?.IActiveDoc2;
            if (swDoc == null) { Console.Error.WriteLine("No active doc"); return 3; }

            string? orientation = null;
            string? layout = null;

            // Parse args — each token is matched against known orientation or layout keywords
            var orientationKeys = new HashSet<string>
                { "front","back","left","right","top","bottom","iso","isometric","dimetric","trimetric" };
            var layoutKeys = new HashSet<string>
                { "single","two-h","two-horizontal","two-v","two-vertical","four" };

            foreach (string arg in args)
            {
                string a = arg.Trim().ToLowerInvariant();
                if (orientationKeys.Contains(a)) orientation = a;
                else if (layoutKeys.Contains(a))  layout      = a;
                else
                {
                    Console.Error.WriteLine($"Unknown argument: '{arg}'");
                    Console.Error.WriteLine("Orientation: front back left right top bottom iso dimetric trimetric");
                    Console.Error.WriteLine("Layout:      single two-h two-v four");
                    return 1;
                }
            }

            if (orientation == null && layout == null)
            {
                orientation = "iso"; // default
            }

            // Apply layout first so orientation renders in the active viewport
            if (layout != null)
            {
                int layoutId = layout switch
                {
                    "single"                       => (int)swViewportDisplay_e.swViewportSingle,
                    "two-h" or "two-horizontal"    => (int)swViewportDisplay_e.swViewportTwoViewHorizontal,
                    "two-v" or "two-vertical"      => (int)swViewportDisplay_e.swViewportTwoViewVertical,
                    "four"                         => (int)swViewportDisplay_e.swViewportFourView,
                    _ => throw new ArgumentException($"Unknown layout: '{layout}'")
                };
                swDoc.ModelViewManager.ViewportDisplay = layoutId;
                Console.WriteLine($"Layout set to: {layout}");
            }

            // Apply orientation
            if (orientation != null)
            {
                (string viewName, int viewId) = orientation switch
                {
                    "front"     => ("*Front",     (int)swStandardViews_e.swFrontView),
                    "back"      => ("*Back",      (int)swStandardViews_e.swBackView),
                    "left"      => ("*Left",      (int)swStandardViews_e.swLeftView),
                    "right"     => ("*Right",     (int)swStandardViews_e.swRightView),
                    "top"       => ("*Top",       (int)swStandardViews_e.swTopView),
                    "bottom"    => ("*Bottom",    (int)swStandardViews_e.swBottomView),
                    "iso"       => ("*Isometric", (int)swStandardViews_e.swIsometricView),
                    "isometric" => ("*Isometric", (int)swStandardViews_e.swIsometricView),
                    "dimetric"  => ("*Dimetric",  (int)swStandardViews_e.swDimetricView),
                    "trimetric" => ("*Trimetric", (int)swStandardViews_e.swTrimetricView),
                    _ => throw new ArgumentException($"Unknown orientation: '{orientation}'")
                };
                swDoc.ShowNamedView2(viewName, viewId);
                swDoc.ViewZoomtofit2();
                Console.WriteLine($"Orientation set to: {orientation}");
            }

            return 0;
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
        finally
        {
            if (swDoc != null) Marshal.ReleaseComObject(swDoc);
            if (swApp != null) Marshal.ReleaseComObject(swApp);
        }
    }
}
