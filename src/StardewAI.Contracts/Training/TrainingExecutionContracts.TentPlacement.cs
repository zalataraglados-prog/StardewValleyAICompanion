using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Training;

public sealed partial class TrainingExecutionRequest
{
    [JsonPropertyName("tent_rectangle_x")]
    public int? TentRectangleX { get; set; }

    [JsonPropertyName("tent_rectangle_y")]
    public int? TentRectangleY { get; set; }

    [JsonPropertyName("tent_rectangle_width")]
    public int? TentRectangleWidth { get; set; }

    [JsonPropertyName("tent_rectangle_height")]
    public int? TentRectangleHeight { get; set; }

    [JsonPropertyName("tent_anchor_tile_x")]
    public int? TentAnchorTileX { get; set; }

    [JsonPropertyName("tent_anchor_tile_y")]
    public int? TentAnchorTileY { get; set; }
}

public sealed partial class TrainingExecutionResult
{
    [JsonPropertyName("tent_direction")]
    public int? TentDirection { get; set; }

    [JsonPropertyName("tent_stand_tile_x")]
    public int? TentStandTileX { get; set; }

    [JsonPropertyName("tent_stand_tile_y")]
    public int? TentStandTileY { get; set; }

    [JsonPropertyName("tent_rectangle_x")]
    public int? TentRectangleX { get; set; }

    [JsonPropertyName("tent_rectangle_y")]
    public int? TentRectangleY { get; set; }

    [JsonPropertyName("tent_rectangle_width")]
    public int? TentRectangleWidth { get; set; }

    [JsonPropertyName("tent_rectangle_height")]
    public int? TentRectangleHeight { get; set; }

    [JsonPropertyName("tent_anchor_tile_x")]
    public int? TentAnchorTileX { get; set; }

    [JsonPropertyName("tent_anchor_tile_y")]
    public int? TentAnchorTileY { get; set; }
}
