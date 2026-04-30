using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.VisualBasic;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace OpenDoc;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        SldWorks? swApp = null;

        try
        {
            if (args.Length == 0 || string.IsNullOrWhiteSpace(args[0]))
            {
                Console.Error.WriteLine("Usage: OpenDoc <file_path>");
                Console.Error.WriteLine("Supported: .sldprt, .sldasm, .slddrw");
                return 3;
            }

            string filePath = args[0];

            if (!File.Exists(filePath))
            {
                Console.Error.WriteLine($"File not found: {filePath}");
                return 1;
            }

            swApp = ConnectToSolidWorks();
            if (swApp == null)
            {
                Console.Error.WriteLine("Unable to attach to SolidWorks.");
                return 2;
            }

            swApp.Visible = true;

            int docType = Path.GetExtension(filePath).ToLower() switch
            {
                ".sldprt" => (int)swDocumentTypes_e.swDocPART,
                ".sldasm" => (int)swDocumentTypes_e.swDocASSEMBLY,
                ".slddrw" => (int)swDocumentTypes_e.swDocDRAWING,
                _ => throw new ArgumentException($"Unsupported file type: {Path.GetExtension(filePath)}")
            };

            int errors = 0;
            int warnings = 0;
            ModelDoc2 openedDoc = (ModelDoc2)swApp.OpenDoc6(
                filePath,
                docType,
                (int)swOpenDocOptions_e.swOpenDocOptions_Silent,
                "",
                ref errors,
                ref warnings
            );

            if (openedDoc == null)
            {
                Console.Error.WriteLine($"開啟失敗，錯誤碼: {errors}");
                return 4;
            }

            swApp.ActivateDoc3(filePath, true, (int)swRebuildOnActivation_e.swUserDecision, ref errors);
            Console.WriteLine($"成功開啟: {filePath}");
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
            if (swApp != null) Marshal.ReleaseComObject(swApp);
        }
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
