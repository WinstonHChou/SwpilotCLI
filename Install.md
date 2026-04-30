# SwpilotCLI Installation Guide

> This document is for AI assistants (Claude Code, Codex, etc.) to follow when performing installation.
> When the user says "install", "setup", or "reinstall SwpilotCLI", follow the steps below.

---

## Pre-Installation Checks

Verify the following conditions in order. Stop and notify the user if any check fails.

### 1. Confirm SolidWorks is installed
Read the registry to find the SolidWorks installation path:
```powershell
# List all installed SolidWorks versions
Get-ChildItem "HKLM:\SOFTWARE\SolidWorks" | Select-Object -ExpandProperty Name
```
- If no version found → notify the user to install SolidWorks first, stop.
- If multiple versions found → ask the user which version to use.

### 2. Get SolidWorks installation path and Interop DLL

Use the version key detected in Step 1 (e.g. `SOLIDWORKS 2025`). Extract the year dynamically — do not hardcode it:

```powershell
# $swVersion = the key name detected in Step 1, e.g. "SOLIDWORKS 2025"
$swPath = (Get-ItemProperty "HKLM:\SOFTWARE\SolidWorks\$swVersion\Setup" -ErrorAction SilentlyContinue)."SolidWorks Folder"
$interopDll = Join-Path $swPath "SolidWorks.Interop.sldworks.dll"
Test-Path $interopDll
```
- If DLL not found → notify the user the path is invalid, stop.

### 3. Confirm MSBuild is available
```powershell
dotnet msbuild --version
```
- If this fails → notify the user to install the .NET SDK.

### 4. Confirm PowerShell execution policy allows scripts
```powershell
Get-ExecutionPolicy -Scope CurrentUser
```
- If result is `Restricted` or `Undefined` → run the following (no admin required):
```powershell
Set-ExecutionPolicy -Scope CurrentUser -ExecutionPolicy RemoteSigned
```
- This is required for Claude and Codex CLI to run (both are installed as `.ps1` scripts via npm).

---

## Installation Steps

### Step 0: Copy dependency DLLs from SolidWorks installation folder

The following three DLLs must be copied from the user's local SolidWorks installation folder. Never use DLLs from another machine or version.

```powershell
$src = $swPath  # path obtained from pre-installation check
$dst = "<working-dir>\Install\swpilotcli-install\src"

Copy-Item "$src\SolidWorks.Interop.swconst.dll"     "$dst\SolidWorks.Interop.swconst.dll"     -Force
Copy-Item "$src\SolidWorks.Interop.swpublished.dll" "$dst\SolidWorks.Interop.swpublished.dll" -Force
Copy-Item "$src\solidworkstools.dll"                "$dst\solidworkstools.dll"                -Force
```

- If any file is missing → notify the user the SolidWorks installation may be incomplete, stop.

---

### Step 1: Build SwpilotCLIAddin.dll

Project structure:
- csproj is at `Install\swpilotcli-install\src\SwpilotCLIAddin.csproj`
- Manage.bat expects the DLL at `Install\swpilotcli-install\bin\Release\`

Build using the user's local SolidWorks Interop DLL (note: Platform is `AnyCPU` with no space):

```powershell
cd "<working-dir>\Install\swpilotcli-install\src"
dotnet msbuild SwpilotCLIAddin.csproj `
  -p:Configuration=Release `
  -p:Platform=AnyCPU `
  -p:RunPostBuildEvent=Never `
  -p:RegisterForComInterop=false `
  "-p:SWInteropPath=$interopDll"
```

> **Note**: SolidWorks must be closed before building, otherwise the DLL will be locked.
> If the build fails, show the error and stop.

**Success indicator**: the last line of output contains `SwpilotCLIAddin.dll`

---

### Step 1b: Copy dependency files to bin\Release\

After building, the dependencies are not copied automatically. Run:

