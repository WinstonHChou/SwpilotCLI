using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

class Program
{
    static int Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        string mode = args.Length > 0 ? args[0].ToLower() : "install";

        if (mode == "uninstall")
        {
            PrintUninstallInstructions();
            return 0;
        }

        Console.WriteLine("=== SwpilotCLI Installer ===");
        Console.WriteLine();

        // Step 1: Detect SolidWorks
        string interopDllPath = FindSolidWorksInteropDll();
        if (interopDllPath == null)
            return 1;

        // Step 2: Check MSBuild
        if (!CheckMsBuild())
            return 1;

        // Step 3: Build
        // AppContext.BaseDirectory is bin\Debug\net8.0\ when running with dotnet run,
        // so go up 3 levels to reach the tool root directory (where src\ lives).
        string toolRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", ".."));
        string csprojPath = Path.Combine(toolRoot, "src", "SwpilotCLIAddin.csproj");
        if (!File.Exists(csprojPath))
        {
            Console.Error.WriteLine($"ERROR: csproj not found at: {csprojPath}");
            return 1;
        }

        bool buildOk = RunBuild(csprojPath, interopDllPath);
        if (!buildOk)
            return 1;

        // Step 4: Registration instructions
        PrintRegistrationInstructions(csprojPath);

        return 0;
    }

    static string FindSolidWorksInteropDll()
    {
        Console.WriteLine("[1/3] Detecting SolidWorks installation...");

        var versions = new List<(int year, string folder)>();

        try
        {
            using var swKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\SolidWorks");
            if (swKey == null)
            {
                Console.Error.WriteLine("ERROR: SolidWorks registry key not found. Please install SolidWorks first.");
                return null;
            }

            foreach (string subKeyName in swKey.GetSubKeyNames())
            {
                if (!subKeyName.StartsWith("SolidWorks ", StringComparison.OrdinalIgnoreCase))
                    continue;

                string yearStr = subKeyName.Substring("SolidWorks ".Length).Trim();
                if (!int.TryParse(yearStr, out int year))
                    continue;

                using var setupKey = swKey.OpenSubKey($@"{subKeyName}\Setup");
                if (setupKey == null) continue;

                string swFolder = setupKey.GetValue("SolidWorks Folder") as string;
                if (string.IsNullOrEmpty(swFolder)) continue;

                string interopPath = Path.Combine(swFolder, "SolidWorks.Interop.sldworks.dll");
                if (File.Exists(interopPath))
                    versions.Add((year, interopPath));
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"ERROR reading registry: {ex.Message}");
            return null;
        }

        if (versions.Count == 0)
        {
            Console.Error.WriteLine("ERROR: No SolidWorks installation found with Interop DLL.");
            return null;
        }

        if (versions.Count == 1)
        {
            Console.WriteLine($"  Found: SolidWorks {versions[0].year}");
            Console.WriteLine($"  Interop DLL: {versions[0].folder}");
            Console.WriteLine();
            return versions[0].folder;
        }

        // Multiple versions — let user choose
        Console.WriteLine("  Multiple SolidWorks versions found:");
        for (int i = 0; i < versions.Count; i++)
            Console.WriteLine($"  [{i + 1}] SolidWorks {versions[i].year}");

        Console.Write("  Choose version [1-{0}]: ", versions.Count);
        string input = Console.ReadLine()?.Trim();
        if (int.TryParse(input, out int choice) && choice >= 1 && choice <= versions.Count)
        {
            var selected = versions[choice - 1];
            Console.WriteLine($"  Selected: SolidWorks {selected.year}");
            Console.WriteLine($"  Interop DLL: {selected.folder}");
            Console.WriteLine();
            return selected.folder;
        }

        Console.Error.WriteLine("ERROR: Invalid selection.");
        return null;
    }

    static bool CheckMsBuild()
    {
        Console.WriteLine("[2/3] Checking MSBuild...");
        try
        {
            var psi = new ProcessStartInfo("dotnet", "msbuild --version")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var p = Process.Start(psi);
            string output = p.StandardOutput.ReadToEnd();
            p.WaitForExit();
            if (p.ExitCode != 0)
            {
                Console.Error.WriteLine("ERROR: dotnet msbuild not available. Please install .NET SDK.");
                return false;
            }
            Console.WriteLine($"  MSBuild OK: {output.Trim().Split('\n')[0].Trim()}");
            Console.WriteLine();
            return true;
        }
        catch
        {
            Console.Error.WriteLine("ERROR: dotnet not found. Please install .NET SDK.");
            return false;
        }
    }

    static bool RunBuild(string csprojPath, string interopDllPath)
    {
        Console.WriteLine("[3/3] Building SwpilotCLIAddin.dll...");
        Console.WriteLine($"  Project: {csprojPath}");
        Console.WriteLine($"  SWInteropPath: {interopDllPath}");
        Console.WriteLine();

        string args = $"msbuild \"{csprojPath}\" -p:Configuration=Release -p:Platform=AnyCPU " +
                      $"-p:RunPostBuildEvent=Never -p:RegisterForComInterop=false " +
                      $"\"-p:SWInteropPath={interopDllPath}\"";

        var psi = new ProcessStartInfo("dotnet", args)
        {
            UseShellExecute = false,
            CreateNoWindow = false
        };

        using var p = Process.Start(psi);
        p.WaitForExit();

        if (p.ExitCode != 0)
        {
            Console.Error.WriteLine();
            Console.Error.WriteLine("ERROR: Build failed. See errors above.");
            return false;
        }

        // Find output DLL
        string binRelease = Path.Combine(Path.GetDirectoryName(csprojPath), "bin", "Release", "SwpilotCLIAddin.dll");
        Console.WriteLine();
        if (File.Exists(binRelease))
            Console.WriteLine($"  Build succeeded: {binRelease}");
        else
            Console.WriteLine("  Build succeeded.");

        Console.WriteLine();
        return true;
    }

    static void PrintRegistrationInstructions(string csprojPath)
    {
        string toolRoot2 = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", ".."));
        string manageBat = Path.Combine(toolRoot2, "Manage.bat");

        Console.WriteLine("=== Next Step: Register DLL (requires Administrator) ===");
        Console.WriteLine();
        Console.WriteLine("AI cannot obtain administrator privileges automatically.");
        Console.WriteLine("Please run the following file as Administrator:");
        Console.WriteLine();
        Console.WriteLine($"  {manageBat}");
        Console.WriteLine();
        Console.WriteLine("  -> Choose option 3 (Reinstall)");
        Console.WriteLine();
    }

    static void PrintUninstallInstructions()
    {
        string toolRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", ".."));
        string manageBat = Path.Combine(toolRoot, "Manage.bat");
        Console.WriteLine("=== Uninstall SwpilotCLI ===");
        Console.WriteLine();
        Console.WriteLine("Please run the following file as Administrator:");
        Console.WriteLine($"  {manageBat}");
        Console.WriteLine("  -> Choose option 2 (Unregister)");
        Console.WriteLine();
    }
}
