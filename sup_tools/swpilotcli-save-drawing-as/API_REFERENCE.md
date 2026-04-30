# API Reference — swpilotcli-save-drawing-as

## Command

```bash
dotnet run --project ./sup_tools/swpilotcli-save-drawing-as/scripts/SaveDrawingAs/SaveDrawingAs.csproj [format] [outputPath]
```

## Arguments

| Position | Name | Required | Default | Description |
|----------|------|----------|---------|-------------|
| 1 | format | No | `pdf` | Target format (see table below) |
| 2 | outputPath | No | Same dir as drawing | Full path for the output file |

### Supported formats

`pdf`, `png`, `jpg`, `jpeg`, `tif`, `tiff`, `dwg`, `dxf`, `ai`, `psd`, `edrw`, `html`, `slddrw`

## Exit Codes

| Code | Meaning |
|------|---------|
| `0` | Export completed successfully |
| `2` | Cannot connect to SolidWorks |
| `3` | No active document in SolidWorks |
| `1` | Unexpected error |

## SolidWorks APIs used

| API | Purpose |
|-----|---------|
| `IModelDoc2.GetPathName()` | Get drawing file path for output directory |
| `ISldWorks.GetExportFileData(swExportPdfData)` | Create ExportPdfData for PDF export |
| `IExportPdfData.SetSheets(swExportData_ExportAllSheets, null)` | Export all sheets |
| `IExportPdfData.ViewPdfAfterSaving` | Disable auto-open after export |
| `IModelDocExtension.SaveAs3(path, 0, Silent, exportData, null, errors, warnings)` | Perform the export |
