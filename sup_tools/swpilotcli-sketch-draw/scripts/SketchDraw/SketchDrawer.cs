using System;
using System.Collections.Generic;
using System.Linq;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace SketchDraw;

// ── Entity context: holds created objects + key points ──────────────────────
class EntityCtx
{
    public EntityDef      Def { get; init; } = null!;
    public SketchSegment? Seg { get; set; }
    public SketchPoint?   StartPt  { get; set; }
    public SketchPoint?   EndPt    { get; set; }
    public SketchPoint?   CenterPt { get; set; }
    public SketchPoint?   MajorPt  { get; set; }
    public SketchPoint?   MinorPt  { get; set; }

    public SketchPoint? GetPoint(string sel) => sel.ToLower() switch
    {
        "start"  or "s" => StartPt,
        "end"    or "e" => EndPt,
        "center" or "c" => CenterPt,
        "major"         => MajorPt,
        "minor"         => MinorPt,
        _ => null
    };
}

// ── Main drawer ──────────────────────────────────────────────────────────────
class SketchDrawer
{
    private readonly ModelDoc2    _doc;
    private readonly SldWorks     _app;
    private readonly SketchManager _mgr;
    private readonly Dictionary<string, EntityCtx> _ctx = new();
    private const double EPS = 1e-6;

    public SketchDrawer(ModelDoc2 doc, SldWorks app)
    {
        _doc = doc;
        _app = app;
        _mgr = (SketchManager)doc.SketchManager;
    }

    public void Draw(SketchPlan plan)
    {
        CreateEntities(plan.Entities);
        AutoInferHV();
        AutoInferCoincident();
        if (plan.Anchor != null) ApplyAnchor(plan.Anchor);
        foreach (var rel in plan.Relations ?? []) ApplyRelation(rel);
        ApplyDimensions(plan.Dimensions ?? []);
    }

    // ── Entity creation ──────────────────────────────────────────────────────

    private void CreateEntities(List<EntityDef> entities)
    {
        foreach (var e in entities)
        {
            var ctx = new EntityCtx { Def = e };
            switch (e.Type.ToLower())
            {
                case "line":    CreateLine(ctx);    break;
                case "circle":  CreateCircle(ctx);  break;
                case "arc":     CreateArc(ctx);     break;
                case "ellipse": CreateEllipse(ctx); break;
                default: Console.WriteLine($"WARN: Unknown entity type '{e.Type}'"); continue;
            }
            _ctx[e.Id] = ctx;
            _doc.ClearSelection2(true);
            Console.WriteLine($"  {e.Type} '{e.Id}' created.");
        }
    }

    private void CreateLine(EntityCtx ctx)
    {
        var e = ctx.Def;
        double x1 = e.Start![0] / 1e3, y1 = e.Start[1] / 1e3;
        double x2 = e.End![0]   / 1e3, y2 = e.End[1]   / 1e3;
        var seg = (SketchSegment)_mgr.CreateLine(x1, y1, 0, x2, y2, 0);
        if (e.Construction) seg.ConstructionGeometry = true;
        ctx.Seg = seg;
        var ln = (SketchLine)(object)seg;
        ctx.StartPt = (SketchPoint)ln.GetStartPoint2();
        ctx.EndPt   = (SketchPoint)ln.GetEndPoint2();
    }

    private void CreateCircle(EntityCtx ctx)
    {
        var e = ctx.Def;
        double cx = e.Center![0] / 1e3, cy = e.Center[1] / 1e3;
        double r  = e.Radius!.Value / 1e3;
        var seg = (SketchSegment)_mgr.CreateCircleByRadius(cx, cy, 0, r);
        if (e.Construction) seg.ConstructionGeometry = true;
        ctx.Seg = seg;
        ctx.CenterPt = (SketchPoint)((SketchArc)(object)seg).GetCenterPoint2();
    }

