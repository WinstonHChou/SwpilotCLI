# API Reference — swpilotcli-check-dangling-dims

## Command

```bash
dotnet run --project ./sup_tools/swpilotcli-check-dangling-dims/scripts/CheckDanglingDims/CheckDanglingDims.csproj
```

## Arguments

None. Always operates on the currently active SolidWorks drawing.

## Exit Codes

| Code | Meaning |
|------|---------|
| `0` | Scan completed successfully (dangling dims may still exist) |
| `2` | Cannot connect to SolidWorks |
| `3` | No active document in SolidWorks |
| `1` | Unexpected error |

## Output Format

Each dimension is printed as one line:

```
  [v|!] [圖紙|視圖] <ViewName>  | <TypeName>  | <Value>  | <FullName>  | OK | *** DANGLING ***
```

Column meanings:

| Column | Description |
|--------|-------------|
| `[v]` / `[!]` | Normal / Dangling |
| `[圖紙]` / `[視圖]` | Sheet-level or view-level annotation |
| TypeName | 線性尺寸, 角度尺寸, 半徑尺寸, 直徑尺寸, etc. |
| Value | Converted to mm (or degrees for angular) |
| FullName | `DimName@FeatureName@PartName` |

## Summary block (end of output)

```
==============================================
檢查完成！共 N 個尺寸，其中 M 個為懸空尺寸。
```

If `M > 0`, a dangling list follows showing only the problematic dimensions.

## SolidWorks APIs used

| API | Purpose |
|-----|---------|
| `IDrawingDoc.GetViews()` | Enumerate all sheets and views |
| `IView.GetFirstAnnotation3()` / `GetNext3()` | Iterate annotations including dangling |
| `IAnnotation.IsDangling()` | Check dangling status |
| `IAnnotation.GetSpecificAnnotation()` | Cast to `DisplayDimension` |
| `IDisplayDimension.GetDimension2(0)` | Get underlying `Dimension` object |
| `IDimension.FullName` | Full dimension name string |
| `IDimension.SystemValue` | Dimension value in SI units (metres) |
| `IDisplayDimension.Type2` | Dimension type enum integer |
