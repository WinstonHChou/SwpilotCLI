using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.VisualBasic;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace InspectDrawing;

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
        if (swDoc.GetType() != (int)swDocumentTypes_e.swDocDRAWING)
        {
            Console.Error.WriteLine("錯誤：請開啟工程圖。");
            return;
        }

        var swDraw   = (DrawingDoc)swDoc;
        var mathUtil = (MathUtility)swApp.GetMathUtility();

        Console.WriteLine("========================================");
        Console.WriteLine("  完整屬性診斷工具");
        Console.WriteLine("========================================\n");

        object[] sheetViews = (object[])swDraw.GetViews();

        for (int s = 0; s < sheetViews.Length; s++)
        {
            object[] views = (object[])sheetViews[s];
            if (views == null) continue;

            for (int v = 0; v < views.Length; v++)
            {
                var swView   = (SolidWorks.Interop.sldworks.View)views[v];
                bool isSheet = (v == 0);
                string viewLabel = isSheet ? "【圖紙】" : "【視圖】";
                Console.WriteLine($"{viewLabel}{swView.GetName2()}");

                MathTransform viewXform = swView.ModelToViewTransform;

                Annotation ann = swView.GetFirstAnnotation3();
                while (ann != null)
                {
                    int annType   = ann.GetType();
                    bool dangling = ann.IsDangling();
                    double[] apos = (double[])ann.GetPosition();
                    string posStr = apos != null ? $"({apos[0]*1000:F3}, {apos[1]*1000:F3}) mm" : "(無)";

                    // ── DisplayDimension ──────────────────────────────
                    if (annType == (int)swAnnotationType_e.swDisplayDimension)
                    {
                        var dispDim = (DisplayDimension)ann.GetSpecificAnnotation();
                        var dim     = dispDim.GetDimension2(0);
                        string status = dangling ? "[!懸空]" : "[v正常]";

                        Console.WriteLine($"\n  {status} DisplayDimension");
                        Console.WriteLine($"    名稱       : {dim?.FullName ?? "(無)"}");
                        Console.WriteLine($"    數值       : {(dim?.SystemValue ?? 0)*1000:F3} mm");
                        Console.WriteLine($"    類型       : {dispDim.Type2}");
                        Console.WriteLine($"    文字位置   : {posStr}");

                        string[] textPartNames = { "Prefix", "Suffix", "CalloutAbove", "CalloutBelow",
                                                    "PrefixDef", "SuffixDef", "CalloutAboveDef", "CalloutBelowDef" };
                        int[] textPartVals = { 1, 2, 3, 4, 5, 6, 7, 8 };
                        for (int i = 0; i < textPartVals.Length; i++)
                        {
                            try
                            {
                                string t = dispDim.GetText(textPartVals[i]) ?? "";
                                if (!string.IsNullOrEmpty(t))
                                    Console.WriteLine($"    文字[{textPartNames[i]}]: \"{t}\"");
                            }
                            catch { }
                        }

                        try
                        {
                            var tol = (DimensionTolerance)dim!.Tolerance;
                            Console.WriteLine($"    公差Type   : {tol?.Type ?? 0}");
                            if (tol != null && tol.Type != 0)
                            {
                                Console.WriteLine($"    公差Max    : +{tol.GetMaxValue()*1000:F3} mm");
                                Console.WriteLine($"    公差Min    : {tol.GetMinValue()*1000:F3} mm");
                            }
                        }
                        catch { }

                        try
                        {
                            Console.WriteLine($"    精度Primary: {dispDim.GetPrimaryPrecision2()}");
                            Console.WriteLine($"    精度TolPri : {dispDim.GetPrimaryTolPrecision2()}");
                        }
                        catch { }

                        try { Console.WriteLine($"    箭頭樣式   : {dispDim.GetArrowHeadStyle()}"); }
                        catch { }

                        Console.WriteLine($"    圖層       : \"{ann.Layer ?? ""}\"");
                        Console.WriteLine($"    顏色(ARGB) : 0x{ann.Color:X8}");
                        Console.WriteLine($"    可見       : {ann.Visible}");
                    }

                    // ── Note ────────────────────────────────────────
                    else if (annType == (int)swAnnotationType_e.swNote)
                    {
                        dynamic note = ann.GetSpecificAnnotation();
                        string status = dangling ? "[!懸空]" : "[v正常]";

                        Console.WriteLine($"\n  {status} Note");
                        Console.WriteLine($"    文字位置   : {posStr}");

                        try { Console.WriteLine($"    文字內容   : \"{note.GetText() ?? ""}\""); } catch { }

                        try
                        {
                            double[] ap = (double[])note.GetAttachPos();
                            if (ap != null && ap.Length >= 3)
                                Console.WriteLine($"    引線附著點 : ({ap[0]*1000:F3}, {ap[1]*1000:F3}, {ap[2]*1000:F3}) mm");
                        }
                        catch { }

                        try { Console.WriteLine($"    箭頭數量   : {note.GetArrowHeadCount()}"); } catch { }
                        try
                        {
                            double[] ai = (double[])note.GetArrowHeadInfo();
                            if (ai != null && ai.Length >= 4)
                                Console.WriteLine($"    箭頭樣式   : {(int)ai[3]}  大小: {ai[1]*1000:F2}x{ai[2]*1000:F2} mm");
                        }
                        catch { }

                        try { Console.WriteLine($"    角度       : {note.Angle * 180/Math.PI:F1}°"); } catch { }
                        Console.WriteLine($"    圖層       : \"{ann.Layer ?? ""}\"");
                        Console.WriteLine($"    顏色(ARGB) : 0x{ann.Color:X8}");
                    }

                    ann = ann.GetNext3();
                }

                // ── 幾何候選點 ──────────────────────────────────────
                Console.WriteLine($"\n  --- 幾何候選點 ---");
                var ptList = new List<(string type, double mx, double my, double mz, double dx, double dy)>();

                object vertObj = swView.GetVisibleEntities2(null, (int)swViewEntityType_e.swViewEntityType_Vertex);
                object[] verts = vertObj as object[] ?? Array.Empty<object>();
                foreach (var vo in verts)
                {
                    var vertex = (Vertex)vo;
                    double[] mp = (double[])vertex.GetPoint();
                    var mpt = (MathPoint)mathUtil.CreatePoint(new double[] { mp[0], mp[1], mp[2] });
                    var dpt = (MathPoint)mpt.MultiplyTransform(viewXform);
                    double[] dp = (double[])dpt.ArrayData;
                    ptList.Add(("V", mp[0]*1000, mp[1]*1000, mp[2]*1000, dp[0]*1000, dp[1]*1000));
                }

                for (int i = 0; i < ptList.Count; i++)
                {
                    var p = ptList[i];
                    Console.WriteLine($"  P{i}[{p.type}] 模型=({p.mx:F2},{p.my:F2},{p.mz:F2})  圖紙=({p.dx:F2},{p.dy:F2}) mm");
                }

                Console.WriteLine("\n========================================");
            }
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
