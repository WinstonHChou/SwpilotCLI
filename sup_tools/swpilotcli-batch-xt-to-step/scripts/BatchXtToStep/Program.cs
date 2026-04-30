using Microsoft.VisualBasic;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using System.Runtime.InteropServices;

namespace BatchXtToStep;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        SldWorks? swApp = null;
        ModelDoc2? activeDoc = null;

        try
        {
            if (args.Length < 1 || string.IsNullOrWhiteSpace(args[0]))
            {
                Console.Error.WriteLine("Usage: BatchXtToStep <folder-path>");
                return 64;
            }

            string folderPath = Path.GetFullPath(args[0]);
            if (!Directory.Exists(folderPath))
            {
                Console.Error.WriteLine($"Folder not found: {folderPath}");
                return 65;
            }

            swApp = ConnectToSolidWorks();
            if (swApp == null)
            {
                Console.Error.WriteLine("Unable to attach to SolidWorks.");
                return 2;
            }

            swApp.Visible = true;
            activeDoc = swApp.IActiveDoc2;

            return ExecuteTask(activeDoc, swApp, folderPath);
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
            if (activeDoc != null)
            {
                Marshal.ReleaseComObject(activeDoc);
            }

            if (swApp != null)
            {
                Marshal.ReleaseComObject(swApp);
            }
        }
    }

    private static int ExecuteTask(ModelDoc2? initialDoc, SldWorks swApp, string folderPath)
    {
        ConfigureImportPreferences(swApp);
        string partTemplatePath = ResolvePartTemplatePath(swApp);

        string[] candidates = Directory
            .EnumerateFiles(folderPath, "*.*", SearchOption.TopDirectoryOnly)
            .Where(path =>
            {
                string ext = Path.GetExtension(path);
                return ext.Equals(".x_t", StringComparison.OrdinalIgnoreCase)
                    || ext.Equals(".xt", StringComparison.OrdinalIgnoreCase);
            })
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (candidates.Length == 0)
        {
            Console.Error.WriteLine($"No .x_t or .xt files found in: {folderPath}");
            return 66;
        }

        Console.WriteLine($"Default part template: {partTemplatePath}");
        Console.WriteLine($"Folder: {folderPath}");
        Console.WriteLine($"Found: {candidates.Length} file(s)");

        int successCount = 0;
        int failureCount = 0;

        foreach (string inputPath in candidates)
        {
            string outputPath = Path.Combine(
                folderPath,
                Path.GetFileNameWithoutExtension(inputPath) + ".step");

            ModelDoc2? openedDoc = null;
            string? closeName = null;

            Console.WriteLine();
            Console.WriteLine($"Input:  {inputPath}");
            Console.WriteLine($"Output: {outputPath}");

            try
            {
                openedDoc = (ModelDoc2?)swApp.NewDocument(partTemplatePath, 0, 0, 0);

                if (openedDoc == null)
                {
                    throw new InvalidOperationException("NewDocument failed.");
                }

                closeName = openedDoc.GetPathName();
                if (string.IsNullOrWhiteSpace(closeName))
                {
                    closeName = openedDoc.GetTitle();
                }

                if (openedDoc is not PartDoc partDoc)
                {
                    throw new InvalidOperationException("NewDocument did not create a PartDoc.");
                }

                string activateName = openedDoc.GetTitle();
                int activateErrors = 0;
                ModelDoc2? activeResult = swApp.ActivateDoc3(
                    activateName,
                    false,
                    (int)swRebuildOnActivation_e.swDontRebuildActiveDoc,
                    ref activateErrors);

                if (activeResult == null || activateErrors != 0)
                {
                    if (activeResult != null)
                    {
                        Marshal.ReleaseComObject(activeResult);
                    }

                    throw new InvalidOperationException(
                        $"ActivateDoc3 failed (errors={activateErrors}).");
                }

                Marshal.ReleaseComObject(activeResult);

                int importErrors = 0;
                object importedFeature = partDoc.InsertImportedFeature(inputPath, out importErrors);
                if (importedFeature == null)
                {
                    throw new InvalidOperationException(
                        $"InsertImportedFeature failed (errors={importErrors}).");
                }

                Marshal.ReleaseComObject(importedFeature);

                int saveErrors = 0;
                int saveWarnings = 0;
                bool saved = openedDoc.Extension.SaveAs3(
                    outputPath,
                    (int)swSaveAsVersion_e.swSaveAsCurrentVersion,
                    (int)swSaveAsOptions_e.swSaveAsOptions_Silent,
                    null,
                    null,
                    ref saveErrors,
                    ref saveWarnings);

                if (!saved || !File.Exists(outputPath))
                {
                    throw new InvalidOperationException(
                        $"SaveAs3 failed (errors={saveErrors}, warnings={saveWarnings}).");
                }

                Console.WriteLine("Status: OK");
                successCount++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Status: FAIL - {ex.Message}");
                failureCount++;
            }
            finally
            {
                if (openedDoc != null)
                {
                    Marshal.ReleaseComObject(openedDoc);
                }

                if (!string.IsNullOrWhiteSpace(closeName))
                {
                    swApp.CloseDoc(closeName);
                }
            }
        }

        if (initialDoc != null)
        {
            string initialTitle = initialDoc.GetTitle();
            if (!string.IsNullOrWhiteSpace(initialTitle))
            {
                int restoreErrors = 0;
                ModelDoc2? restored = swApp.ActivateDoc3(
                    initialTitle,
                    false,
                    (int)swRebuildOnActivation_e.swDontRebuildActiveDoc,
                    ref restoreErrors);
                if (restored != null)
                {
                    Marshal.ReleaseComObject(restored);
                }
            }
        }

        Console.WriteLine();
        Console.WriteLine($"Summary: success={successCount}, failed={failureCount}, total={candidates.Length}");
        return failureCount == 0 ? 0 : 1;
    }

    private static SldWorks? ConnectToSolidWorks()
    {
        try
        {
            return Interaction.GetObject(string.Empty, "SldWorks.Application") as SldWorks;
        }
        catch (COMException)
        {
        }

        Type? type = Type.GetTypeFromProgID("SldWorks.Application");
        return type == null ? null : (SldWorks?)Activator.CreateInstance(type);
    }

    private static void ConfigureImportPreferences(SldWorks swApp)
    {
        swApp.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swAlwaysUseDefaultTemplates, true);
        swApp.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swMultiCAD_Enable3DInterconnect, true);
        swApp.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swImportAutoRunImportDiagnostics, false);
        swApp.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swImportAutoRunImportDiagnosticsPersist, false);
        swApp.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swForceEnableImportDiagnosis, false);
        swApp.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swImportNeutralRunDiagnostics, false);
        swApp.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swImportSolidBody, true);
        swApp.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swImportSurfaceBody, false);
        swApp.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swImportSolidSurface, false);
        swApp.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swMultiCAD_ApplyOnlyToParts, true);
        swApp.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swImportMultBodyAsPartData, true);
    }

    private static string ResolvePartTemplatePath(SldWorks swApp)
    {
        string current = swApp.GetUserPreferenceStringValue(
            (int)swUserPreferenceStringValue_e.swDefaultTemplatePart);

        if (!string.IsNullOrWhiteSpace(current) && File.Exists(current))
        {
            return current;
        }

        string[] candidates =
        {
            Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.CommonApplicationData), "SOLIDWORKS", "SOLIDWORKS 2025", "templates"),
            Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.CommonDocuments), "SOLIDWORKS", "SOLIDWORKS 2025", "templates")
        };

        foreach (string dir in candidates)
        {
            if (!Directory.Exists(dir))
            {
                continue;
            }

            string? template = Directory
                .EnumerateFiles(dir, "*.prtdot", SearchOption.AllDirectories)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();

            if (template == null)
            {
                continue;
            }

            bool ok = swApp.SetUserPreferenceStringValue(
                (int)swUserPreferenceStringValue_e.swDefaultTemplatePart,
                template);

            if (!ok)
            {
                throw new InvalidOperationException($"Failed to set default part template: {template}");
            }

            return template;
        }

        throw new InvalidOperationException("No .prtdot template found for swDefaultTemplatePart.");
    }
}
