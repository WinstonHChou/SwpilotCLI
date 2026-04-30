# swpilotcli-export-assembly-parts-to-xt

Batch export all unique parts in the active SolidWorks assembly to Parasolid `.x_t`.

## Directory Structure

```
swpilotcli-export-assembly-parts-to-xt/
├── SKILL.md                              ← Skill definition
├── API_REFERENCE.md                      ← Output schema and return codes
├── README.md                             ← This file
└── scripts/
    └── ExportAssemblyPartsToXt/
        ├── ExportAssemblyPartsToXt.csproj
        └── Program.cs
```

## Quick Start

```bash
dotnet run --project ./scripts/ExportAssemblyPartsToXt/ExportAssemblyPartsToXt.csproj
```

## Related Tools

| Tool | Use case |
|------|----------|
| `ListAssemblyParts` | Preview parts before exporting |
| `ExportPartToXt` | Export a single part |
| `swpilotcli-export-assembly-parts-to-xt` | Batch export (this tool) |

## Migration to Agent Skills

Copy the entire directory to `~/.claude/skills/`:

```bash
cp -r ./main_tools/swpilotcli-export-assembly-parts-to-xt ~/.claude/skills/
```
