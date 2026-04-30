using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
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

            ExecuteTask(swDoc, swApp);
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

    private static void ExecuteTask(ModelDoc2 swDoc, SldWorks swApp)
    {
        if (swDoc.GetType() != (int)swDocumentTypes_e.swDocASSEMBLY)
            throw new InvalidOperationException("Active document is not an assembly.");

        var assemblyDoc = swDoc as AssemblyDoc
            ?? throw new InvalidOperationException("Failed to cast active document to AssemblyDoc.");

        var componentsRaw = assemblyDoc.GetComponents(false);
        var uniqueParts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (componentsRaw != null)
        {
            var items = componentsRaw as object[] ?? new[] { componentsRaw };
            foreach (var item in items)
            {
                if (item is not Component2 comp) continue;

                var partPath = comp.GetPathName();
                if (string.IsNullOrWhiteSpace(partPath)) continue;
                if (!string.Equals(Path.GetExtension(partPath), ".sldprt", StringComparison.OrdinalIgnoreCase)) continue;

                try { partPath = Path.GetFullPath(partPath); } catch { }
                uniqueParts.Add(partPath);
            }
        }

        var result = uniqueParts.OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList();

        Console.WriteLine($"Assembly: {swDoc.GetPathName() ?? swDoc.GetTitle()}");
        Console.WriteLine($"Unique parts: {result.Count}");
        Console.WriteLine();
        Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static SldWorks? ConnectToSolidWorks()
    {
        try { return Interaction.GetObject(string.Empty, "SldWorks.Application") as SldWorks; }
        catch (COMException) { }

        var type = Type.GetTypeFromProgID("SldWorks.Application");
        return type == null ? null : (SldWorks?)Activator.CreateInstance(type);
    }
}
