using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SketchDraw;

public record SketchPlan(
    [property: JsonPropertyName("entities")]   List<EntityDef>    Entities,
    [property: JsonPropertyName("anchor")]     AnchorDef?         Anchor      = null,
    [property: JsonPropertyName("relations")]  List<RelationDef>? Relations   = null,
    [property: JsonPropertyName("dimensions")] List<DimensionDef>? Dimensions = null
);

public class EntityDef
{
    [JsonPropertyName("id")]           public string  Id           { get; init; } = "";
    [JsonPropertyName("type")]         public string  Type         { get; init; } = "";
    [JsonPropertyName("construction")] public bool    Construction { get; init; } = false;

    // line
    [JsonPropertyName("start")]        public double[]? Start      { get; init; }
    [JsonPropertyName("end")]          public double[]? End        { get; init; }

    // circle
    [JsonPropertyName("center")]       public double[]? Center     { get; init; }
    [JsonPropertyName("radius")]       public double?   Radius     { get; init; }

    // arc (3-point)
    [JsonPropertyName("through")]      public double[]? Through    { get; init; }

    // ellipse
    [JsonPropertyName("semi_major")]   public double?   SemiMajor  { get; init; }
    [JsonPropertyName("semi_minor")]   public double?   SemiMinor  { get; init; }
    [JsonPropertyName("orientation")]  public string?   Orientation { get; init; }
}

public record AnchorDef(
    [property: JsonPropertyName("ref")] string Ref,
    [property: JsonPropertyName("to")]  string To = "origin"
);

public record RelationDef(
    [property: JsonPropertyName("type")] string       Type,
    [property: JsonPropertyName("refs")] List<string> Refs
);

public class DimensionDef
{
    [JsonPropertyName("ref")]   public string?       Ref   { get; init; }  // single entity
    [JsonPropertyName("refs")]  public List<string>? Refs  { get; init; }  // two entities
    [JsonPropertyName("type")]  public string        Type  { get; init; } = "";
    [JsonPropertyName("value")] public double        Value { get; init; }
    [JsonPropertyName("label")] public double[]?     Label { get; init; }  // [x, y] in mm, optional
}
