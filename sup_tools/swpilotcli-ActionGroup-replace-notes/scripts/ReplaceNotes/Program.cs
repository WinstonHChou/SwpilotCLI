// Usage: -- <sourceFolder> <outputFolder> <find> <replace> <suffix>
// Batch find-and-replace text in Note annotations across all .slddrw files
// in sourceFolder (root only). Saves modified copies to outputFolder.
// Original files are never modified.

using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.VisualBasic;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace ReplaceNotes;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        SldWorks? swApp = null;

        try
        {
            if (args.Length < 5)
            {
                Console.Error.WriteLine("用法: -- <來源資料夾> <輸出資料夾> <找尋文字> <替換文字> <檔名後綴>");
                return 1;
            }

            string sourceFolder = args[0];
            string outputFolder = args[1];
            string findText     = args[2];
            string replaceText  = args[3];
            string suffix       = args[4];

            swApp = ConnectToSolidWorks();
            if (swApp == null)
            {
                Console.Error.WriteLine("無法連接 SolidWorks。請確認 SolidWorks 已開啟。");
                return 2;
            }

            swApp.Visible = true;

            if (!Directory.Exists(sourceFolder))
            {
                Console.Error.WriteLine($"來源資料夾不存在: {sourceFolder}");
                return 3;
            }

            Directory.CreateDirectory(outputFolder);

            string[] drawingFiles = Directory.GetFiles(sourceFolder, "*.slddrw",
                SearchOption.TopDirectoryOnly);

            if (drawingFiles.Length == 0)
            {
                Console.WriteLine("資料夾內找不到任何 .slddrw 檔案。");
                return 0;
            }

            Console.WriteLine($"來源資料夾 : {sourceFolder}");
            Console.WriteLine($"輸出資料夾 : {outputFolder}");
            Console.WriteLine($"找尋 → 替換: \"{findText}\" → \"{replaceText}\"");
            Console.WriteLine($"共找到 {drawingFiles.Length} 個工程圖，開始處理...\n");

            int totalModified = 0;

            foreach (string filePath in drawingFiles)
            {
                string fileName = Path.GetFileNameWithoutExtension(filePath);
                string outPath  = Path.Combine(outputFolder, fileName + suffix + ".slddrw");

                Console.WriteLine($"[{fileName}]");

                int openErrors = 0, openWarnings = 0;
                ModelDoc2? doc = (ModelDoc2?)swApp.OpenDoc6(
                    filePath,
                    (int)swDocumentTypes_e.swDocDRAWING,
                    (int)swOpenDocOptions_e.swOpenDocOptions_Silent,
                    "", ref openErrors, ref openWarnings);

                if (doc == null)
                {
                    Console.WriteLine($"  ✗ 開啟失敗 (errors={openErrors})，跳過。\n");
                    continue;
                }

                int noteCount = ReplaceNotesInDrawing((DrawingDoc)doc, findText, replaceText);

                if (noteCount == 0)
                {
                    Console.WriteLine($"  (無含「{findText}」的註解，跳過存檔)");
                    swApp.CloseDoc(filePath);
                    Marshal.ReleaseComObject(doc);
                    Console.WriteLine();
                    continue;
                }

                var docExt = (ModelDocExtension)doc.Extension;
                int saveErrors = 0, saveWarnings = 0;
                bool saved = docExt.SaveAs3(
                    outPath,
                    0,
                    (int)swSaveAsOptions_e.swSaveAsOptions_Silent |
                    (int)swSaveAsOptions_e.swSaveAsOptions_Copy,
                    null, null,
                    ref saveErrors, ref saveWarnings);

                if (saved)
                {
                    Console.WriteLine($"  ✓ 已存新檔: {Path.GetFileName(outPath)}  (替換 {noteCount} 個註解)");
                    totalModified++;
                }
                else
                {
                    Console.WriteLine($"  ✗ 存檔失敗 (errors={saveErrors}, warnings={saveWarnings})");
                }

                swApp.CloseDoc(filePath);
                Marshal.ReleaseComObject(doc);
                Console.WriteLine();
            }

            Console.WriteLine("==============================");
            Console.WriteLine($"完成：共修改並另存 {totalModified} 個工程圖。");
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

    private static int ReplaceNotesInDrawing(DrawingDoc drawDoc, string findText, string replaceText)
    {
        int replaced = 0;

        object rawSheets = drawDoc.GetViews();
        if (rawSheets is not object[] sheetViews) return 0;

        foreach (object sheetObj in sheetViews)
        {
            if (sheetObj is not object[] views) continue;
            foreach (object viewObj in views)
            {
                var view = (SolidWorks.Interop.sldworks.View)viewObj;
                Annotation? ann = view.GetFirstAnnotation3();
                while (ann != null)
                {
                    if (ann.GetType() == (int)swAnnotationType_e.swNote)
                    {
                        var note = (INote)ann.GetSpecificAnnotation();
                        string? txt = note.GetText();
                        if (txt != null && txt.Contains(findText))
                        {
                            string newTxt = txt.Replace(findText, replaceText);
                            note.SetText(newTxt);
                            Console.WriteLine($"  替換: \"{txt}\" → \"{newTxt}\"");
                            replaced++;
                        }
                    }
                    ann = ann.GetNext3();
                }
            }
        }

        return replaced;
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
