# swpilotcli-close-doc

Close the active SolidWorks document with optional save, or close all open documents at once.

## Source

Graduated from `SolidWorksConsole/CloseActiveDoc/` after validation.

## Development notes

- `CloseDoc("")` (empty string) did NOT reliably close the active document in testing — use `CloseDoc(path)` with the explicit file path instead
- `CloseAllDocuments(true)` is the correct API for "close all"; looping `CloseDoc` on the same file does not work because the document reference stays valid until SolidWorks internally processes the close
- For `save` mode: `Save3` with `swSaveAsOptions_Silent` suppresses the save dialog; without Silent flag a UI prompt may appear
- If the document has never been saved (no path), `Save3` will fail — the tool detects this and forces close without saving (Option A behaviour)
