# BatchXtToStep API Reference

## Arguments

| Position | Value | Default |
|----------|-------|---------|
| `args[0]` | Full path to target folder | Required |

## Output

```text
Default part template: <part template path>
Folder: <folder path>
Found: <count> file(s)

Input:  <full input path>
Output: <full output path>
Status: OK

Summary: success=<n>, failed=<n>, total=<n>
```

## Return Codes

| Code | Meaning |
|------|---------|
| 0 | All files converted successfully |
| 1 | One or more files failed, or unhandled exception |
| 2 | Cannot attach to SolidWorks |
| 64 | Missing folder argument |
| 65 | Specified folder not found |
| 66 | No `.x_t` or `.xt` files found in folder |
| COM code | COM interop error |

## SolidWorks API Used

- `ISldWorks.NewDocument()`
- `IPartDoc.InsertImportedFeature()`
- `ISldWorks.ActivateDoc3()`
- `IModelDocExtension.SaveAs3()`
- `ISldWorks.CloseDoc()`
- `ISldWorks.SetUserPreferenceToggle()`
- `ISldWorks.GetUserPreferenceStringValue()`
- `ISldWorks.SetUserPreferenceStringValue()`
