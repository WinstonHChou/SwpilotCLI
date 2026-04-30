using System.Runtime.InteropServices;
using Microsoft.VisualBasic;
using SolidWorks.Interop.sldworks;

namespace SolidWorksConsole;

internal static class Program
{
    private const string SourceSketchName = "草圖1";
    private const string TargetSketchName = "草圖5";
    private const string DimFullName = "D15@草圖1@點位2 text.Part";
    private const double DeltaMm = 0.01;
    private const double BisectorHalfLengthMm = 350.0;

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
        Sketch sourceSketch = EnsureSketchEditing(swDoc, SourceSketchName);
        var dim = swDoc.Parameter(DimFullName) as Dimension
            ?? throw new InvalidOperationException($"Cannot find dimension {DimFullName}.");
        double originalMm = dim.SystemValue * 1000.0;

        try
        {
            SetDimensionMm(swDoc, dim, 68.0);
            var basePoints = GetTrackedPoints(sourceSketch, UpperSeedMm, LowerSeedMm);

            SetDimensionMm(swDoc, dim, 68.0 + DeltaMm);
            var movedPoints = GetTrackedPoints(sourceSketch, basePoints.Upper, basePoints.Lower);

            SetDimensionMm(swDoc, dim, 68.0);

            Pt ic = ComputeFiniteCenter(basePoints.Upper, movedPoints.Upper, basePoints.Lower, movedPoints.Lower);
            Pt upperMid = Midpoint(basePoints.Upper, movedPoints.Upper);
            Pt lowerMid = Midpoint(basePoints.Lower, movedPoints.Lower);
            Pt upperMoveDir = Normalize(new Pt(movedPoints.Upper.X - basePoints.Upper.X, movedPoints.Upper.Y - basePoints.Upper.Y));
            Pt lowerMoveDir = Normalize(new Pt(movedPoints.Lower.X - basePoints.Lower.X, movedPoints.Lower.Y - basePoints.Lower.Y));
            Pt upperBisectorDir = new(-upperMoveDir.Y, upperMoveDir.X);
            Pt lowerBisectorDir = new(-lowerMoveDir.Y, lowerMoveDir.X);

            EnsureSketchEditing(swDoc, TargetSketchName);
            var sketchMgr = (SketchManager)swDoc.SketchManager;
            bool oldAddToDb = sketchMgr.AddToDB;
            bool oldDisplay = sketchMgr.DisplayWhenAdded;
            sketchMgr.AddToDB = true;
            sketchMgr.DisplayWhenAdded = false;

            var activeSketch = (Sketch)sketchMgr.ActiveSketch;
            MaybeCreateFixedPoint(swDoc, sketchMgr, activeSketch, ic);

            CreateCenterLineMm(sketchMgr, upperMid, upperBisectorDir, BisectorHalfLengthMm);
            CreateCenterLineMm(sketchMgr, lowerMid, lowerBisectorDir, BisectorHalfLengthMm);

            sketchMgr.AddToDB = oldAddToDb;
            sketchMgr.DisplayWhenAdded = oldDisplay;

            Console.WriteLine($"Upper midpoint: ({upperMid.X:F3}, {upperMid.Y:F3})");
            Console.WriteLine($"Lower midpoint: ({lowerMid.X:F3}, {lowerMid.Y:F3})");
            Console.WriteLine($"IC @ 68: ({ic.X:F3}, {ic.Y:F3})");
        }
        finally
        {
            SetDimensionMm(swDoc, dim, originalMm);
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

    private static void MaybeCreateFixedPoint(ModelDoc2 swDoc, SketchManager sketchMgr, Sketch sketch, Pt pointMm)
    {
        if (HasNearbySketchPoint(sketch, pointMm, 0.001))
        {
            return;
        }

        var point = sketchMgr.CreatePoint(pointMm.X / 1000.0, pointMm.Y / 1000.0, 0.0);
        if (point == null)
        {
            return;
        }

        if (!point.Select4(false, null))
        {
            throw new InvalidOperationException("Failed to select created sketch point.");
        }

        swDoc.SketchAddConstraints("sgFIXED");
        swDoc.ClearSelection2(true);
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

    private static void CreateCenterLineMm(SketchManager sketchMgr, Pt centerMm, Pt directionUnit, double halfLengthMm)
    {
        var start = new Pt(
            centerMm.X - (directionUnit.X * halfLengthMm),
            centerMm.Y - (directionUnit.Y * halfLengthMm));

        var end = new Pt(
            centerMm.X + (directionUnit.X * halfLengthMm),
            centerMm.Y + (directionUnit.Y * halfLengthMm));

        if (sketchMgr.CreateCenterLine(
            start.X / 1000.0,
            start.Y / 1000.0,
            0.0,
            end.X / 1000.0,
            end.Y / 1000.0,
            0.0) == null)
        {
            throw new InvalidOperationException("Failed to create centerline.");
        }
    }

    private static void SetDimensionMm(ModelDoc2 swDoc, Dimension dim, double valueMm)
    {
        dim.SystemValue = valueMm / 1000.0;
        if (!swDoc.EditRebuild3())
        {
            throw new InvalidOperationException($"Rebuild failed after setting dimension to {valueMm:F3} mm.");
        }
    }

    private static Pt ComputeFiniteCenter(Pt u0, Pt u1, Pt l0, Pt l1)
    {
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

    private static Pt Normalize(Pt vector)
    {
        double len = Math.Sqrt((vector.X * vector.X) + (vector.Y * vector.Y));
        if (len < 1e-9)
        {
            throw new InvalidOperationException("Zero-length vector.");
        }

        return new Pt(vector.X / len, vector.Y / len);
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
