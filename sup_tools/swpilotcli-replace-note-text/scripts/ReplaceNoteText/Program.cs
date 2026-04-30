// Usage: -- <find> <replace>
// Finds all Note annotations in the active drawing that contain <find>
// and replaces every occurrence with <replace>.
// Does NOT save the file — caller is responsible.

using System;
using System.Runtime.InteropServices;
using Microsoft.VisualBasic;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace ReplaceNoteText;

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
                Console.Error.WriteLine("用法: -- <找尋文字> <替換文字>");
                return 1;
            }

            string findText    = args[0];
            string replaceText = args[1];

            swApp = ConnectToSolidWorks();
            if (swApp == null)
            {
                Console.Error.WriteLine("無法連接 SolidWorks。");
                return 2;
            }

            swApp.Visible = true;
            swDoc = swApp.IActiveDoc2;

            if (swDoc == null)
            {
                Console.Error.WriteLine("SolidWorks 沒有開啟中的文件。");
                return 3;
            }

            if (swDoc.GetType() != (int)swDocumentTypes_e.swDocDRAWING)
            {
                Console.Error.WriteLine("目前文件不是工程圖 (.slddrw)。");
                return 4;
            }

            Console.WriteLine($"文件: {swDoc.GetPathName() ?? swDoc.GetTitle()}");
            Console.WriteLine($"找尋: \"{findText}\"  →  替換: \"{replaceText}\"");
            Console.WriteLine();

            int replaced = ReplaceNotesInDrawing((DrawingDoc)swDoc, findText, replaceText);

            Console.WriteLine($"完成：共替換 {replaced} 個 Note 註解。");

            // Exit code 0 = success (even if replaced == 0)
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
