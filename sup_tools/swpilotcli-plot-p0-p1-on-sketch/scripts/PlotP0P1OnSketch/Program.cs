using System.Runtime.InteropServices;
using Microsoft.VisualBasic;
using SolidWorks.Interop.sldworks;

namespace SolidWorksConsole;

internal static class Program
{
    private const string SketchName = "草圖5";
    private static readonly Pt P0 = new(251.342, 249.402);
    private static readonly Pt P1 = new(251.338, 249.384);
    private static readonly Pt Mid = new((251.342 + 251.338) * 0.5, (249.402 + 249.384) * 0.5);

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
        Sketch sketch = EnsureSketchEditing(swDoc, SketchName);
        var sketchMgr = (SketchManager)swDoc.SketchManager;
        bool oldAddToDb = sketchMgr.AddToDB;
        bool oldDisplay = sketchMgr.DisplayWhenAdded;

        try
        {
            sketchMgr.AddToDB = true;
            sketchMgr.DisplayWhenAdded = false;

            MaybeCreateFixedPoint(swDoc, sketchMgr, sketch, P0);
            MaybeCreateFixedPoint(swDoc, sketchMgr, sketch, P1);
            MaybeCreateFixedPoint(swDoc, sketchMgr, sketch, Mid);
            MaybeCreateLine(sketchMgr, sketch, P0, P1);
        }
        finally
        {
            sketchMgr.AddToDB = oldAddToDb;
            sketchMgr.DisplayWhenAdded = oldDisplay;
            swDoc.ClearSelection2(true);
            swDoc.GraphicsRedraw2();
        }
    }

    private static Sketch EnsureSketchEditing(ModelDoc2 swDoc, string sketchName)
    {
        var sketchMgr = (SketchManager)swDoc.SketchManager;
        if (sketchMgr.ActiveSketch is Sketch)
        {
            swDoc.ClearSelection2(true);
            swDoc.EditSketch();
        }

        Feature? feat = swDoc.IFirstFeature();
        while (feat != null)
        {
            if (string.Equals(feat.Name, sketchName, StringComparison.OrdinalIgnoreCase))
            {
                swDoc.ClearSelection2(true);
                if (!feat.Select2(false, -1))
                {
                    throw new InvalidOperationException($"Failed to select {sketchName}.");
                }

                swDoc.EditSketch();
                if (sketchMgr.ActiveSketch is Sketch entered)
                {
                    return entered;
                }
            }

            feat = feat.GetNextFeature() as Feature;
        }

        throw new InvalidOperationException($"Cannot enter {sketchName}.");
    }

    private static void MaybeCreateFixedPoint(ModelDoc2 swDoc, SketchManager sketchMgr, Sketch sketch, Pt pointMm)
    {
        if (HasNearbySketchPoint(sketch, pointMm, 0.001))
        {
            return;
        }

        var point = sketchMgr.CreatePoint(pointMm.X / 1000.0, pointMm.Y / 1000.0, 0.0);
        if (point == null)
        {
            throw new InvalidOperationException("Failed to create point.");
        }

        if (!point.Select4(false, null))
        {
            throw new InvalidOperationException("Failed to select point.");
        }

        swDoc.SketchAddConstraints("sgFIXED");
        swDoc.ClearSelection2(true);
    }

    private static void MaybeCreateLine(SketchManager sketchMgr, Sketch sketch, Pt startMm, Pt endMm)
    {
        if (HasNearbyLine(sketch, startMm, endMm, 0.001))
        {
            return;
        }

        var seg = sketchMgr.CreateLine(
            startMm.X / 1000.0,
            startMm.Y / 1000.0,
            0.0,
            endMm.X / 1000.0,
            endMm.Y / 1000.0,
            0.0);

        if (seg == null)
        {
            throw new InvalidOperationException("Failed to create P0-P1 line.");
        }
    }

    private static bool HasNearbySketchPoint(Sketch sketch, Pt targetMm, double toleranceMm)
    {
        object[]? pts = sketch.GetSketchPoints2() as object[];
        if (pts == null)
        {
            return false;
        }

        foreach (object obj in pts)
        {
            if (obj is not SketchPoint pt)
            {
                continue;
            }

            var candidate = new Pt(pt.X * 1000.0, pt.Y * 1000.0);
            if (Distance(candidate, targetMm) <= toleranceMm)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasNearbyLine(Sketch sketch, Pt startMm, Pt endMm, double toleranceMm)
    {
        object[]? segments = sketch.GetSketchSegments() as object[];
        if (segments == null)
        {
            return false;
        }

        foreach (object obj in segments)
        {
            if (obj is not SketchLine line)
            {
                continue;
            }

            var a = new Pt(line.GetStartPoint2().X * 1000.0, line.GetStartPoint2().Y * 1000.0);
            var b = new Pt(line.GetEndPoint2().X * 1000.0, line.GetEndPoint2().Y * 1000.0);
            bool sameDir = Distance(a, startMm) <= toleranceMm && Distance(b, endMm) <= toleranceMm;
            bool revDir = Distance(a, endMm) <= toleranceMm && Distance(b, startMm) <= toleranceMm;
            if (sameDir || revDir)
            {
                return true;
            }
        }

        return false;
    }

    private static double Distance(Pt a, Pt b)
    {
        double dx = a.X - b.X;
        double dy = a.Y - b.Y;
        return Math.Sqrt((dx * dx) + (dy * dy));
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
