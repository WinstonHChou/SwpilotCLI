using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.VisualBasic;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace ChamferCircularHole;

internal static class Program
{
    private const double RadiusToleranceM = 0.00005; // 0.05 mm

    [STAThread]
    private static int Main(string[] args)
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

            swApp.Visible = true;
            swDoc = swApp.IActiveDoc2;
            if (swDoc == null)
            {
                Console.Error.WriteLine("No active document in SolidWorks.");
                return 3;
            }

            return ExecuteTask(swDoc, args);
        }
        catch (COMException ex)
        {
            Console.Error.WriteLine($"COM error: 0x{ex.ErrorCode:X8} - {ex.Message}");
            return 1;
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

    private static int ExecuteTask(ModelDoc2 swDoc, string[] args)
    {
        if (!TryReadArgs(args, out var options))
            return 4;

        if (swDoc.GetType() != (int)swDocumentTypes_e.swDocPART)
        {
            Console.Error.WriteLine("ERROR: Active document must be a part.");
            return 5;
        }

        var candidates = FindMatchingCircularEdges(swDoc, options.TargetDiameterMm);
        if (candidates.Count == 0)
        {
            Console.Error.WriteLine($"ERROR: No full circular edges found with diameter {options.TargetDiameterMm}mm.");
            return 6;
        }

        if (options.NearestPointMm != null)
        {
            candidates = candidates
                .OrderBy(edge => DistanceSquaredMm(edge.CenterMm, options.NearestPointMm.Value))
                .Take(1)
                .ToList();
        }

        swDoc.ClearSelection2(true);
        var selected = new List<CircularEdge>();
        foreach (var candidate in candidates)
        {
            var entity = (Entity)candidate.Edge;
            if (!entity.Select4(true, null))
                continue;

            selected.Add(candidate);
            Console.WriteLine(
                $"Selected circular edge: center=({candidate.CenterMm.X:F3},{candidate.CenterMm.Y:F3},{candidate.CenterMm.Z:F3})mm radius={candidate.RadiusMm:F3}mm");
        }

        if (selected.Count == 0)
        {
            Console.Error.WriteLine("ERROR: Matching circular edges were found, but none could be selected.");
            return 7;
        }

        swDoc.FeatureChamferType(
            (short)swChamferType_e.swChamferAngleDistance,
            options.ChamferMm / 1000.0,
            options.AngleDeg * Math.PI / 180.0,
            false,
            0.0,
            0.0,
            0.0,
            0.0);

        swDoc.EditRebuild3();
        swDoc.GraphicsRedraw2();

        var feature = (Feature)swDoc.FeatureByPositionReverse(0);
        string featureName = feature?.Name ?? "(unknown)";
        Console.WriteLine($"OK: Chamfer applied to {selected.Count} circular edge(s). Feature={featureName}, C={options.ChamferMm}mm, angle={options.AngleDeg}deg.");
        return 0;
    }

    private static List<CircularEdge> FindMatchingCircularEdges(ModelDoc2 swDoc, double targetDiameterMm)
    {
        var part = (PartDoc)swDoc;
        var bodiesObj = part.GetBodies2((int)swBodyType_e.swSolidBody, true);
        var result = new List<CircularEdge>();
        if (bodiesObj is not object[] bodies)
            return result;

        double targetRadiusM = targetDiameterMm / 2000.0;

        foreach (var bodyObj in bodies)
        {
            var body = (Body2)bodyObj;
            if (body.GetEdges() is not object[] edges)
                continue;

            foreach (var edgeObj in edges)
            {
                var edge = (Edge)edgeObj;
                var curve = (Curve)edge.GetCurve();
                if (curve == null || !curve.IsCircle())
                    continue;

                if (curve.CircleParams is not double[] circleParams || circleParams.Length < 7)
                    continue;

                double radiusM = circleParams[6];
                if (Math.Abs(radiusM - targetRadiusM) > RadiusToleranceM)
                    continue;

                if (!IsFullCircle(edge))
                    continue;

                result.Add(new CircularEdge(
                    edge,
                    new Point3(circleParams[0] * 1000.0, circleParams[1] * 1000.0, circleParams[2] * 1000.0),
                    radiusM * 1000.0));
            }
        }

        return result;
    }

    private static bool IsFullCircle(Edge edge)
    {
        var edgeParamsObj = edge.GetCurveParams2();
        if (edgeParamsObj is not double[] edgeParams || edgeParams.Length < 8)
            return true;

        double span = Math.Abs(edgeParams[7] - edgeParams[6]);
        return span >= Math.PI * 1.5;
    }

    private static bool TryReadArgs(string[] args, out Options options)
    {
        options = new Options(10.0, 0.5, 45.0, null);
        if (args.Length > 4)
        {
            PrintUsage("Too many arguments.");
            return false;
        }

        double diameterMm = 10.0;
        double chamferMm = 0.5;
        double angleDeg = 45.0;
        Point3? nearestPointMm = null;

        if (args.Length > 0 && !TryParsePositive(args[0], out diameterMm))
        {
            PrintUsage("diameter_mm must be a positive number.");
            return false;
        }

        if (args.Length > 1 && !TryParsePositive(args[1], out chamferMm))
        {
            PrintUsage("chamfer_mm must be a positive number.");
            return false;
        }

        if (args.Length > 2 && (!TryParsePositive(args[2], out angleDeg) || angleDeg >= 180.0))
        {
            PrintUsage("angle_deg must be greater than 0 and less than 180.");
            return false;
        }

        if (args.Length > 3 && !TryParsePoint(args[3], out nearestPointMm))
        {
            PrintUsage("nearest point must use x,y,z in millimetres.");
            return false;
        }

        options = new Options(diameterMm, chamferMm, angleDeg, nearestPointMm);
        return true;
    }

    private static bool TryParsePositive(string text, out double value)
    {
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && value > 0.0;
    }

    private static bool TryParsePoint(string text, out Point3? point)
    {
        point = null;
        var parts = text.Split(',');
        if (parts.Length != 3)
            return false;

        if (!double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double x) ||
            !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double y) ||
            !double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double z))
        {
            return false;
        }

        point = new Point3(x, y, z);
        return true;
    }

    private static void PrintUsage(string error)
    {
        Console.Error.WriteLine($"ERROR: {error}");
        Console.Error.WriteLine("Usage: chamfer-circular-hole [diameter_mm] [chamfer_mm] [angle_deg] [nearest_x,nearest_y,nearest_z]");
    }

    private static double DistanceSquaredMm(Point3 a, Point3 b)
    {
        double dx = a.X - b.X;
        double dy = a.Y - b.Y;
        double dz = a.Z - b.Z;
        return dx * dx + dy * dy + dz * dz;
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

    private readonly record struct Options(double TargetDiameterMm, double ChamferMm, double AngleDeg, Point3? NearestPointMm);

    private readonly record struct Point3(double X, double Y, double Z);

    private sealed record CircularEdge(Edge Edge, Point3 CenterMm, double RadiusMm);
}
