# ExportPartToXt API Reference

## Arguments

| Position | Value | Default |
|----------|-------|---------|
| `args[0]` | Full path to `.sldprt` | Active document |
| `args[1]` | Output directory | Same directory as part |

## Output

```
Part:   <full part path>
Output: <full output path>
OK -> <full output path>
```

## Return Codes

| Code | Meaning |
|------|---------|
| 0 | Success |
| 1 | Unhandled exception |
| 2 | Cannot attach to SolidWorks |
| 3 | No active document |
| 4 | Active document is not a part |
| 5 | Active part not saved yet |
| 6 | Specified file not found |
| COM code | COM interop error |

## SolidWorks API Used

- `ISldWorks.OpenDoc6()` — `swOpenDocOptions_Silent`
- `IModelDocExtension.SaveAs3()` — `swSaveAsCurrentVersion`, `swSaveAsOptions_Silent`
- `ISldWorks.CloseDoc()`
