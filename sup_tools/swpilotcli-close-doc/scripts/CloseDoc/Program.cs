using System;
using System.Runtime.InteropServices;
using Microsoft.VisualBasic;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace CloseDoc;

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

            ExecuteTask(swDoc, swApp);

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

    private static void ExecuteTask(ModelDoc2 swDoc, SldWorks swApp)
    {
        // Parse argument: "save" | "nosave" | "all" (default: nosave)
        string[] cliArgs = System.Environment.GetCommandLineArgs();
        string arg = cliArgs.Length > 1 ? cliArgs[cliArgs.Length - 1].ToLower() : "nosave";

        // "all" mode: close every open document, no save
        if (arg == "all")
        {
            bool ok = swApp.CloseAllDocuments(true); // true = include unsaved
            Console.WriteLine(ok ? "✓ 所有文件已關閉" : "❌ CloseAllDocuments 回傳失敗");
            return;
        }

        bool shouldSave = arg == "save";

        string docTitle = swDoc.GetTitle();
        string docPath  = swDoc.GetPathName();

        Console.WriteLine($"文件  : {docTitle}");
        Console.WriteLine($"動作  : {(shouldSave ? "存檔後關閉" : "直接關閉（不存檔）")}");

        if (shouldSave)
        {
            if (string.IsNullOrEmpty(docPath))
            {
                Console.WriteLine("⚠ 新建文件尚未儲存過，無法存檔，強制關閉...");
            }
            else
            {
                int errors = 0, warnings = 0;
                bool saved = swDoc.Save3(
                    (int)swSaveAsOptions_e.swSaveAsOptions_Silent,
                    ref errors, ref warnings);

                if (saved)
                    Console.WriteLine("✓ 存檔成功");
                else
                    Console.WriteLine($"⚠ 存檔失敗 (errors={errors})，強制關閉...");
            }
        }

        // Close active document silently — no dialog, no save prompt
        swApp.CloseDoc(docPath);
        Console.WriteLine($"✓ 已關閉: {docTitle}");
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
