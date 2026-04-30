# UndoOneStep API Reference

## Arguments

This tool takes no command-line arguments.

## Output

```
Active document: <path or title>
Undo target document: <path or title>
Undo executed: 1 step.
Task completed successfully.
```

## Return Codes

| Code | Meaning |
|------|---------|
| 0 | Success |
| 1 | Unhandled exception |
| 2 | Cannot attach to SolidWorks |
| 3 | No active document |
| COM code | COM interop error |

## SolidWorks API Used

- `IModelDoc2.EditUndo2(1)`
- `IModelDoc2.GraphicsRedraw2()`
