using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        if (swDoc.GetType() != (int)swDocumentTypes_e.swDocASSEMBLY)
        {
            throw new InvalidOperationException("Active document is not an assembly.");
        }

        var assemblyPath = swDoc.GetPathName();
        if (string.IsNullOrWhiteSpace(assemblyPath))
        {
            throw new InvalidOperationException("Active assembly has not been saved yet.");
        }

        var assemblyDir = Path.GetDirectoryName(assemblyPath);
        if (string.IsNullOrWhiteSpace(assemblyDir))
        {
            throw new InvalidOperationException("Cannot resolve assembly directory.");
        }

        var outputDir = Path.Combine(assemblyDir, "xt");
        Directory.CreateDirectory(outputDir);
        Console.WriteLine($"Output folder: {outputDir}");

        var assemblyDoc = swDoc as AssemblyDoc;
        if (assemblyDoc == null)
        {
            throw new InvalidOperationException("Failed to cast active document to AssemblyDoc.");
        }

        var componentsRaw = assemblyDoc.GetComponents(false);
        if (componentsRaw == null)
        {
            Console.WriteLine("No components found.");
            return;
        }

        var componentObjects = componentsRaw as object[] ?? new[] { componentsRaw };
        var uniquePartPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in componentObjects)
        {
            var comp = item as Component2;
            if (comp == null)
            {
                continue;
            }

            var partPath = comp.GetPathName();
            if (string.IsNullOrWhiteSpace(partPath))
            {
                continue;
            }

            if (!string.Equals(Path.GetExtension(partPath), ".sldprt", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            try
            {
                partPath = Path.GetFullPath(partPath);
            }
            catch
            {
                // Keep original path if normalization fails.
            }

            uniquePartPaths.Add(partPath);
        }

        Console.WriteLine($"Found unique part files: {uniquePartPaths.Count}");
        if (uniquePartPaths.Count == 0)
        {
            return;
        }

        var usedOutputBases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var originalAssemblyTitle = swDoc.GetTitle();
        var exported = 0;
        var skipped = 0;
        var failed = 0;

        foreach (var partPath in uniquePartPaths.OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
        {
            if (!File.Exists(partPath))
            {
                skipped++;
                Console.WriteLine($"SKIP missing file: {partPath}");
                continue;
            }

            var outputBase = Path.GetFileNameWithoutExtension(partPath);
            var outputXt = BuildUniqueOutputPath(outputDir, outputBase, usedOutputBases, ".x_t");

            var wasAlreadyOpen = swApp.GetOpenDocumentByName(partPath) != null;

            int openErrors = 0;
            int openWarnings = 0;
            var opened = swApp.OpenDoc6(
                partPath,
                (int)swDocumentTypes_e.swDocPART,
                (int)swOpenDocOptions_e.swOpenDocOptions_Silent,
                "",
                ref openErrors,
                ref openWarnings
            );

            if (opened is not ModelDoc2 partDoc)
            {
                failed++;
                Console.WriteLine($"FAIL open: {partPath} (openErrors={openErrors}, openWarnings={openWarnings})");
                continue;
            }

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

                int saveErrors = 0;
                int saveWarnings = 0;
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
                {
                    exported++;
                    Console.WriteLine($"OK  {Path.GetFileName(partPath)} -> {Path.GetFileName(outputXt)}");
                }
                else
                {
                    failed++;
                    Console.WriteLine($"FAIL save: {partPath} (saveErrors={saveErrors}, saveWarnings={saveWarnings})");
                }
            }
            finally
            {
                if (!wasAlreadyOpen)
                {
                    swApp.CloseDoc(partTitle);
                }
            }
        }

        int restoreErrors = 0;
        swApp.ActivateDoc3(
            originalAssemblyTitle,
            false,
            (int)swRebuildOnActivation_e.swDontRebuildActiveDoc,
            ref restoreErrors
        );

        Console.WriteLine($"Summary: exported={exported}, skipped={skipped}, failed={failed}");

        static string BuildUniqueOutputPath(
            string outputDir,
            string preferredBase,
            HashSet<string> usedBases,
            string extension)
        {
            var baseName = preferredBase;
            var index = 2;
            while (usedBases.Contains(baseName) || File.Exists(Path.Combine(outputDir, baseName + extension)))
            {
                baseName = $"{preferredBase}_{index}";
                index++;
            }

            usedBases.Add(baseName);
            return Path.Combine(outputDir, baseName + extension);
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