```powershell
$src = "<working-dir>\Install\swpilotcli-install\src"
$dst = "<working-dir>\Install\swpilotcli-install\bin\Release"
New-Item -ItemType Directory -Force -Path $dst | Out-Null

# Copy main addin DLL
Copy-Item "$src\bin\Release\SwpilotCLIAddin.dll"              "$dst\SwpilotCLIAddin.dll"                -Force

# Copy DLL dependencies
Copy-Item "$src\solidworkstools.dll"                          "$dst\solidworkstools.dll"                -Force
Copy-Item "$src\lib\WebView2\Microsoft.Web.WebView2.Core.dll" "$dst\Microsoft.Web.WebView2.Core.dll"    -Force
Copy-Item "$src\lib\WebView2\Microsoft.Web.WebView2.WinForms.dll" "$dst\Microsoft.Web.WebView2.WinForms.dll" -Force
Copy-Item "$src\lib\WebView2\WebView2Loader.dll"              "$dst\WebView2Loader.dll"                 -Force

# Copy web assets
if (Test-Path "$dst\web") { Remove-Item "$dst\web" -Recurse -Force }
Copy-Item "$src\web" "$dst\web" -Recurse -Force
```

Confirm `bin\Release\` contains the following before continuing:
- `SwpilotCLIAddin.dll`
- `solidworkstools.dll`
- `Microsoft.Web.WebView2.Core.dll`
- `Microsoft.Web.WebView2.WinForms.dll`
- `WebView2Loader.dll`
- `web\` folder (with xterm subdirectory)

---

### Step 2: Register the DLL (requires admin privileges)

Run the following command. Windows will show a UAC prompt — ask the user to click "Yes":

```powershell
Start-Process "<working-dir>\Install\swpilotcli-install\Reinstall.bat"
```

> `Reinstall.bat` triggers UAC elevation automatically. The user does not need to manually run anything as administrator.
> The window should end with `[OK] Reinstall succeeded.`

> **Note**: If a dialog appears saying "Cannot delete a subkey tree because the subkey does not exist",
> this is normal (no previous version was registered). Click OK and continue.

Wait for the user to confirm they see `[OK] Reinstall succeeded.` before continuing.

---

### Step 3: Configure swapi-pilot MCP (Claude Code CLI)

> swapi-pilot MCP is an MCP server designed for the SolidWorks API.
> See: https://github.com/arthurle3210/swapi-pilot-solidworks-mcp

> **Important**: Claude Code CLI MCP servers must be configured via the `claude mcp add` command.
> This writes to the top-level `mcpServers` in `~/.claude.json`.
> Do **not** manually edit `~/.claude/settings.json` — that file is not for MCP configuration.

#### Check if already installed

```bash
claude mcp list
```

- If output contains `swapi-pilot` → **skip this step**, mark MCP status as ✅ (already configured)
- If not → continue below

#### Add swapi-pilot

```bash
claude mcp add --transport http swapi-pilot https://swapi-pilot.com/mcp --scope user
```

- `--scope user`: global setting, applies to all projects
- On success: `Added HTTP MCP server swapi-pilot ...` is printed and `~/.claude.json` is updated

Restart Claude Code and confirm `swapi-pilot` appears in `/mcp`.

#### Remove (if needed)

```bash
claude mcp remove swapi-pilot --scope user
```

---

### Step 3b: Configure swapi-pilot MCP (Codex CLI)

> Codex CLI uses its own MCP configuration, separate from Claude Code CLI.
> For Codex, manage MCP servers with `codex mcp ...`.

#### Check if already installed

```bash
codex mcp list
```

- If output contains `swapi-pilot`, skip this step.

#### Add swapi-pilot

```bash
codex mcp add swapi-pilot --url https://swapi-pilot.com/mcp
```

- This adds the MCP server to Codex's config.
- Codex stores MCP configuration in `~/.codex/config.toml`.

#### Verify

```bash
codex mcp get swapi-pilot
```

or:

```bash
codex mcp list
```

- Confirm that `swapi-pilot` appears in the configured MCP servers.

#### Remove (if needed)

```bash
codex mcp remove swapi-pilot
```

---

## Installation Summary

After completion, output the following summary to the user:

```
## Installation Result

| Step | Status | Notes |
|------|--------|-------|
| SolidWorks detected | ✅ / ❌ | Version: 20XX |
| Build DLL | ✅ / ❌ | |
| Register DLL | ✅ / ❌ | confirmed by user |
| swapi-pilot MCP | ✅ / ❌ | |
```

---

## Uninstall

When the user says "uninstall" or "remove SwpilotCLI":

1. Run the following — ask the user to click "Yes" on the UAC prompt:
   ```powershell
   Start-Process "<working-dir>\Install\swpilotcli-install\Unregister.bat"
   ```
2. Remove swapi-pilot MCP:
   ```bash
   claude mcp remove swapi-pilot --scope user
   ```
