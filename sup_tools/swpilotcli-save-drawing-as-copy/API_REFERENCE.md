# API Reference — swpilotcli-save-drawing-as-copy

## Command

```bash
dotnet run --project ./sup_tools/swpilotcli-save-drawing-as-copy/scripts/SaveDrawingAsCopy/SaveDrawingAsCopy.csproj -- <sourcePath> <outputPath>
```

## Arguments

| Position | Name | Required | Description |
|----------|------|----------|-------------|
| 1 | sourcePath | Yes | Full path to the source `.slddrw` file |
| 2 | outputPath | Yes | Full path for the output `.slddrw` copy |

## Exit Codes

| Code | Meaning |
|------|---------|
| `0` | Success |
| `1` | Missing arguments or unexpected error |
| `2` | Cannot connect to SolidWorks |
| `4` | Cannot open source file |
| `5` | SaveAs3 failed |

## SolidWorks APIs used

| API | Purpose |
|-----|---------|
| `ISldWorks.OpenDoc6(path, swDocDRAWING, swOpenDocOptions_Silent, ...)` | Open source drawing silently |
| `IModelDocExtension.SaveAs3(outputPath, 0, Silent\|Copy, null, null, errors, warnings)` | Save a copy without changing document path |

## Key flag

`swSaveAsOptions_Copy` — saves to the new path as a copy; the in-memory document retains its original path. Without this flag, `SaveAs3` on a `.slddrw` acts as "Save As" and permanently changes the document's path.
