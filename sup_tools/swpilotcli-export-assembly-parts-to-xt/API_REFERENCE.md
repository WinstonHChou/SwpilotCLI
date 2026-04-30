# ExportAssemblyPartsToXt API Reference

## Arguments

None. Uses the active SolidWorks document.

## Output

Console lines per part:

```
OK  <source filename> -> <output filename>
SKIP missing file: <path>
FAIL open: <path> (openErrors=N, openWarnings=N)
FAIL save: <path> (saveErrors=N, saveWarnings=N)
```

Summary line:

```
Summary: exported=N, skipped=N, failed=N
```

## Return Codes

| Code | Meaning |
|------|---------|
| 0 | Success |
| 1 | Unhandled exception |
| 2 | Cannot attach to SolidWorks |
| 3 | No active document |
| COM code | COM interop error |

## Output Location

`<assembly_directory>/xt/<partname>.x_t`

Duplicate base names are suffixed: `PartA_2.x_t`, `PartA_3.x_t`, etc.

## SolidWorks API Used

- `IAssemblyDoc.GetComponents(topLevelOnly: false)`
- `ISldWorks.OpenDoc6()` — `swOpenDocOptions_Silent`
- `IModelDocExtension.SaveAs3()` — `swSaveAsCurrentVersion`, `swSaveAsOptions_Silent`
- `ISldWorks.CloseDoc()`
