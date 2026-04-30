using System.Runtime.InteropServices;
using Microsoft.VisualBasic;
using SolidWorks.Interop.sldworks;

namespace SolidWorksConsole;

internal static class Program
{
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
        Console.WriteLine($"Title: {swDoc.GetTitle()}");
        Console.WriteLine($"Path: {swDoc.GetPathName()}");

        var selMgr = (SelectionMgr)swDoc.SelectionManager;
        int selectedCount = selMgr.GetSelectedObjectCount();
        Console.WriteLine($"Selections: {selectedCount}");
        for (int i = 1; i <= selectedCount; i++)
        {
            object? obj = selMgr.GetSelectedObject6(i, -1);
            string typeName = obj?.GetType().FullName ?? "null";
            Console.WriteLine($"  Sel{i}: {typeName}");
            if (obj is DisplayDimension dd && dd.GetDimension2(0) is Dimension dim)
            {
                Console.WriteLine($"    FullName: {dim.FullName}");
                Console.WriteLine($"    SystemValue(m): {dim.SystemValue}");
            }
        }

        Console.WriteLine("Features:");
        Feature? feat = swDoc.IFirstFeature();
        while (feat != null)
        {
            string name = feat.Name;
            string type = feat.GetTypeName2();
            Console.WriteLine($"  {name} | {type}");

            if (name.Contains("點位", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Point", StringComparison.OrdinalIgnoreCase))
            {
                TryPrintFeatureDetail(feat);
            }

            feat = feat.GetNextFeature() as Feature;
        }

        var sketchMgr = (SketchManager)swDoc.SketchManager;
        if (sketchMgr.ActiveSketch is Sketch sketch)
        {
            Console.WriteLine("ActiveSketchPoints:");
            object[]? pts = sketch.GetSketchPoints2() as object[];
            if (pts != null)
            {
                int idx = 1;
                foreach (object obj in pts)
                {
                    if (obj is SketchPoint pt)
                    {
                        Console.WriteLine(
                            $"  P{idx}: ({pt.X * 1000.0:F3}, {pt.Y * 1000.0:F3}) type={pt.Type} status={pt.Status} relations={pt.GetRelationsCount()}");
                        idx++;
                    }
                }
            }

            Console.WriteLine("ActiveSketchSegments:");
            object[]? segs = sketch.GetSketchSegments() as object[];
            if (segs != null)
            {
                int idx = 1;
                foreach (object obj in segs)
                {
                    if (obj is SketchLine line)
                    {
                        var sp = (SketchPoint)line.GetStartPoint2();
                        var ep = (SketchPoint)line.GetEndPoint2();
                        var seg = (SketchSegment)line;
                        Console.WriteLine(
                            $"  S{idx}: line construction={seg.ConstructionGeometry} " +
                            $"start=({sp.X * 1000.0:F3}, {sp.Y * 1000.0:F3}) " +
                            $"end=({ep.X * 1000.0:F3}, {ep.Y * 1000.0:F3})");
                    }
                    else if (obj is SketchSegment seg)
                    {
                        Console.WriteLine($"  S{idx}: type={seg.GetType()} construction={seg.ConstructionGeometry}");
                    }

                    idx++;
                }
            }
        }
    }

    private static void TryPrintFeatureDetail(Feature feat)
    {
        try
        {
            object? specific = feat.GetSpecificFeature2();
            Console.WriteLine($"    Specific: {specific?.GetType().FullName ?? "null"}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"    Specific: error {ex.Message}");
        }

        try
        {
            object? definition = feat.GetDefinition();
            Console.WriteLine($"    Definition: {definition?.GetType().FullName ?? "null"}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"    Definition: error {ex.Message}");
        }
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
}
