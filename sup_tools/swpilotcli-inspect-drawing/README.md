# swpilotcli-inspect-drawing

Full attribute diagnostic tool for the active SolidWorks drawing. Dumps complete properties for every annotation and lists visible geometry vertex coordinates in both model and sheet space.

## Source

Graduated from `SolidWorksConsole/InspectDrawing/` after validation.

## Workflow position

```
CheckDanglingDims  →  InspectDrawing  →  RepairDanglingAI
  (find problems)     (deep audit)        (fix problems)
```

## Development notes

- `INote.GetText()` must be called as a method — `.Text` property throws with EmbedInteropTypes
- `IView.GetFirstAnnotation3()` is required to enumerate dangling annotations; standard `GetAnnotations()` skips them
- `ModelToViewTransform` + `IMathPoint.MultiplyTransform()` converts 3D model coords to 2D sheet coords
- Read-only: no document is modified
