# swpilotcli-undo-one-step

Execute one SolidWorks undo action (Ctrl+Z equivalent).

## Directory Structure

```
swpilotcli-undo-one-step/
├── SKILL.md
├── API_REFERENCE.md
├── README.md
└── scripts/
    └── UndoOneStep/
        ├── SolidWorksConsole.csproj
        └── Program.cs
```

## Quick Start

```bash
dotnet run --project ./sup_tools/swpilotcli-undo-one-step/scripts/UndoOneStep/SolidWorksConsole.csproj
```

## Use Cases

- Revert the last operation after running a repair tool
- Provide a reliable one-step rollback command in workflows
- Mimic manual Ctrl+Z behavior from terminal automation

## Related Tools

| Tool | Use case |
|------|----------|
| `swpilotcli-eye-screenshot` | Verify visual state before/after undo |
| `CheckDangling` (SolidWorksConsole) | Verify dangling dimensions after undo |
| `InspectDrawing` (SolidWorksConsole) | Detailed post-undo diagnostics |
