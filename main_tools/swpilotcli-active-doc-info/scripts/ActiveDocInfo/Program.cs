using System;
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
        var model = swApp.IActiveDoc2;
        if (model == null)
        {
            Console.WriteLine("No active SolidWorks document.");
            return;
        }

        var docType = model.GetType();
        string docTypeName = docType switch
        {
            (int)swDocumentTypes_e.swDocPART => ".sldprt (Part)",
            (int)swDocumentTypes_e.swDocASSEMBLY => ".sldasm (Assembly)",
            (int)swDocumentTypes_e.swDocDRAWING => ".slddrw (Drawing)",
            _ => $"Unknown ({docType})"
        };

        Console.WriteLine($"Title: {model.GetTitle()}");
        Console.WriteLine($"Type: {docTypeName}");

        var path = model.GetPathName();
        Console.WriteLine($"Path: {(string.IsNullOrWhiteSpace(path) ? "<unsaved document>" : path)}");
    }

    private static SldWorks? ConnectToSolidWorks()
    {
        try
        {
            return Interaction.GetObject(string.Empty, "SldWorks.Application") as SldWorks;
        }
        catch (COMException)
        {
        }

        var type = Type.GetTypeFromProgID("SldWorks.Application");
        return type == null ? null : (SldWorks?)Activator.CreateInstance(type);
    }
}
