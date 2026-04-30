# swpilotcli-save-drawing-as-copy

Save a SolidWorks drawing as a copy to a new path, without affecting the original document.

## Source

Graduated from `SolidWorksConsole/SaveDrawingAsCopy/` after validation.

## Development notes

- `SaveAs3` without `swSaveAsOptions_Copy` on a `.slddrw` file behaves like "File > Save As" — it changes the document's path in memory. The original filename is lost as the active document. Always use `Copy` flag when the intent is to duplicate.
- `swpilotcli-save-drawing-as` does NOT use the `Copy` flag, making it unsuitable for SLDDRW-to-SLDDRW copy operations. This tool exists specifically to fill that gap.
- If the source file is already open in SolidWorks, `OpenDoc6` reuses the existing document rather than opening a second instance.
