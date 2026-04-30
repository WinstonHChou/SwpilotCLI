# API Reference — swpilotcli-inspect-drawing

## Command

```bash
dotnet run --project ./sup_tools/swpilotcli-inspect-drawing/scripts/InspectDrawing/InspectDrawing.csproj
```

## Arguments

None. Always operates on the currently active SolidWorks drawing.

## Exit Codes

| Code | Meaning |
|------|---------|
| `0` | Completed successfully |
| `2` | Cannot connect to SolidWorks |
| `3` | No active document in SolidWorks |
| `1` | Unexpected error |

## Output sections per view

### DisplayDimension block

```
  [v正常|!懸空] DisplayDimension
    名稱       : <FullName>
    數值       : <value> mm
    類型       : <Type2 int>
    文字位置   : (<x>, <y>) mm
    文字[Prefix]: "<text>"          ← only shown if non-empty
    文字[Suffix]: "<text>"
    文字[CalloutAbove]: "<text>"
    文字[CalloutBelow]: "<text>"
    文字[PrefixDef]: "<text>"
    文字[SuffixDef]: "<text>"
    文字[CalloutAboveDef]: "<text>"
    文字[CalloutBelowDef]: "<text>"
    公差Type   : <int>              ← 0=none, 1=symmetric, 2=bilateral, etc.
    公差Max    : +<value> mm
    公差Min    : <value> mm
    精度Primary: <int>              ← -2=follow doc, positive=decimal places
    精度TolPri : <int>
    箭頭樣式   : <int>
    圖層       : "<layer name>"
    顏色(ARGB) : 0x<hex>
    可見       : True|False
```

### Note block

```
  [v正常|!懸空] Note
    文字位置   : (<x>, <y>) mm
    文字內容   : "<text>"
    引線附著點 : (<x>, <y>, <z>) mm
    箭頭數量   : <int>
    箭頭樣式   : <int>  大小: <w>x<h> mm
    角度       : <degrees>°
    圖層       : "<layer name>"
    顏色(ARGB) : 0x<hex>
```

### Geometry vertices

```
  P<index>[V] 模型=(<mx>,<my>,<mz>)  圖紙=(<dx>,<dy>) mm
```

All coordinates in mm. Model coordinates are 3D; sheet coordinates are 2D (Z ignored).

## SolidWorks APIs used

| API | Purpose |
|-----|---------|
| `IDrawingDoc.GetViews()` | Enumerate sheets and views |
| `IView.GetFirstAnnotation3()` / `GetNext3()` | Iterate all annotations |
| `IAnnotation.IsDangling()` | Dangling status |
| `IAnnotation.GetPosition()` | Text anchor position |
| `IAnnotation.Layer` / `.Color` / `.Visible` | Annotation properties |
| `IDisplayDimension.GetText(part)` | Text parts 1–8 |
| `IDisplayDimension.Type2` | Dimension type code |
| `IDisplayDimension.GetPrimaryPrecision2()` | Primary precision |
| `IDisplayDimension.GetPrimaryTolPrecision2()` | Tolerance precision |
| `IDisplayDimension.GetArrowHeadStyle()` | Arrow style |
| `IDimensionTolerance.Type` / `GetMaxValue()` / `GetMinValue()` | Tolerance |
| `INote.GetText()` | Note text (method, not property) |
| `INote.GetAttachPos()` | Leader attach point |
| `INote.GetArrowHeadCount()` / `GetArrowHeadInfo()` | Arrow info |
| `IView.GetVisibleEntities2()` | Visible geometry |
| `IVertex.GetPoint()` | Vertex 3D coordinates |
| `IView.ModelToViewTransform` | Coordinate space transform |
| `IMathPoint.MultiplyTransform()` | Apply transform |
