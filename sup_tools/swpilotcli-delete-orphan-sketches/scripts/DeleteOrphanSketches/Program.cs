using System;
using System.Runtime.InteropServices;
using Microsoft.VisualBasic;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace DeleteOrphanSketches;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        SldWorks? swApp = null;
        ModelDoc2? swDoc = null;
        try
        {
            swApp = Interaction.GetObject(string.Empty, "SldWorks.Application") as SldWorks;
            if (swApp == null) { Console.Error.WriteLine("Unable to attach to SolidWorks."); return 2; }

            swDoc = swApp.IActiveDoc2;
            if (swDoc == null) { Console.Error.WriteLine("No active document."); return 3; }

            var sketchMgr = (SketchManager)swDoc.SketchManager;
            if (sketchMgr.ActiveSketch != null)
            {
                sketchMgr.InsertSketch(true);
                Console.WriteLine("Exited active sketch first.");
            }

            int deleted = 0;
            var f = (Feature)swDoc.FirstFeature();
            while (f != null)
            {
                var next = (Feature)f.GetNextFeature();
                string typeName = f.GetTypeName2();
                if (typeName == "ProfileFeature")
                {
                    Console.Write($"Deleting orphan sketch '{f.Name}'... ");
                    swDoc.ClearSelection2(true);
                    f.Select2(false, -1);
                    bool ok = swDoc.Extension.DeleteSelection2(
                        (int)swDeleteSelectionOptions_e.swDelete_Absorbed);
                    Console.WriteLine(ok ? "OK" : "FAILED");
                    if (ok) deleted++;
                }
                f = next;
            }

            swDoc.EditRebuild3();
            swDoc.GraphicsRedraw2();
            Console.WriteLine($"Done. Deleted {deleted} orphan sketch(es).");
            return 0;
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
}
