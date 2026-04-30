# swpilotcli-eye-screenshot

Take a screenshot for visual analysis by Claude. Part of the `swpilotcli-eye-*` vision toolkit.

## Directory Structure

```
swpilotcli-eye-screenshot/
├── SKILL.md
├── API_REFERENCE.md
├── README.md
└── scripts/
    └── EyeScreenshot/
        ├── EyeScreenshot.csproj
        └── Program.cs
```

## Quick Start

```bash
# Full screen
dotnet run --project ./scripts/EyeScreenshot/EyeScreenshot.csproj

# SolidWorks window
dotnet run --project ./scripts/EyeScreenshot/EyeScreenshot.csproj -- sw
```

## eye-* Vision Toolkit

| Tool | Purpose |
|------|---------|
| `swpilotcli-eye-screenshot` | Capture screen (this tool) |
| _(future)_ `swpilotcli-eye-ocr` | Extract text from screen |
| _(future)_ `swpilotcli-eye-diff` | Compare two screenshots |

## Migration to Agent Skills

```bash
cp -r ./main_tools/swpilotcli-eye-screenshot ~/.claude/skills/
```