    private void CreateArc(EntityCtx ctx)
    {
        var e = ctx.Def;
        double x1 = e.Start![0]   / 1e3, y1 = e.Start[1]   / 1e3;
        double x2 = e.End![0]     / 1e3, y2 = e.End[1]     / 1e3;
        double xm = e.Through![0] / 1e3, ym = e.Through[1] / 1e3;
        var seg = (SketchSegment)_mgr.Create3PointArc(x1, y1, 0, x2, y2, 0, xm, ym, 0);
        if (e.Construction) seg.ConstructionGeometry = true;
        ctx.Seg = seg;
        var arc = (SketchArc)(object)seg;
        ctx.StartPt  = (SketchPoint)arc.GetStartPoint2();
        ctx.EndPt    = (SketchPoint)arc.GetEndPoint2();
        ctx.CenterPt = (SketchPoint)arc.GetCenterPoint2();
    }

    private void CreateEllipse(EntityCtx ctx)
    {
        var e = ctx.Def;
        double cx = e.Center![0] / 1e3, cy = e.Center[1] / 1e3;
        double a  = e.SemiMajor!.Value / 1e3;
        double b  = e.SemiMinor!.Value / 1e3;
        bool horiz = (e.Orientation ?? "horizontal").ToLower() != "vertical";

        // tiny tilt so H/V orientation relations are not treated as redundant
        const double tilt = 0.001;
        double mx, my, nx, ny;
        if (horiz)
        {
            mx = cx + a * Math.Cos(tilt); my = cy + a * Math.Sin(tilt);
            nx = cx - b * Math.Sin(tilt); ny = cy + b * Math.Cos(tilt);
        }
        else
        {
            mx = cx + a * Math.Sin(tilt); my = cy + a * Math.Cos(tilt);
            nx = cx + b * Math.Cos(tilt); ny = cy - b * Math.Sin(tilt);
        }

        var seg = (SketchSegment)_mgr.CreateEllipse(cx, cy, 0, mx, my, 0, nx, ny, 0);
        if (e.Construction) seg.ConstructionGeometry = true;
        ctx.Seg = seg;
        RefreshEllipsePoints(ctx);
    }

    private void RefreshEllipsePoints(EntityCtx ctx)
    {
        var ell = (SketchEllipse)(object)ctx.Seg!;
        ctx.CenterPt = (SketchPoint)ell.GetCenterPoint2();
        ctx.MajorPt  = (SketchPoint)ell.GetMajorPoint2();
        ctx.MinorPt  = (SketchPoint)ell.GetMinorPoint2();
    }

    // ── Auto-infer H/V + ellipse orientation ────────────────────────────────

    private void AutoInferHV()
    {
        foreach (var (_, ctx) in _ctx)
        {
            switch (ctx.Def.Type.ToLower())
            {
                case "line":
                    var sp = ctx.StartPt!; var ep = ctx.EndPt!;
                    if (Math.Abs(sp.Y - ep.Y) < EPS)
                    { Sel(ctx.Seg!); _doc.SketchAddConstraints("sgHORIZONTAL"); Clear(); }
                    else if (Math.Abs(sp.X - ep.X) < EPS)
                    { Sel(ctx.Seg!); _doc.SketchAddConstraints("sgVERTICAL"); Clear(); }
                    break;

                case "ellipse":
                    RefreshEllipsePoints(ctx);
                    bool horiz = (ctx.Def.Orientation ?? "horizontal").ToLower() != "vertical";
                    // center + major/minor → H/V orientation
                    ctx.CenterPt!.Select4(false, null);
                    ctx.MajorPt!.Select4(true, null);
                    _doc.SketchAddConstraints(horiz ? "sgHORIZPOINTS" : "sgVERTPOINTS");
                    Clear();
                    RefreshEllipsePoints(ctx);
                    ctx.CenterPt!.Select4(false, null);
                    ctx.MinorPt!.Select4(true, null);
                    _doc.SketchAddConstraints(horiz ? "sgVERTPOINTS" : "sgHORIZPOINTS");
                    Clear();
                    break;
            }
        }
    }

    // ── Auto-infer COINCIDENT on shared start/end points ────────────────────

