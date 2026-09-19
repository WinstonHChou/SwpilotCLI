# swpilotcli-batch-xt-to-step

Batch convert Parasolid `.x_t` and `.xt` files in a folder to STEP `.step`.

## Directory Structure

```text
swpilotcli-batch-xt-to-step/
├── SKILL.md
├── API_REFERENCE.md
├── README.md
└── scripts/
    └── BatchXtToStep/
        ├── BatchXtToStep.csproj
        └── Program.cs
```

## Quick Start

```bash
dotnet run --project ./scripts/BatchXtToStep/BatchXtToStep.csproj -- "C:\path\to\folder"
```

## Related Tools

| Tool | Use case |
|------|----------|
| `swpilotcli-export-part-to-xt` | Export one SolidWorks part to XT |
| `swpilotcli-export-assembly-parts-to-xt` | Export all parts in an assembly to XT |
| `swpilotcli-batch-xt-to-step` | Convert a folder of XT files to STEP |

## Migration to Agent Skills

```bash
# Pi Coding Agent
cp -r ./sup_tools/swpilotcli-batch-xt-to-step ~/.pi/skills/

# Claude Code
cp -r ./sup_tools/swpilotcli-batch-xt-to-step ~/.claude/skills/
```
