using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.VisualBasic;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace CheckDanglingDims;

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
        if (swDoc == null || swDoc.GetType() != (int)swDocumentTypes_e.swDocDRAWING)
        {
            Console.WriteLine("錯誤：請開啟一張工程圖。");
            return;
        }

        var swDraw = (DrawingDoc)swDoc;
        string docTitle = swDoc.GetTitle();
        Console.WriteLine("==============================================");
        Console.WriteLine($"工程圖: {docTitle}");
        Console.WriteLine("==============================================");
        Console.WriteLine("正在檢查 Dangling Dimensions（懸空尺寸）...");
        Console.WriteLine();

        object[] sheetViews = (object[])swDraw.GetViews();
        if (sheetViews == null || sheetViews.Length == 0)
        {
            Console.WriteLine("此工程圖沒有任何視圖。");
            return;
        }

        int totalDimCount = 0;
        int danglingCount = 0;
        var danglingList = new List<string>();

        for (int s = 0; s < sheetViews.Length; s++)
        {
            object[] views = (object[])sheetViews[s];
            if (views == null) continue;

            for (int v = 0; v < views.Length; v++)
            {
                var swView = (SolidWorks.Interop.sldworks.View)views[v];
                string viewName = swView.GetName2();
                bool isSheet = (v == 0);
                string label = isSheet ? $"[圖紙] {viewName}" : $"[視圖] {viewName}";

                // GetFirstAnnotation3 遍歷所有 annotation（包含 dangling）
                Annotation ann = swView.GetFirstAnnotation3();
                while (ann != null)
                {
                    int annType = ann.GetType();
                    if (annType == (int)swAnnotationType_e.swDisplayDimension)
                    {
                        totalDimCount++;
                        DisplayDimension dispDim = (DisplayDimension)ann.GetSpecificAnnotation();
                        Dimension dim = dispDim.GetDimension2(0);

                        bool isDangling = ann.IsDangling();

                        string dimName = dim != null ? dim.FullName : "(無法取得)";
                        double value = dim != null ? dim.SystemValue : 0;

                        int dimTypeInt = dispDim.Type2;
                        string dimTypeName = dimTypeInt switch
                        {
                            1  => "序號尺寸",
                            2  => "線性尺寸",
                            3  => "角度尺寸",
                            4  => "弧長尺寸",
                            5  => "半徑尺寸",
                            6  => "直徑尺寸",
                            7  => "水平序號",
                            8  => "垂直序號",
                            10 => "倒角尺寸",
                            11 => "水平線性",
                            12 => "垂直線性",
                            _  => $"其他({dimTypeInt})"
                        };

                        string displayValue;
                        if (dimTypeInt == 3)
                        {
                            double degrees = value * (180.0 / Math.PI);
                            displayValue = $"{degrees:F2}°";
                        }
                        else
                        {
                            double mm = value * 1000.0;
                            displayValue = $"{mm:F3} mm";
                        }

                        string status = isDangling ? "*** DANGLING（懸空）***" : "OK";

                        if (isDangling)
                        {
                            danglingCount++;
                            danglingList.Add($"  [!] {label} -> {dimTypeName} | 值: {displayValue} | 名稱: {dimName}");
                        }

                        Console.WriteLine($"  {(isDangling ? "[!]" : "[v]")} {label,-20} | {dimTypeName,-8} | {displayValue,-14} | {dimName,-40} | {status}");
                    }
                    ann = ann.GetNext3();
                }
            }
        }

        Console.WriteLine();
        Console.WriteLine("==============================================");
        Console.WriteLine($"檢查完成！共 {totalDimCount} 個尺寸，其中 {danglingCount} 個為懸空尺寸。");

        if (danglingCount > 0)
        {
            Console.WriteLine();
            Console.WriteLine("以下尺寸為 Dangling（懸空），需要修復：");
            foreach (var item in danglingList)
                Console.WriteLine(item);
            Console.WriteLine();
            Console.WriteLine("建議：請在 SolidWorks 中檢查這些尺寸，重新附著到正確的幾何邊線。");
        }
        else
        {
            Console.WriteLine("所有尺寸都正常，沒有發現懸空尺寸。");
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
