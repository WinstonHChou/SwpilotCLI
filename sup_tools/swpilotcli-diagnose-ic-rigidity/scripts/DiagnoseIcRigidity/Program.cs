using System.Runtime.InteropServices;
using Microsoft.VisualBasic;
using SolidWorks.Interop.sldworks;

namespace SolidWorksConsole;

internal static class Program
{
    private const string SketchName = "草圖1";
    private const string DimFullName = "D15@草圖1@點位2 text.Part";

    private static readonly Pt[] CandidateSeeds =
    [
        new(417.863, 220.565),
        new(227.016, 252.320),
        new(251.342, 249.402),
        new(431.880, 337.848),
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
        Sketch sketch = EnsureSketchEditing(swDoc);
        var dim = swDoc.Parameter(DimFullName) as Dimension
            ?? throw new InvalidOperationException($"Cannot find dimension {DimFullName}.");

        double originalMm = dim.SystemValue * 1000.0;

        try
        {
            SetDimensionMm(swDoc, dim, 68.0);
            var basePoints = GetUserPointsMm(sketch);
            PrintPoints("Pose 68.000", basePoints);

            Pt[] baseMatches = MatchSeeds(basePoints, CandidateSeeds);
            PrintDistances("Distances at 68.000", baseMatches);

            SetDimensionMm(swDoc, dim, 68.01);
            var movedPoints = GetUserPointsMm(sketch);
            PrintPoints("Pose 68.010", movedPoints);

            Pt[] movedMatches = MatchSeeds(movedPoints, baseMatches);
            PrintDistances("Distances at 68.010", movedMatches);

            Console.WriteLine("Candidate motions:");
            for (int i = 0; i < baseMatches.Length; i++)
            {
                Console.WriteLine(
                    $"  C{i + 1}: ({baseMatches[i].X:F3}, {baseMatches[i].Y:F3}) -> " +
                    $"({movedMatches[i].X:F3}, {movedMatches[i].Y:F3}), move={Distance(baseMatches[i], movedMatches[i]):F6} mm");
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
            }

            feat = feat.GetNextFeature() as Feature;
        }

        throw new InvalidOperationException($"Cannot enter {SketchName}.");
    }

    private static List<Pt> GetUserPointsMm(Sketch sketch)
    {
        object[]? pts = sketch.GetSketchPoints2() as object[];
        if (pts == null)
        {
            throw new InvalidOperationException("Active sketch has no points.");
        }

        return pts
            .OfType<SketchPoint>()
            .Where(p => p.Type == 1)
            .Select(p => new Pt(p.X * 1000.0, p.Y * 1000.0))
            .ToList();
    }

    private static Pt[] MatchSeeds(List<Pt> points, IReadOnlyList<Pt> seeds)
    {
        var matches = new Pt[seeds.Count];
        var used = new bool[points.Count];

        for (int s = 0; s < seeds.Count; s++)
        {
            int bestIndex = -1;
            double bestDist = double.MaxValue;
            for (int i = 0; i < points.Count; i++)
            {
                if (used[i])
                {
                    continue;
                }

                double dist = Distance(points[i], seeds[s]);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestIndex = i;
                }
            }

            if (bestIndex < 0)
            {
                throw new InvalidOperationException($"Could not match seed {s + 1}.");
            }

            used[bestIndex] = true;
            matches[s] = points[bestIndex];
        }

        return matches;
    }

    private static void PrintPoints(string title, List<Pt> points)
    {
        Console.WriteLine(title);
        for (int i = 0; i < points.Count; i++)
        {
            Console.WriteLine($"  P{i + 1}: ({points[i].X:F3}, {points[i].Y:F3})");
        }
    }

    private static void PrintDistances(string title, Pt[] pts)
    {
        Console.WriteLine(title);
        for (int i = 0; i < pts.Length; i++)
        {
            for (int j = i + 1; j < pts.Length; j++)
            {
                Console.WriteLine($"  C{i + 1}-C{j + 1}: {Distance(pts[i], pts[j]):F6} mm");
            }
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
