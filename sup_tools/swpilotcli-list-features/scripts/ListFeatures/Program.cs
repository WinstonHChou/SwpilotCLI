using System;
using System.Runtime.InteropServices;
using Microsoft.VisualBasic;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace ListFeatures;

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

            int limit = int.MaxValue;
            if (args.Length > 0 && int.TryParse(args[0], out int parsed) && parsed > 0)
                limit = parsed;

            var feat = (Feature)swDoc.FirstFeature();
            int n = 0;
            while (feat != null && n < limit)
            {
                Console.WriteLine($"[{feat.GetTypeName2()}] {feat.Name}");
                feat = (Feature)feat.GetNextFeature();
                n++;
            }
            Console.WriteLine($"Total listed: {n} feature(s).");
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
