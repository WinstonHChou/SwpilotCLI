# swpilotcli-export-part-to-xt

Export a single SolidWorks part (.sldprt) to Parasolid (.x_t).

## Directory Structure

```
swpilotcli-export-part-to-xt/
├── SKILL.md
├── API_REFERENCE.md
├── README.md
└── scripts/
    └── ExportPartToXt/
        ├── ExportPartToXt.csproj
        └── Program.cs
```

## Quick Start

```bash
# Active part
dotnet run --project ./scripts/ExportPartToXt/ExportPartToXt.csproj

# Specified part
dotnet run --project ./scripts/ExportPartToXt/ExportPartToXt.csproj -- "C:\path\to\part.sldprt"
```

## Related Tools

| Tool | Use case |
|------|----------|
| `swpilotcli-list-assembly-parts` | List parts first |
| `swpilotcli-export-part-to-xt` | Export single part (this tool) |
| `swpilotcli-export-assembly-parts-to-xt` | Batch export all parts |

## Migration to Agent Skills

```bash
# Pi Coding Agent
cp -r ./sup_tools/swpilotcli-export-part-to-xt ~/.pi/skills/

# Claude Code
cp -r ./sup_tools/swpilotcli-export-part-to-xt ~/.claude/skills/
```
