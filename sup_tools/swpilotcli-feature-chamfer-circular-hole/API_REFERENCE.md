# API Reference

## SolidWorks APIs Used

- `IPartDoc.GetBodies2(swBodyType_e.swSolidBody, true)` to retrieve visible solid bodies.
- `IBody2.GetEdges()` to traverse body edges.
- `IEdge.GetCurve()` before reading edge curve data.
- `ICurve.IsCircle()` and `ICurve.CircleParams` to identify circular edges and read center/radius.
- `IEdge.GetCurveParams2()` to filter out partial circular arcs.
- `IEntity.Select4(true, null)` to select target edge entities.
- `IModelDoc2.FeatureChamferType(...)` to create the chamfer feature.

## Enums

- `swBodyType_e.swSolidBody`
- `swChamferType_e.swChamferAngleDistance`

## Units

- Command arguments are in millimetres/degrees.
- SolidWorks API calls use metres/radians internally.
