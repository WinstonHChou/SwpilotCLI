using System.Runtime.InteropServices;
using Microsoft.VisualBasic;
using SolidWorks.Interop.sldworks;

namespace SolidWorksConsole;

internal static class Program
{
    private static readonly Pt[] IcPointsMm =
    [
        new(521.066, 218.903),
        new(524.649, 220.548),
        new(528.378, 222.200),
        new(532.278, 223.866),
        new(536.379, 225.554),
        new(540.712, 227.272),
        new(545.316, 229.033),
        new(550.234, 230.851),
        new(555.519, 232.741),
        new(561.229, 234.724),
        new(567.439, 236.825),
        new(574.234, 239.073),
        new(581.723, 241.506),
        new(590.040, 244.173),
        new(599.352, 247.135),
        new(609.877, 250.470),
        new(621.901, 254.284),
        new(635.808, 258.718),
        new(652.129, 263.968),
        new(671.626, 270.315),
        new(695.427, 278.175),
        new(725.287, 288.196),
        new(764.106, 301.449),
        new(817.047, 319.836),
        new(894.313, 347.122),
        new(1019.346, 391.959),
        new(1260.794, 479.695),
        new(1944.198, 730.588),
    ];

    [STAThread]
    private static int Main()
    {
        SldWorks? swApp = null;
        ModelDoc2? swDoc = null;

        try
        {
            swApp = ConnectToSolidWorks();
            if (swApp == null)
            {
                Console.Error.WriteLine("Unable to attach to SolidWorks.");
                return 2;
            }

            swDoc = swApp.IActiveDoc2;
            if (swDoc == null)
            {
                Console.Error.WriteLine("No active document in SolidWorks.");
                return 3;
            }

            ExecuteTask(swDoc);
            Console.WriteLine("Task completed successfully.");
            return 0;
        }
        catch (COMException ex)
        {
            Console.Error.WriteLine($"COM error: 0x{ex.ErrorCode:X8} - {ex.Message}");
            return ex.ErrorCode;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
        finally
        {
            if (swDoc != null) Marshal.ReleaseComObject(swDoc);
            if (swApp != null) Marshal.ReleaseComObject(swApp);
        }
    }

    private static void ExecuteTask(ModelDoc2 swDoc)
    {
        var sketch = EnsureSketchEditing(swDoc);
        var sketchMgr = (SketchManager)swDoc.SketchManager;

        bool oldAddToDb = sketchMgr.AddToDB;
        bool oldDisplay = sketchMgr.DisplayWhenAdded;

        try
        {
            sketchMgr.AddToDB = true;
            sketchMgr.DisplayWhenAdded = false;

            int index = 1;
            foreach (Pt point in IcPointsMm)
            {
                var created = sketchMgr.CreatePoint(point.X / 1000.0, point.Y / 1000.0, 0.0);
                if (created == null)
                {
                    throw new InvalidOperationException($"Failed to create point {index}.");
                }

                if (!created.Select4(false, null))
                {
                    throw new InvalidOperationException($"Failed to select point {index}.");
                }

                swDoc.SketchAddConstraints("sgFIXED");
                swDoc.ClearSelection2(true);
                Console.WriteLine($"P{index}: ({point.X:F3}, {point.Y:F3}) fixed");
                index++;
            }
        }
        finally
        {
            sketchMgr.AddToDB = oldAddToDb;
            sketchMgr.DisplayWhenAdded = oldDisplay;
            swDoc.ClearSelection2(true);
            swDoc.GraphicsRedraw2();
            GC.KeepAlive(sketch);
        }
    }

    private static Sketch EnsureSketchEditing(ModelDoc2 swDoc)
    {
        var sketchMgr = (SketchManager)swDoc.SketchManager;
        if (sketchMgr.ActiveSketch is Sketch active)
        {
            return active;
        }

        Feature? feat = swDoc.IFirstFeature();
        while (feat != null)
        {
            if (string.Equals(feat.Name, "草圖1", StringComparison.OrdinalIgnoreCase))
            {
                swDoc.ClearSelection2(true);
                if (!feat.Select2(false, -1))
                {
                    throw new InvalidOperationException("Failed to select 草圖1.");
                }

                swDoc.EditSketch();
                if (sketchMgr.ActiveSketch is Sketch entered)
                {
                    return entered;
                }
            }

            feat = feat.GetNextFeature() as Feature;
        }

        throw new InvalidOperationException("Cannot enter 草圖1.");
    }

    private static SldWorks? ConnectToSolidWorks()
    {
        try
        {
            return Interaction.GetObject(string.Empty, "SldWorks.Application") as SldWorks;
        }
        catch (COMException)
        {
        }

        var type = Type.GetTypeFromProgID("SldWorks.Application");
        return type == null ? null : (SldWorks?)Activator.CreateInstance(type);
    }

    private readonly record struct Pt(double X, double Y);
}
