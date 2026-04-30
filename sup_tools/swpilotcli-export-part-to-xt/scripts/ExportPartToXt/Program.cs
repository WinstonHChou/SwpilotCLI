// Usage:
//   dotnet run --project ExportPartToXt.csproj
//       -> export active doc (must be .sldprt) to same directory
//   dotnet run --project ExportPartToXt.csproj -- "C:\path\to\part.sldprt"
//       -> export specified part to its own directory
//   dotnet run --project ExportPartToXt.csproj -- "C:\path\to\part.sldprt" "C:\output\dir"
//       -> export to specified output directory

using System;
using System.IO;
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

        try
        {
            swApp = ConnectToSolidWorks();
            if (swApp == null)
            {
                Console.Error.WriteLine("Unable to attach to SolidWorks.");
                return 2;
            }

            swApp.Visible = true;

            string? partPath = args.Length > 0 ? args[0] : null;
            string? outputDir = args.Length > 1 ? args[1] : null;

            // No part path given — use active document
            if (string.IsNullOrWhiteSpace(partPath))
            {
                var activeDoc = swApp.IActiveDoc2;
                if (activeDoc == null)
                {
                    Console.Error.WriteLine("No active document in SolidWorks.");
                    return 3;
                }

                if (activeDoc.GetType() != (int)swDocumentTypes_e.swDocPART)
                {
                    Console.Error.WriteLine("Active document is not a part (.sldprt). Provide a part path as argument.");
                    Marshal.ReleaseComObject(activeDoc);
                    return 4;
                }

                partPath = activeDoc.GetPathName();
                Marshal.ReleaseComObject(activeDoc);

                if (string.IsNullOrWhiteSpace(partPath))
                {
                    Console.Error.WriteLine("Active part has not been saved yet.");
                    return 5;
                }
            }

            partPath = Path.GetFullPath(partPath);
            if (!File.Exists(partPath))
            {
                Console.Error.WriteLine($"File not found: {partPath}");
                return 6;
            }

            outputDir = string.IsNullOrWhiteSpace(outputDir)
                ? Path.GetDirectoryName(partPath)!
                : outputDir;

            Directory.CreateDirectory(outputDir);

            var outputXt = Path.Combine(outputDir, Path.GetFileNameWithoutExtension(partPath) + ".x_t");

            ExportToXt(swApp, partPath, outputXt);
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

    private static void ExportToXt(SldWorks swApp, string partPath, string outputXt)
    {
        Console.WriteLine($"Part:   {partPath}");
        Console.WriteLine($"Output: {outputXt}");

        var wasAlreadyOpen = swApp.GetOpenDocumentByName(partPath) != null;

        int openErrors = 0, openWarnings = 0;
        var opened = swApp.OpenDoc6(
            partPath,
            (int)swDocumentTypes_e.swDocPART,
            (int)swOpenDocOptions_e.swOpenDocOptions_Silent,
            "",
            ref openErrors,
            ref openWarnings
        );

        if (opened is not ModelDoc2 partDoc)
            throw new InvalidOperationException($"Failed to open part (errors={openErrors}, warnings={openWarnings}).");

        var partTitle = partDoc.GetTitle();

        try
        {
            int activateErrors = 0;
            swApp.ActivateDoc3(
                partTitle,
                false,
                (int)swRebuildOnActivation_e.swDontRebuildActiveDoc,
                ref activateErrors
            );

            int saveErrors = 0, saveWarnings = 0;
            var saved = partDoc.Extension.SaveAs3(
                outputXt,
                (int)swSaveAsVersion_e.swSaveAsCurrentVersion,
                (int)swSaveAsOptions_e.swSaveAsOptions_Silent,
                null,
                null,
                ref saveErrors,
                ref saveWarnings
            );

            if (saved && File.Exists(outputXt))
                Console.WriteLine($"OK -> {outputXt}");
            else
                throw new InvalidOperationException($"SaveAs3 failed (saveErrors={saveErrors}, saveWarnings={saveWarnings}).");
        }
        finally
        {
            if (!wasAlreadyOpen)
                swApp.CloseDoc(partTitle);
        }
    }

    private static SldWorks? ConnectToSolidWorks()
    {
        try { return Interaction.GetObject(string.Empty, "SldWorks.Application") as SldWorks; }
        catch (COMException) { }

        var type = Type.GetTypeFromProgID("SldWorks.Application");
        return type == null ? null : (SldWorks?)Activator.CreateInstance(type);
    }
}
