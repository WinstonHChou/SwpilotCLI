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

// Tool: swpilotcli-sketch-on-face
// Args: [optional] "PlaneName"       — select a named plane (e.g. 前基準面)
//                  "x,y,z"           — select a face by world coordinates in mm (e.g. 25,10,-20)
//                  (none)            — use pre-selected face/plane
var cmdArgs = System.Environment.GetCommandLineArgs().Skip(1).ToArray();
var sketchMgr = (SketchManager)swDoc.SketchManager;
var ext       = swDoc.Extension;
var selMgr    = (SelectionMgr)swDoc.SelectionManager;

if (cmdArgs.Length > 0 && !string.IsNullOrWhiteSpace(cmdArgs[0]))
{
    string arg = cmdArgs[0];
    swDoc.ClearSelection2(true);

    if (arg.Contains(','))
    {
        // Face world coordinates: "x,y,z" in mm
        var parts = arg.Split(',');
        double fx = double.Parse(parts[0].Trim()) / 1000.0;
        double fy = double.Parse(parts[1].Trim()) / 1000.0;
        double fz = double.Parse(parts[2].Trim()) / 1000.0;
        bool ok = ext.SelectByID2("", "FACE", fx, fy, fz, false, 0, null, 0);
        if (!ok)
        {
            Console.Error.WriteLine($"ERROR: Cannot select face at ({arg})mm.");
            return;
        }
        Console.WriteLine($"Selected face at: ({arg})mm");
    }
    else
    {
        // Named plane
        bool ok = ext.SelectByID2(arg, "PLANE", 0, 0, 0, false, 0, null, 0);
        if (!ok)
        {
            Console.Error.WriteLine($"ERROR: Cannot select plane '{arg}'.");
            return;
        }
        Console.WriteLine($"Selected plane: {arg}");
    }
}
else
{
    int count = selMgr.GetSelectedObjectCount2(-1);
    if (count == 0)
    {
        Console.Error.WriteLine("ERROR: No face/plane pre-selected and no argument given.");
        return;
    }
    Console.WriteLine("Using pre-selected entity.");
}

sketchMgr.InsertSketch(false);

var activeSketch = (Sketch)sketchMgr.ActiveSketch;
if (activeSketch == null)
{
    Console.Error.WriteLine("ERROR: Failed to enter sketch mode.");
    return;
}

Console.WriteLine("OK: Entered sketch mode. Run sketch-draw, then extrude-boss or extrude-cut.");

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
