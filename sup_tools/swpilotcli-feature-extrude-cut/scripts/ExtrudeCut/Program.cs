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

// Tool: swpilotcli-feature-extrude-cut
// Must be called while inside sketch editing mode (instead of sketch-exit).
// Args: <depth_mm | through_all>
var cmdArgs = System.Environment.GetCommandLineArgs().Skip(1).ToArray();
if (cmdArgs.Length == 0)
{
    Console.Error.WriteLine("ERROR: Usage: extrude-cut <depth_mm | through_all>");
    return;
}

var sketchMgr = (SketchManager)swDoc.SketchManager;
if (sketchMgr.ActiveSketch == null)
{
    Console.Error.WriteLine("ERROR: No active sketch. Must be called while in sketch editing mode.");
    return;
}

bool throughAll = cmdArgs[0].Equals("through_all", StringComparison.OrdinalIgnoreCase);
double depth    = throughAll ? 0.0 : double.Parse(cmdArgs[0]) / 1000.0;
int    endCond  = throughAll
    ? (int)swEndConditions_e.swEndCondThroughAll
    : (int)swEndConditions_e.swEndCondBlind;

swDoc.FeatureCut3(
    true,   // Sd: single direction
    false,  // Flip
    false,  // Dir
    endCond,
    0,
    depth,
    0.0,
    false, false, false, false,
    0.0, 0.0,
    false, false,
    0
);

// Find the last feature created (the cut)
Feature? lastFeat = null;
var f = (Feature)swDoc.FirstFeature();
while (f != null) { lastFeat = f; f = (Feature)f.GetNextFeature(); }

if (lastFeat == null)
{
    Console.Error.WriteLine("ERROR: Cut-Extrude failed. Ensure a valid closed sketch profile is active.");
    return;
}

swDoc.EditRebuild3();
swDoc.GraphicsRedraw2();
string depthStr = throughAll ? "Through All" : $"{cmdArgs[0]}mm";
Console.WriteLine($"OK: Cut-Extrude '{lastFeat.Name}' created. Depth = {depthStr}.");

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
