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

// Tool: swpilotcli-feature-extrude-boss
// Must be called while inside sketch editing mode (instead of sketch-exit).
// Args: <depth_mm>
var cmdArgs = System.Environment.GetCommandLineArgs().Skip(1).ToArray();
if (cmdArgs.Length == 0)
{
    Console.Error.WriteLine("ERROR: Usage: extrude-boss <depth_mm>");
    return;
}

var sketchMgr = (SketchManager)swDoc.SketchManager;
if (sketchMgr.ActiveSketch == null)
{
    Console.Error.WriteLine("ERROR: No active sketch. Must be called while in sketch editing mode.");
    return;
}

double depthMm = double.Parse(cmdArgs[0]);
double depth   = depthMm / 1000.0; // meters

var feat = (Feature)swDoc.FeatureManager.FeatureExtrusion3(
    true,   // Sd: single direction
    false,  // Flip
    false,  // Dir: along sketch normal (boss direction)
    (int)swEndConditions_e.swEndCondBlind,
    0,
    depth,
    0.0,
    false, false, false, false,
    0.0, 0.0,
    false, false,
    false, false,
    true,   // Merge
    false,  // UseFeatScope
    true,   // UseAutoSelect
    (int)swStartConditions_e.swStartSketchPlane,
    0.0,
    false
);

if (feat == null)
{
    Console.Error.WriteLine("ERROR: Boss-Extrude failed. Ensure a valid closed sketch profile is active.");
    return;
}

swDoc.EditRebuild3();
swDoc.GraphicsRedraw2();
Console.WriteLine($"OK: Boss-Extrude '{feat.Name}' created. Depth = {depthMm}mm.");

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
