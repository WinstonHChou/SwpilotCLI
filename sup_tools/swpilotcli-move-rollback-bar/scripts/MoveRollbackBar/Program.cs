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

            // === Execute task ===
            ExecuteTask(swDoc, swApp, args);
            // === End of task ===

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

    // ============================================================
    // AI: Only modify the code inside this method.
    // swDoc = active ModelDoc2, swApp = SldWorks application.
    // ============================================================
    private static void ExecuteTask(ModelDoc2 swDoc, SldWorks swApp, string[] args)
    {
        // Get all features
        var allFeatures = GetAllFeatures(swDoc);

        // Display feature list
        Console.WriteLine("=== Feature Manager Tree ===");
        for (int i = 0; i < allFeatures.Count; i++)
        {
            Console.WriteLine($"[{i}] {allFeatures[i]}");
        }
        Console.WriteLine();

        // Parse arguments: feature identifier and location
        string targetFeature = args.Length > 0 ? args[0] : "";
        string location = args.Length > 1 ? args[1] : "before";

        // If target is numeric, convert to feature name by index
        if (int.TryParse(targetFeature, out int featureIndex))
        {
            if (featureIndex >= 0 && featureIndex < allFeatures.Count)
            {
                targetFeature = allFeatures[featureIndex];
                Console.WriteLine($"Selected feature by index [{featureIndex}]: {targetFeature}");
            }
            else
            {
                Console.WriteLine($"ERROR: Feature index {featureIndex} out of range (0-{allFeatures.Count - 1})");
                System.Environment.Exit(1);
            }
        }

        if (string.IsNullOrEmpty(targetFeature) && location != "end")
        {
            Console.WriteLine("ERROR: Target feature required (use index number or feature name)");
            Console.WriteLine("Usage: MoveRollbackBar <feature_name_or_index> <before|after|end>");
            System.Environment.Exit(1);
        }

        Console.WriteLine($"Moving Rollback Bar to {location} '{targetFeature}'");
        Console.WriteLine();

        // Map location string to enum value
        int locationValue = location.ToLower() switch
        {
            "before" => 3,      // swMoveRollbackBarToBeforeFeature
            "after" => 4,       // swMoveRollbackBarToAfterFeature
            "end" => 1,         // swMoveRollbackBarToEnd
            "previous" => 2,    // swMoveRollbackBarToPreviousPosition
            _ => 3              // default to "before"
        };

        IFeatureManager fm = swDoc.FeatureManager;
        bool result = fm.EditRollback(locationValue, targetFeature);

        // Allow UI to refresh
        System.Windows.Forms.Application.DoEvents();
        System.Threading.Thread.Sleep(500);

        if (result)
        {
            Console.WriteLine($"✓ SUCCESS: Rollback Bar moved to {location} {targetFeature}");
        }
        else
        {
            Console.WriteLine($"✗ FAILED: Could not move Rollback Bar to {location} {targetFeature}");
            System.Environment.Exit(1);
        }
    }

    private static List<string> GetAllFeatures(ModelDoc2 swDoc)
    {
        var features = new List<string>();

        try
        {
            var feat = swDoc.FirstFeature();
            while (feat != null)
            {
                features.Add(feat.Name);
                feat = (Feature?)feat.GetNextFeature();
            }
        }
        catch
        {
            Console.WriteLine("Could not enumerate features");
        }

        return features;
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

        var type = Type.GetTypeFromProgID("SolidWorks.Application");
        return type == null ? null : (SldWorks?)Activator.CreateInstance(type);
    }
}