    private void AutoInferCoincident()
    {
        var pts = _ctx.Values
            .SelectMany(ctx => new[] { ctx.StartPt, ctx.EndPt })
            .Where(p => p != null)
            .Cast<SketchPoint>()
            .ToList();

        int n = 0;
        for (int i = 0; i < pts.Count; i++)
        for (int j = i + 1; j < pts.Count; j++)
        {
            double dx = pts[i].X - pts[j].X, dy = pts[i].Y - pts[j].Y;
            if (dx * dx + dy * dy < EPS * EPS)
            {
                pts[i].Select4(false, null);
                pts[j].Select4(true, null);
                _doc.SketchAddConstraints("sgCOINCIDENT");
                Clear();
                n++;
            }
        }
        if (n > 0) Console.WriteLine($"  Auto-inferred {n} coincident relations.");
    }

    // ── Anchor ───────────────────────────────────────────────────────────────

    private void ApplyAnchor(AnchorDef anchor)
    {
        var (entityId, selector) = ParseRef(anchor.Ref);
        if (!_ctx.TryGetValue(entityId, out var ctx))
        { Console.WriteLine($"WARN: Anchor entity '{entityId}' not found."); return; }

        if (ctx.Def.Type.ToLower() == "ellipse")
        { Console.WriteLine("  Ellipse anchor: position fixed by creation coords, skipping COINCIDENT."); return; }

        var pt = ctx.GetPoint(selector ?? "start");
        if (pt == null) { Console.WriteLine($"WARN: Cannot resolve anchor point '{anchor.Ref}'."); return; }

        bool oSel = _doc.Extension.SelectByID2("Point1@原點", "EXTSKETCHPOINT", 0, 0, 0, false, 0, null, 0);
        bool pSel = pt.Select4(true, null);
        if (oSel && pSel) { _doc.SketchAddConstraints("sgCOINCIDENT"); Console.WriteLine($"  Anchor '{anchor.Ref}' → origin OK."); }
        else Console.WriteLine($"  WARN: Anchor selection failed (origin={oSel}, pt={pSel}).");
        Clear();
    }

    // ── Explicit relations ───────────────────────────────────────────────────

    private void ApplyRelation(RelationDef rel)
    {
        bool allPointRefs = rel.Refs.All(r => r.Contains('.'));
        string sg = rel.Type.ToLower() switch
        {
            "tangent"       => "sgTANGENT",
            "parallel"      => "sgPARALLEL",
            "perpendicular" => "sgPERPENDICULAR",
            "equal"         => "sgSAMELENGTH",
            "concentric"    => "sgCONCENTRIC",
            "coincident"    => "sgCOINCIDENT",
            "collinear"     => "sgCOLINEAR",
            "symmetric"     => "sgSYMMETRIC",
            "fixed"         => "sgFIXED",
            "horizontal"    => allPointRefs ? "sgHORIZPOINTS" : "sgHORIZONTAL",
            "vertical"      => allPointRefs ? "sgVERTPOINTS"  : "sgVERTICAL",
            _ => ""
        };
        if (sg == "") { Console.WriteLine($"WARN: Unknown relation type '{rel.Type}'."); return; }

        bool first = true;
        foreach (var r in rel.Refs)
        {
            var (id, sel) = ParseRef(r);
            if (!_ctx.TryGetValue(id, out var ctx)) { Console.WriteLine($"WARN: Relation entity '{id}' not found."); Clear(); return; }
            bool ok = sel != null
                ? (ctx.GetPoint(sel)?.Select4(!first, null) ?? false)
                : (ctx.Seg?.Select4(!first, null) ?? false);
            if (!ok) { Console.WriteLine($"WARN: Select failed for '{r}'."); Clear(); return; }
            first = false;
        }
        _doc.SketchAddConstraints(sg);
        Console.WriteLine($"  Relation '{rel.Type}' applied.");
        Clear();
    }

    // ── Dimensions ───────────────────────────────────────────────────────────

