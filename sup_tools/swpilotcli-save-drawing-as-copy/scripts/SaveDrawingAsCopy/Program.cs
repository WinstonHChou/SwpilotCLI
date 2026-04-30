// Usage: -- <sourcePath> <outputPath>
// Saves a COPY of the drawing to outputPath without changing the active document's path.

using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.VisualBasic;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace SaveDrawingAsCopy;

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
                Console.Error.WriteLine("用法: -- <來源路徑> <輸出路徑>");
                return 1;
            }

            string sourcePath = args[0];
            string outputPath = args[1];

            swApp = ConnectToSolidWorks();
            if (swApp == null)
            {
                Console.Error.WriteLine("無法連接 SolidWorks。");
                return 2;
            }

            swApp.Visible = true;

            // Open source file (silently; reuses existing if already open)
            int errors = 0, warnings = 0;
            swDoc = (ModelDoc2)swApp.OpenDoc6(
                sourcePath,
                (int)swDocumentTypes_e.swDocDRAWING,
                (int)swOpenDocOptions_e.swOpenDocOptions_Silent,
                "", ref errors, ref warnings);

            if (swDoc == null)
            {
                Console.Error.WriteLine($"無法開啟: {sourcePath} (errors={errors})");
                return 4;
            }

            Console.WriteLine($"已開啟  : {Path.GetFileName(sourcePath)}");
            Console.WriteLine($"輸出    : {outputPath}");

            // SaveAs3 with Copy flag — does NOT change the document's path
            var swExt = (ModelDocExtension)swDoc.Extension;
            int saveErrors = 0, saveWarnings = 0;

            bool ok = swExt.SaveAs3(
                outputPath,
                0,
                (int)swSaveAsOptions_e.swSaveAsOptions_Silent |
                (int)swSaveAsOptions_e.swSaveAsOptions_Copy,
                null, null,
                ref saveErrors, ref saveWarnings);

            if (ok)
            {
                var fi = new FileInfo(outputPath);
                Console.WriteLine($"✓ 存檔成功  大小: {fi.Length / 1024} KB");
            }
            else
            {
                Console.Error.WriteLine($"✗ 存檔失敗 (errors={saveErrors}, warnings={saveWarnings})");
                return 5;
            }

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

    private static SldWorks? ConnectToSolidWorks()
    {
        try
        {
            return Interaction.GetObject(string.Empty, "SldWorks.Application") as SldWorks;
        }
        catch (COMException) { }

        var type = Type.GetTypeFromProgID("SldWorks.Application");
        return type == null ? null : (SldWorks?)Activator.CreateInstance(type);
    }
}
