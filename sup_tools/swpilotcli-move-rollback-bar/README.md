# Move Rollback Bar Tool

## Overview

This tool provides automated control over the SolidWorks Rollback Bar position via the C# API. It enables users to programmatically suppress features and manage the design history.

**Key Features:**
- List all features with indices for precise identification
- Move rollback bar by feature index (avoids translation issues)
- Move rollback bar by feature name
- Support for Chinese, English, and mixed feature names
- Real-time UI refresh after moving

## Development

### Compile the project

```bash
dotnet build ./scripts/MoveRollbackBar/MoveRollbackBar.csproj
```

### Run tests

```bash
dotnet run --project ./scripts/MoveRollbackBar/MoveRollbackBar.csproj -- Sketch1 before
```

## Architecture

- **Program.cs**: Main entry point, handles SolidWorks connection and COM cleanup
- **ExecuteTask()**: Contains the core logic for moving the rollback bar

## API Used

- `IFeatureManager.EditRollback()` - Moves the rollback bar
- Enum: `swMoveRollbackBarTo_e` - Location constants

## Troubleshooting

### Tool returns "FAILED" message

1. Check feature name is exact (case-sensitive)
2. Verify feature exists in the current document
3. Ensure SolidWorks is running and document is active

### COM Connection Error

- Ensure SolidWorks is running before executing the tool
- If running via SwpilotCLI, the connection is automatic

## Feedback

For issues or suggestions, refer to the main SwpilotCLI project documentation.