    private void ApplyDimensions(List<DimensionDef> dims)
    {
        _app.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swInputDimValOnCreate, false);
        foreach (var dim in dims) ApplyOneDim(dim);
        _app.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swInputDimValOnCreate, true);
    }

    private void ApplyOneDim(DimensionDef dim)
    {
        double lx, ly;

        if (dim.Type is "semi_major" or "semi_minor")
        {
            if (dim.Ref == null || !_ctx.TryGetValue(dim.Ref, out var ctx))
            { Console.WriteLine($"WARN: Dim ref '{dim.Ref}' not found."); return; }
            RefreshEllipsePoints(ctx);
            var ell = (SketchEllipse)(object)ctx.Seg!;
            var ctr = (SketchPoint)ell.GetCenterPoint2();
            var axPt = dim.Type == "semi_major"
                ? (SketchPoint)ell.GetMajorPoint2()
                : (SketchPoint)ell.GetMinorPoint2();
            ctr.Select4(false, null);
            axPt.Select4(true, null);
            lx = dim.Label != null ? dim.Label[0] / 1e3 : (ctr.X + axPt.X) / 2;
            ly = dim.Label != null ? dim.Label[1] / 1e3 : (ctr.Y + axPt.Y) / 2 - 0.012;
        }
        else
        {
            var refs = dim.Refs ?? (dim.Ref != null ? new List<string> { dim.Ref } : null);
            if (refs == null) { Console.WriteLine("WARN: Dimension has no ref."); return; }

            bool first = true;
            foreach (var r in refs)
            {
                var (id, sel) = ParseRef(r);
                if (!_ctx.TryGetValue(id, out var ctx)) { Console.WriteLine($"WARN: Dim entity '{id}' not found."); Clear(); return; }
                bool ok = sel != null
                    ? (ctx.GetPoint(sel)?.Select4(!first, null) ?? false)
                    : (ctx.Seg?.Select4(!first, null) ?? false);
                if (!ok) { Console.WriteLine($"WARN: Dim select failed for '{r}'."); Clear(); return; }
                first = false;
            }

            (lx, ly) = dim.Label != null
                ? (dim.Label[0] / 1e3, dim.Label[1] / 1e3)
                : AutoLabel(refs);
        }

        var dispDim = _doc.AddDimension2(lx, ly, 0) as DisplayDimension;

        // AddDimension2 on a circle creates a DIAMETER dim (not radius).
        // Compensate so a "radius" spec on a circle yields the intended radius.
        double sysValue = dim.Value / 1e3;
        if (dim.Type?.ToLowerInvariant() == "radius" && dim.Ref != null
            && _ctx.TryGetValue(dim.Ref, out var rCtx)
            && rCtx.Def.Type.ToLowerInvariant() == "circle")
        {
            sysValue *= 2;
        }

        if (dispDim?.GetDimension() is Dimension d)
        { d.SystemValue = sysValue; Console.WriteLine($"  Dim {dim.Type}={dim.Value}mm OK."); }
        else Console.WriteLine($"  WARN: AddDimension2 failed for '{dim.Type}'.");
        Clear();
    }

    // Auto-compute a reasonable label position from the refs' midpoint
    private (double x, double y) AutoLabel(List<string> refs)
    {
        var pts = refs
            .Select(r => { var (id, sel) = ParseRef(r); _ctx.TryGetValue(id, out var c); return (ctx: c, sel); })
            .SelectMany(t => t.sel != null
                ? new[] { t.ctx?.GetPoint(t.sel) }
                : new[] { t.ctx?.StartPt, t.ctx?.EndPt })
            .Where(p => p != null)
            .Cast<SketchPoint>()
            .ToList();

        if (pts.Count == 0) return (0, -0.020);
        double mx = pts.Average(p => p.X);
        double my = pts.Average(p => p.Y);
        return (mx + 0.010, my - 0.015);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static (string Id, string? Sel) ParseRef(string r)
    {
        var parts = r.Split('.', 2);
        return (parts[0], parts.Length > 1 ? parts[1] : null);
    }

    private void Sel(SketchSegment seg) => seg.Select4(false, null);
    private void Clear() => _doc.ClearSelection2(true);
}
