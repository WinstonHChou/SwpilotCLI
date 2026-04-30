using System.Runtime.InteropServices;
using Microsoft.VisualBasic;
using SolidWorks.Interop.sldworks;

namespace SolidWorksConsole;

internal static class Program
{
    private const string DimFullName = "D15@草圖1@點位2 text.Part";
    private const string SketchName = "草圖1";
    private const double DeltaMm = 0.01;

    private static readonly Pt UpperSeedMm = new(251.342, 249.402);
    private static readonly Pt LowerSeedMm = new(431.880, 337.848);

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
        var sketch = EnsureSketchEditing(swDoc);
        var dim = swDoc.Parameter(DimFullName) as Dimension;
        if (dim == null)
        {
            throw new InvalidOperationException($"Cannot find dimension {DimFullName}.");
        }

        double originalMm = dim.SystemValue * 1000.0;
        Console.WriteLine($"Dimension: {DimFullName}");
        Console.WriteLine($"Original(mm): {originalMm:F3}");
        Console.WriteLine("d_mm,ic_x_mm,ic_y_mm");

        try
        {
            Pt nextUpperTarget = UpperSeedMm;
            Pt nextLowerTarget = LowerSeedMm;

            for (int d = 68; d <= 108; d++)
            {
                SetDimensionMm(swDoc, dim, d);
                var basePoints = GetTrackedPoints(sketch, nextUpperTarget, nextLowerTarget);

                SetDimensionMm(swDoc, dim, d + DeltaMm);
                var movedPoints = GetTrackedPoints(sketch, basePoints.Upper, basePoints.Lower);

                Pt ic = ComputeFiniteCenter(basePoints.Upper, movedPoints.Upper, basePoints.Lower, movedPoints.Lower);
                Console.WriteLine($"{d},{ic.X:F3},{ic.Y:F3}");

                nextUpperTarget = movedPoints.Upper;
                nextLowerTarget = movedPoints.Lower;
            }
        }
        finally
        {
            SetDimensionMm(swDoc, dim, originalMm);
            swDoc.GraphicsRedraw2();
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
            if (string.Equals(feat.Name, SketchName, StringComparison.OrdinalIgnoreCase))
            {
                swDoc.ClearSelection2(true);
                if (!feat.Select2(false, -1))
                {
                    throw new InvalidOperationException($"Failed to select {SketchName}.");
                }

                swDoc.EditSketch();
                if (sketchMgr.ActiveSketch is Sketch entered)
                {
                    return entered;
                }

                break;
            }

            feat = feat.GetNextFeature() as Feature;
        }

        throw new InvalidOperationException($"Cannot enter {SketchName}.");
    }

    private static void SetDimensionMm(ModelDoc2 swDoc, Dimension dim, double valueMm)
    {
        dim.SystemValue = valueMm / 1000.0;
        if (!swDoc.EditRebuild3())
        {
            throw new InvalidOperationException($"Rebuild failed after setting dimension to {valueMm:F3} mm.");
        }
    }

    private static (Pt Upper, Pt Lower) GetTrackedPoints(Sketch sketch, Pt upperTarget, Pt lowerTarget)
    {
        object[]? segments = sketch.GetSketchSegments() as object[];
        if (segments == null)
        {
            throw new InvalidOperationException("Active sketch has no segments.");
        }

        var candidates = new List<Pt>();
        foreach (object obj in segments)
        {
            if (obj is not SketchLine line)
            {
                continue;
            }

            AddUniquePoint(candidates, new Pt(line.GetStartPoint2().X * 1000.0, line.GetStartPoint2().Y * 1000.0));
            AddUniquePoint(candidates, new Pt(line.GetEndPoint2().X * 1000.0, line.GetEndPoint2().Y * 1000.0));
        }

        if (candidates.Count == 0)
        {
            throw new InvalidOperationException("No candidate sketch points found.");
        }

        Pt upper = FindNearest(candidates, upperTarget);
        Pt lower = FindNearest(candidates, lowerTarget, upper);
        return (upper, lower);
    }

    private static Pt FindNearest(List<Pt> points, Pt target, Pt? exclude = null)
    {
        Pt? best = null;
        double bestDist = double.MaxValue;

        foreach (Pt pt in points)
        {
            if (exclude.HasValue && Distance(pt, exclude.Value) < 1e-6)
            {
                continue;
            }

            double dist = Distance(pt, target);
            if (dist < bestDist)
            {
                bestDist = dist;
                best = pt;
            }
        }

        if (!best.HasValue)
        {
            throw new InvalidOperationException("Could not match tracked point.");
        }

        return best.Value;
    }

    private static void AddUniquePoint(List<Pt> points, Pt candidate)
    {
        if (points.Any(existing => Distance(existing, candidate) < 1e-6))
        {
            return;
        }

        points.Add(candidate);
    }

    private static Pt ComputeFiniteCenter(Pt u0, Pt u1, Pt l0, Pt l1)
    {
        if (Distance(u0, u1) < 1e-6 || Distance(l0, l1) < 1e-6)
        {
            throw new InvalidOperationException("Tracked points did not move enough to compute IC.");
        }

        Pt um = Midpoint(u0, u1);
        Pt lm = Midpoint(l0, l1);
        Pt un = PerpDirection(u0, u1);
        Pt ln = PerpDirection(l0, l1);

        return IntersectInfiniteLines(um, new Pt(um.X + un.X, um.Y + un.Y), lm, new Pt(lm.X + ln.X, lm.Y + ln.Y));
    }

    private static Pt Midpoint(Pt a, Pt b) => new((a.X + b.X) * 0.5, (a.Y + b.Y) * 0.5);

    private static Pt PerpDirection(Pt a, Pt b)
    {
        double dx = b.X - a.X;
        double dy = b.Y - a.Y;
        double len = Math.Sqrt((dx * dx) + (dy * dy));
        return new Pt(-dy / len, dx / len);
    }

    private static Pt IntersectInfiniteLines(Pt a1, Pt a2, Pt b1, Pt b2)
    {
        double x1 = a1.X;
        double y1 = a1.Y;
        double x2 = a2.X;
        double y2 = a2.Y;
        double x3 = b1.X;
        double y3 = b1.Y;
        double x4 = b2.X;
        double y4 = b2.Y;

        double denominator = ((x1 - x2) * (y3 - y4)) - ((y1 - y2) * (x3 - x4));
        if (Math.Abs(denominator) < 1e-9)
        {
            throw new InvalidOperationException("Perpendicular bisectors are nearly parallel.");
        }

        double determinantA = (x1 * y2) - (y1 * x2);
        double determinantB = (x3 * y4) - (y3 * x4);

        double x = ((determinantA * (x3 - x4)) - ((x1 - x2) * determinantB)) / denominator;
        double y = ((determinantA * (y3 - y4)) - ((y1 - y2) * determinantB)) / denominator;
        return new Pt(x, y);
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
