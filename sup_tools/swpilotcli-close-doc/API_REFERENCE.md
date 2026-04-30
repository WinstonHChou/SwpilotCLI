# API Reference — swpilotcli-close-doc

## Command

```bash
dotnet run --project ./sup_tools/swpilotcli-close-doc/scripts/CloseDoc/CloseDoc.csproj [mode]
```

## Arguments

| Position | Name | Required | Default | Description |
|----------|------|----------|---------|-------------|
| 1 | mode | No | `nosave` | `nosave` / `save` / `all` |

### Mode details

| Mode | Behaviour |
|------|-----------|
| `nosave` | Close active document without saving, no dialog |
| `save` | Silently save active document then close; if no path, forces close without saving |
| `all` | Close every open document including unsaved ones |

## Exit Codes

| Code | Meaning |
|------|---------|
| `0` | Success |
| `1` | Unexpected error |
| `2` | Cannot connect to SolidWorks |
| `3` | No active document |

## SolidWorks APIs used

| API | Purpose |
|-----|---------|
| `ISldWorks.IActiveDoc2` | Get active document |
| `IModelDoc2.GetTitle()` | Get document display name |
| `IModelDoc2.GetPathName()` | Get document file path |
| `IModelDoc2.Save3(swSaveAsOptions_Silent, errors, warnings)` | Silently save document |
| `ISldWorks.CloseDoc(path)` | Close specific document without dialog |
| `ISldWorks.CloseAllDocuments(true)` | Close all open documents including unsaved |
