using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.VisualBasic;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace SaveDrawingAs;

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

            ExecuteTask(swDoc, swApp, args);

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

    private static void ExecuteTask(ModelDoc2 swDoc, SldWorks swApp, string[] args)
    {
        if (swDoc.GetType() != (int)swDocumentTypes_e.swDocDRAWING)
        {
            Console.Error.WriteLine("錯誤：請開啟工程圖後再執行。");
            return;
        }

        var formatMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "pdf",    ".pdf"    },
            { "png",    ".png"    },
            { "jpg",    ".jpg"    },
            { "jpeg",   ".jpg"    },
            { "tif",    ".tif"    },
            { "tiff",   ".tif"    },
            { "dwg",    ".dwg"    },
            { "dxf",    ".dxf"    },
            { "ai",     ".ai"     },
            { "psd",    ".psd"    },
            { "edrw",   ".edrw"   },
            { "html",   ".html"   },
            { "slddrw", ".slddrw" },
        };

        string format = args.Length > 0 ? args[0].TrimStart('.') : "pdf";
        if (!formatMap.TryGetValue(format, out string? ext))
        {
            Console.Error.WriteLine($"錯誤：不支援的格式 \"{format}\"");
            Console.Error.WriteLine($"支援格式：{string.Join(", ", formatMap.Keys)}");
            return;
        }

        string drawingPath = swDoc.GetPathName();
        if (string.IsNullOrEmpty(drawingPath))
        {
            Console.Error.WriteLine("錯誤：工程圖尚未存檔，請先存檔再執行。");
            return;
        }

        string drawingDir  = Path.GetDirectoryName(drawingPath)!;
        string drawingName = Path.GetFileNameWithoutExtension(drawingPath);

        string outputPath = args.Length > 1
            ? args[1]
            : Path.Combine(drawingDir, drawingName + ext);

        Console.WriteLine($"工程圖 : {Path.GetFileName(drawingPath)}");
        Console.WriteLine($"格式   : {format.ToUpper()}");
        Console.WriteLine($"輸出   : {outputPath}");

        var swExt = (ModelDocExtension)swDoc.Extension;
        int errors = 0, warnings = 0;

        // PDF 匯出需要額外傳入 ExportPdfData 並設定 Sheet 範圍
        object? exportData = null;
        if (ext == ".pdf")
        {
            dynamic pdfData = swApp.GetExportFileData((int)swExportDataFileType_e.swExportPdfData);
            pdfData.SetSheets((int)swExportDataSheetsToExport_e.swExportData_ExportAllSheets, null);
            pdfData.ViewPdfAfterSaving = false;
            exportData = pdfData;
        }

        bool result = swExt.SaveAs3(
            outputPath,
            0,
            (int)swSaveAsOptions_e.swSaveAsOptions_Silent,
            exportData,
            null,
            ref errors,
            ref warnings
        );

        if (result)
        {
            Console.WriteLine($"✓ 匯出成功");
            var fi = new FileInfo(outputPath);
            Console.WriteLine($"  大小: {fi.Length / 1024} KB");
        }
        else
        {
            Console.Error.WriteLine($"✗ 匯出失敗！錯誤碼: {errors}, 警告碼: {warnings}");
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
