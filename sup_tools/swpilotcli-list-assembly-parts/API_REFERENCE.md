# ListAssemblyParts API Reference

## Arguments

None. Uses the active SolidWorks document.

## Output

```
Assembly: <full path or title>
Unique parts: N

[
  "C:\\path\\to\\PartA.sldprt",
  "C:\\path\\to\\PartB.sldprt"
]
```

JSON array is sorted alphabetically by full path.

## Return Codes

| Code | Meaning |
|------|---------|
| 0 | Success |
| 1 | Unhandled exception |
| 2 | Cannot attach to SolidWorks |
| 3 | No active document |
| COM code | COM interop error |

## SolidWorks API Used

- `IAssemblyDoc.GetComponents(topLevelOnly: false)`
- `IComponent2.GetPathName()`
