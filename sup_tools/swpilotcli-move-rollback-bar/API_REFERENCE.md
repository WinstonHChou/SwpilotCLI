# API Reference - Move Rollback Bar

## SolidWorks APIs Used

### IFeatureManager.EditRollback

**Purpose**: Moves the rollback bar in the Feature Manager tree to control which features are active in the model.

**Syntax**:
```csharp
bool result = featureManager.EditRollback(int location, string featureName);
```

**Parameters**:
- `location` (int): Position to move the rollback bar
  - `1` = `swMoveRollbackBarToEnd` - Move to end (all features active)
  - `2` = `swMoveRollbackBarToPreviousPosition` - Restore previous position
  - `3` = `swMoveRollbackBarToBeforeFeature` - Move before specified feature
  - `4` = `swMoveRollbackBarToAfterFeature` - Move after specified feature

- `featureName` (string): Name of the feature to use as reference (required for locations 3 and 4)

**Returns**:
- `true` - Operation successful
- `false` - Operation failed (feature not found or invalid parameter)

**Example**:
```csharp
IFeatureManager fm = swDoc.FeatureManager;

// Move rollback bar before "Sketch1"
bool success = fm.EditRollback(3, "Sketch1");

// Move rollback bar to end (unsuppress all features)
bool success = fm.EditRollback(1, "");
```

## Related Enums

### swMoveRollbackBarTo_e

```csharp
enum swMoveRollbackBarTo_e
{
    swMoveRollbackBarToEnd = 1,
    swMoveRollbackBarToPreviousPosition = 2,
    swMoveRollbackBarToBeforeFeature = 3,
    swMoveRollbackBarToAfterFeature = 4
}
```

## Return Behavior

- When rollback bar is successfully moved, all features after it (if moving before/after) are suppressed
- The model automatically regenerates after the rollback bar position changes
- Feature sketches and references after the rollback bar remain intact but not computed

## Error Handling

The tool provides these error codes:
- `0` - Success
- `1` - Failed to move rollback bar (likely feature not found)
- `2` - SolidWorks connection error
- `3` - No active document

## Limitations

- Feature names are case-sensitive
- The target feature must exist in the model
- Cannot move rollback bar to suppress the Origin feature
- Some derived features may not be valid rollback targets
