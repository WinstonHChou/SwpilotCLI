using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.VisualBasic;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace SketchDraw;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        SldWorks?   swApp = null;
        ModelDoc2?  swDoc = null;
        try
        {
            string json = args.Length > 0
                ? File.ReadAllText(args[0])
                : Console.In.ReadToEnd();

            var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var plan = JsonSerializer.Deserialize<SketchPlan>(json, opts);
            if (plan == null) { Console.Error.WriteLine("ERROR: Invalid JSON input."); return 1; }

            swApp = Connect();
            if (swApp == null) { Console.Error.WriteLine("ERROR: Cannot connect to SolidWorks."); return 2; }

            swDoc = (ModelDoc2)swApp.IActiveDoc2;
            if (swDoc == null) { Console.Error.WriteLine("ERROR: No active document."); return 3; }

            var sketchMgr  = (SketchManager)swDoc.SketchManager;
            var activeSketch = (Sketch)sketchMgr.ActiveSketch;
            if (activeSketch == null)
            { Console.Error.WriteLine("ERROR: No active sketch. Enter a sketch first."); return 4; }

            Console.WriteLine("Drawing sketch...");
            new SketchDrawer(swDoc, swApp).Draw(plan);
            swDoc.GraphicsRedraw2();
            Console.WriteLine("OK: Sketch drawn. Call swpilotcli-sketch-exit to commit.");
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
