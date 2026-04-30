using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.VisualBasic;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace SolidWorksConsole;

internal static class Program
{
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

            Console.WriteLine($"Active document: {swDoc.GetPathName() ?? swDoc.GetTitle()}");

            // === Execute task ===
            ExecuteTask(swDoc, swApp);
            // === End of task ===

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

    // ============================================================
    // AI: Only modify the code inside this method.
    // swDoc = active ModelDoc2, swApp = SldWorks application.
    // ============================================================
    private static void ExecuteTask(ModelDoc2 swDoc, SldWorks swApp)
    {

// Tool: swpilotcli-feature-hole-wizard-straight-tap
// Args: <size> <depth_mm|through_all> <face_x,face_y,face_z> <x1,y1;x2,y2;...>
//   face_x,face_y,face_z = world coordinates (mm) of any point on the target face
//   x1,y1;...            = hole positions (mm) in face sketch space
// Example: M6 12 20,10,5 -10.6,-11.65;10.6,-11.65;-10.6,11.65;10.6,11.65
var cmdArgs = System.Environment.GetCommandLineArgs().Skip(1).ToArray();
if (cmdArgs.Length < 4)
{
    Console.Error.WriteLine("ERROR: Usage: hole-wizard-straight-tap <size> <depth_mm|through_all> <face_x,face_y,face_z> <x1,y1;x2,y2;...>");
    Console.Error.WriteLine("  Example: M6 12 20,10,5 -10.6,-11.65;10.6,-11.65;-10.6,11.65;10.6,11.65");
    return;
}

string size      = cmdArgs[0];
string depthArg  = cmdArgs[1];
bool   throughAll = depthArg.Equals("through_all", StringComparison.OrdinalIgnoreCase);
double depth     = throughAll ? 0.0 : double.Parse(depthArg) / 1000.0;
short  endType   = (short)(throughAll
    ? (int)swEndConditions_e.swEndCondThroughAll
    : (int)swEndConditions_e.swEndCondBlind);

// Parse face world coordinates (mm → m)
var faceParts = cmdArgs[2].Split(',');
double faceX = double.Parse(faceParts[0].Trim()) / 1000.0;
double faceY = double.Parse(faceParts[1].Trim()) / 1000.0;
double faceZ = double.Parse(faceParts[2].Trim()) / 1000.0;

var positions = cmdArgs[3].Split(';')
    .Select(p => p.Trim().Split(','))
    .Select(parts => (X: double.Parse(parts[0].Trim()) / 1000.0,
                      Y: double.Parse(parts[1].Trim()) / 1000.0))
    .ToList();

if (positions.Count == 0)
{
    Console.Error.WriteLine("ERROR: No positions specified.");
    return;
}

var selMgr    = (SelectionMgr)swDoc.SelectionManager;
var sketchMgr = (SketchManager)swDoc.SketchManager;
var ext       = swDoc.Extension;
var featMgr   = swDoc.FeatureManager;

// Tap drill diameter for common metric sizes
double tapDrill = size.ToUpper() switch
{
    "M3"  => 0.0025,
    "M4"  => 0.0033,
    "M5"  => 0.0042,
    "M6"  => 0.0050,
    "M8"  => 0.0067,
    "M10" => 0.0085,
    "M12" => 0.0102,
    _     => 0.0
};

// ── Step 1: select face ──
swDoc.ClearSelection2(true);
bool faceOk = ext.SelectByID2("", "FACE", faceX, faceY, faceZ, false, 0, null, 0);
if (!faceOk)
{
    Console.Error.WriteLine($"ERROR: Cannot select face at ({faceX*1000:F3},{faceY*1000:F3},{faceZ*1000:F3})mm.");
    return;
}
Console.WriteLine($"Face selected at: ({faceX*1000:F1},{faceY*1000:F1},{faceZ*1000:F1})mm");

// ── Step 2: HoleWizard5 with face only → creates feature + internal position sketch ──
var feat = (Feature)featMgr.HoleWizard5(
    4,          // GenericHoleType: swWzdTap
    8,          // StandardIndex:   swStandardISO
    147,        // FastenerTypeIndex: swStandardISOTappedHole
    size,
    endType,
    tapDrill,
    depth,
    0.0,
    -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
    "", false, true, true, true, false, false
);

if (feat == null)
{
    Console.Error.WriteLine("ERROR: HoleWizard failed. Verify size, standard index, and face selection.");
    return;
}
Console.WriteLine($"HoleWizard feature '{feat.Name}' created.");

// ── Step 3: get the internal position sketch sub-feature ──
var subFeat = (Feature?)feat.GetFirstSubFeature();
if (subFeat == null)
{
    Console.Error.WriteLine("ERROR: Cannot find position sketch sub-feature.");
    return;
}
Console.WriteLine($"Position sketch sub-feature: '{subFeat.Name}' ({subFeat.GetTypeName2()})");

// ── Step 4: enter edit mode for the position sketch ──
swDoc.ClearSelection2(true);
ext.SelectByID2(subFeat.Name, "SKETCH", 0, 0, 0, false, 0, null, 0);
swDoc.EditSketch();

if (sketchMgr.ActiveSketch == null)
{
    Console.Error.WriteLine("ERROR: Could not enter position sketch edit mode.");
    return;
}
Console.WriteLine("Entered position sketch edit mode.");

// ── Step 5: delete the default point HoleWizard placed ──
var sk = (Sketch)sketchMgr.ActiveSketch;
var existingPts = sk.GetSketchPoints2() as object[];
if (existingPts?.Length > 0)
{
    swDoc.ClearSelection2(true);
    bool first = true;
    foreach (SketchPoint pt in existingPts.OfType<SketchPoint>())
    {
        pt.Select4(!first, null);
        first = false;
    }
    swDoc.Extension.DeleteSelection2((int)swDeleteSelectionOptions_e.swDelete_Absorbed);
    Console.WriteLine($"Deleted {existingPts.Length} default point(s).");
}

// ── Step 6: add correct hole position points ──
for (int i = 0; i < positions.Count; i++)
{
    sketchMgr.CreatePoint(positions[i].X, positions[i].Y, 0);
    Console.WriteLine($"  Point{i+1}: ({positions[i].X*1000:F2},{positions[i].Y*1000:F2})mm");
}

// ── Step 7: exit position sketch ──
sketchMgr.InsertSketch(true);

swDoc.EditRebuild3();
swDoc.GraphicsRedraw2();
Console.WriteLine($"OK: '{feat.Name}' — {positions.Count}x {size} tap, depth={depthArg}.");

    }

    private static SldWorks? ConnectToSolidWorks()
    {
        try
        {
            return Interaction.GetObject(string.Empty, "SldWorks.Application") as SldWorks;
        }
        catch (COMException)
        {
            // Fall through to creating a new instance.
        }

        var type = Type.GetTypeFromProgID("SldWorks.Application");
        return type == null ? null : (SldWorks?)Activator.CreateInstance(type);
    }
}
