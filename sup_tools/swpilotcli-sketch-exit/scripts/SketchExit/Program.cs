using System;
using System.Runtime.InteropServices;
using Microsoft.VisualBasic;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        SldWorks?  swApp = null;
        ModelDoc2? swDoc = null;
        try
        {
            swApp = Connect();
            if (swApp == null) { Console.Error.WriteLine("ERROR: Cannot connect to SolidWorks."); return 2; }

            swDoc = (ModelDoc2)swApp.IActiveDoc2;
            if (swDoc == null) { Console.Error.WriteLine("ERROR: No active document."); return 3; }

            var sketchMgr   = (SketchManager)swDoc.SketchManager;
            var activeSketch = (Sketch)sketchMgr.ActiveSketch;
            if (activeSketch == null)
            { Console.Error.WriteLine("ERROR: No active sketch."); return 4; }

            swDoc.EditRebuild3();
            swDoc.GraphicsRedraw2();

            int status = activeSketch.GetConstrainedStatus();
            string statusText = status switch
            {
                (int)swConstrainedStatus_e.swFullyConstrained  => "Fully Defined",
                (int)swConstrainedStatus_e.swUnderConstrained  => "Under-Constrained",
                (int)swConstrainedStatus_e.swOverConstrained   => "Over-Constrained",
                _ => $"Unknown ({status})"
            };

            Console.WriteLine($"Sketch status: {statusText}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"ERROR: {ex.Message}");
            return 1;
        }
        finally
        {
            if (swDoc != null) Marshal.ReleaseComObject(swDoc);
            if (swApp != null) Marshal.ReleaseComObject(swApp);
        }
    }

    private static SldWorks? Connect()
    {
        try { return Interaction.GetObject(string.Empty, "SldWorks.Application") as SldWorks; }
        catch (COMException) { }
        var t = Type.GetTypeFromProgID("SldWorks.Application");
        return t == null ? null : (SldWorks?)Activator.CreateInstance(t);
    }
}
