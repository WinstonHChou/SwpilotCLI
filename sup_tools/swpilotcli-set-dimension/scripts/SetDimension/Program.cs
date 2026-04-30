using System;
using System.Globalization;
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
            if (args.Length < 2)
            {
                Console.Error.WriteLine("Usage: <dimName@owner> <value_mm>");
                return 1;
            }

            string dimFullName = args[0];
            if (!double.TryParse(args[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double valueMm))
            {
                Console.Error.WriteLine($"ERROR: '{args[1]}' is not a valid number.");
                return 1;
            }

            swApp = Interaction.GetObject(string.Empty, "SldWorks.Application") as SldWorks;
            if (swApp == null) { Console.Error.WriteLine("Unable to attach to SolidWorks."); return 2; }
            swApp.Visible = true;
            swDoc = swApp.IActiveDoc2;
            if (swDoc == null) { Console.Error.WriteLine("No active document."); return 3; }

            var dim = swDoc.Parameter(dimFullName) as Dimension;
            if (dim == null)
            {
                Console.Error.WriteLine($"ERROR: Dimension '{dimFullName}' not found.");
                return 4;
            }

            int rc = dim.SetSystemValue3(
                valueMm / 1000.0,
                (int)swSetValueInConfiguration_e.swSetValue_InThisConfiguration,
                null);

            swDoc.EditRebuild3();
            swDoc.GraphicsRedraw2();

            Console.WriteLine($"OK: {dimFullName} = {valueMm} mm (rc={rc})");
            return 0;
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
