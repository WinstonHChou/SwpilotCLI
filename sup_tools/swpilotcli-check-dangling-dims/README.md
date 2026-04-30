# swpilotcli-check-dangling-dims

Read-only diagnostic tool that scans all dimensions in the active SolidWorks drawing and reports which ones are dangling.

## Source

Graduated from `SolidWorksConsole/CheckDangling/` after validation.

## Workflow position

```
ExportDrawingPng  →  CheckDanglingDims  →  RepairDanglingAI
    (visual)           (find problems)       (fix problems)
```

## Development notes

- Uses `IView.GetFirstAnnotation3()` + `IAnnotation.IsDangling()` — the only reliable way to enumerate dangling annotations (standard `GetAnnotations()` skips dangling ones)
- Read-only: no SolidWorks document is modified
- Does not scan Notes or GD&T symbols, only `DisplayDimension` type annotations
