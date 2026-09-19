# swpilotcli-list-assembly-parts

List all unique parts in the active SolidWorks assembly. Read-only.

## Directory Structure

```
swpilotcli-list-assembly-parts/
├── SKILL.md
├── API_REFERENCE.md
├── README.md
└── scripts/
    └── ListAssemblyParts/
        ├── ListAssemblyParts.csproj
        └── Program.cs
```

## Quick Start

```bash
dotnet run --project ./scripts/ListAssemblyParts/ListAssemblyParts.csproj
```

## Related Tools

| Tool | Use case |
|------|----------|
| `swpilotcli-list-assembly-parts` | List parts (this tool) |
| `swpilotcli-export-part-to-xt` | Export single part |
| `swpilotcli-export-assembly-parts-to-xt` | Batch export all parts |

## Migration to Agent Skills

```bash
# Pi Coding Agent
cp -r ./sup_tools/swpilotcli-list-assembly-parts ~/.pi/skills/

# Claude Code
cp -r ./sup_tools/swpilotcli-list-assembly-parts ~/.claude/skills/
```
