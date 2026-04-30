# swpilotcli-save-drawing-as

Export the active SolidWorks drawing to another format. Output is saved in the same directory as the source drawing file.

## Source

Graduated from `SolidWorksConsole/ExportDrawingPng/` after redesign and validation.

## Development notes

- PDF export requires `ExportPdfData` object passed to `SaveAs3` — passing `null` causes `swGenericSaveError`
- If output file is already open (locked), `SaveAs3` returns `swGenericSaveError` (1) — close the file first
- Format is determined by file extension in the output path; `SaveAs3` handles all supported formats automatically
