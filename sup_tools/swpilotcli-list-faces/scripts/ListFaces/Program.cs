using System;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.VisualBasic;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace ListFaces;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        SldWorks? swApp = null;
        ModelDoc2? swDoc = null;
        try
        {
            swApp = Interaction.GetObject(string.Empty, "SldWorks.Application") as SldWorks;
            if (swApp == null) { Console.Error.WriteLine("Unable to attach to SolidWorks."); return 2; }

            swDoc = swApp.IActiveDoc2;
            if (swDoc == null) { Console.Error.WriteLine("No active document."); return 3; }

            if (swDoc.GetType() != (int)swDocumentTypes_e.swDocPART)
            {
                Console.Error.WriteLine("Active document is not a Part.");
                return 1;
            }

            var part = (PartDoc)swDoc;
            var bodies = part.GetBodies2((int)swBodyType_e.swSolidBody, false) as object[];
            if (bodies == null || bodies.Length == 0) { Console.Error.WriteLine("No solid bodies."); return 1; }

            int faceIdx = 0;
            foreach (Body2 body in bodies.OfType<Body2>())
            {
                var faces = body.GetFaces() as object[];
                if (faces == null) continue;
                foreach (Face2 face in faces.OfType<Face2>())
                {
                    double[]? n = face.Normal as double[];
                    double[]? box = face.GetBox() as double[];
                    if (n == null || box == null) continue;
                    double cx = (box[0] + box[3]) / 2 * 1000;
                    double cy = (box[1] + box[4]) / 2 * 1000;
                    double cz = (box[2] + box[5]) / 2 * 1000;
                    Console.WriteLine($"[{faceIdx}] Normal=({n[0]:F2},{n[1]:F2},{n[2]:F2})  Centre=({cx:F1},{cy:F1},{cz:F1})mm");
                    faceIdx++;
                }
            }
            Console.WriteLine($"Total: {faceIdx} face(s).");
            return 0;
        }
        catch (COMException ex)
        {
            Console.Error.WriteLine($"COM error: 0x{ex.ErrorCode:X8} - {ex.Message}");
            return 1;
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
}
