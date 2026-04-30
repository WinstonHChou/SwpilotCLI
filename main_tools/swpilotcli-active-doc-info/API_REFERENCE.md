# ActiveDocInfo API Reference

## Output Schema

The tool returns a JSON object with the following structure:

```json
{
  "filePath": "C:\\Users\\User\\Documents\\Part001.sldprt",
  "fileName": "Part001.sldprt",
  "documentType": "Part",
  "isSaved": true,
  "isModified": false,
  "isReadOnly": false,
  "activeConfigurationName": "Default",
  "configurations": ["Default", "Config2"],
  "componentCount": 0,
  "featuresCount": 5,
  "customProperties": {
    "Title": "Shaft Assembly",
    "Author": "John Doe"
  }
}
```

## Field Descriptions

| Field | Type | Description |
|-------|------|-------------|
| `filePath` | string | Full path to the document file |
| `fileName` | string | File name with extension |
| `documentType` | string | One of: "Part", "Assembly", "Drawing" |
| `isSaved` | boolean | Whether the document has unsaved changes |
| `isModified` | boolean | True if document was modified since last save |
| `isReadOnly` | boolean | True if document is read-only |
| `activeConfigurationName` | string | Currently active configuration name |
| `configurations` | array | List of all available configurations |
| `componentCount` | integer | Number of components (assemblies only) |
| `featuresCount` | integer | Number of features in the feature tree |
| `customProperties` | object | Custom document properties (key-value pairs) |

## Return Codes

- **0**: Success - document info retrieved
- **1**: Error - no active SolidWorks document
- **2**: Error - SolidWorks instance not found
- **3**: Error - COM error accessing document

## Usage Examples

### Check if document is saved
```bash
dotnet run --project ./scripts/ActiveDocInfo.csproj | grep "isSaved"
```

### Get file path
```bash
dotnet run --project ./scripts/ActiveDocInfo.csproj | grep "filePath"
```
