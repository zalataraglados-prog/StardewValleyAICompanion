using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.OptionRegistry;

public sealed partial class CandidateOptionAvailabilityEvaluator
{
    private EventCandidate[] AnimalProductCandidates(SnapshotEnvelope snapshot)
    {
        var animals = ReadStateFieldValue(snapshot, "farm", "animals");
        if (!animals.HasValue || animals.Value.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<EventCandidate>();
        }

        var currentLocation = ReadStateFieldString(snapshot, "player", "location_id");
        var playerX = ReadStateFieldInt(snapshot, "player", "tile_x");
        var playerY = ReadStateFieldInt(snapshot, "player", "tile_y");
        return animals.Value.EnumerateArray()
            .Where(animal => animal.ValueKind == JsonValueKind.Object &&
                ReadString(animal, "harvest_type") == "HarvestWithTool")
            .Select(animal =>
            {
                var animalId = ReadStringOrNumber(animal, "animal_id");
                var locationId = ReadString(animal, "location_id");
                var x = ReadInt(animal, "tile_x");
                var y = ReadInt(animal, "tile_y");
                var sameLocation = string.Equals(locationId, currentLocation, StringComparison.OrdinalIgnoreCase);
                var stand = sameLocation ? FindBestStandTile(snapshot, x, y) : null;
                var status = ReadString(animal, "harvest_status");
                var outputId = ReadString(animal, "harvest_output_qualified_item_id");
                var outputJson = ReadString(animal, "harvest_expected_output_items_json");
                var outputHash = ReadString(animal, "harvest_output_unit_state_sha256");
                var quantity = ReadInt(animal, "harvest_output_quantity");
                var harvestTool = ReadString(animal, "harvest_tool");
                var harvestToolRuntimeType = ReadString(animal, "harvest_tool_runtime_type");
                var blockReasons = new List<string>();
                if (!string.Equals(
                        ReadString(animal, "runtime_type"),
                        "StardewValley.FarmAnimal",
                        StringComparison.Ordinal))
                {
                    blockReasons.Add("unsupported_animal_runtime_type");
                }
                if (!string.Equals(
                        ReadString(animal, "harvest_output_runtime_type"),
                        "StardewValley.Object",
                        StringComparison.Ordinal))
                {
                    blockReasons.Add("unsupported_animal_product_runtime_type");
                }
                var expectedToolRuntimeType = harvestTool switch
                {
                    "Milk Pail" => "StardewValley.Tools.MilkPail",
                    "Shears" => "StardewValley.Tools.Shears",
                    _ => string.Empty
                };
                if (expectedToolRuntimeType.Length == 0 ||
                    !string.Equals(harvestToolRuntimeType, expectedToolRuntimeType, StringComparison.Ordinal))
                {
                    blockReasons.Add("unsupported_animal_harvest_tool_runtime_type");
                }
                if (status != "ready")
                {
                    blockReasons.Add(string.IsNullOrWhiteSpace(status) ? "animal_harvest_projection_unavailable" : status);
                }
                if (!sameLocation)
                {
                    blockReasons.Add("animal_not_in_current_location");
                }
                if (stand is null && sameLocation)
                {
                    blockReasons.Add("animal_harvest_no_adjacent_stand_tile");
                }
                if (string.IsNullOrWhiteSpace(animalId) || string.IsNullOrWhiteSpace(outputId) ||
                    string.IsNullOrWhiteSpace(outputJson) || outputHash.Length != 64 || quantity <= 0)
                {
                    blockReasons.Add("animal_harvest_output_identity_incomplete");
                }

                var parameters = stand is null
                    ? Array.Empty<SmallModelActionParameter>()
                    : AnimalProductParameters(animal, animalId, x, y, stand.X, stand.Y);
                if (parameters.Length > 0)
                {
                    blockReasons.AddRange(CompilerProbeBlockingReasons(snapshot, new OptionAvailabilityCandidate
                    {
                        OptionId = "executor.collect_animal_product",
                        Parameters = parameters
                    }));
                }

                var distance = stand is null ? 0 : Math.Abs(playerX - stand.X) + Math.Abs(playerY - stand.Y);
                return new EventCandidate
                {
                    CandidateId = "collect-animal-product:" + locationId + ":" + animalId + ":" + outputId,
                    Kind = "collect_animal_product",
                    Available = blockReasons.Count == 0,
                    LocationId = locationId,
                    TileX = x,
                    TileY = y,
                    ItemId = ReadString(animal, "current_produce_item_id"),
                    QualifiedItemId = outputId,
                    DisplayName = ReadString(animal, "display_name"),
                    Quantity = quantity,
                    ExpectedEffect = AnimalProductExpectedEffect(animal, animalId, stand),
                    EstimatedTicks = Math.Max(120, distance * 60 + 120),
                    EnergyCost = ReadInt(animal, "harvest_energy_cost"),
                    AvailabilityClass = "transparent_native_animal_tool_harvest",
                    BlockReasons = blockReasons.Distinct(StringComparer.Ordinal).ToArray(),
                    Parameters = parameters
                };
            })
            .ToArray();
    }

    private static SmallModelActionParameter[] AnimalProductParameters(JsonElement animal, string animalId, int x, int y, int standX, int standY)
    {
        return new[]
        {
            Parameter("target_location", ReadString(animal, "location_id")),
            Parameter("location_id", ReadString(animal, "location_id")),
            Parameter("target_tile_x", x.ToString()),
            Parameter("target_tile_y", y.ToString()),
            Parameter("stand_tile_x", standX.ToString()),
            Parameter("stand_tile_y", standY.ToString()),
            Parameter("target_runtime_type", ReadString(animal, "runtime_type")),
            Parameter("target_runtime_identity", animalId),
            Parameter("target_name", ReadString(animal, "name")),
            Parameter("required_tool_kind", ReadString(animal, "harvest_tool")),
            Parameter("tool_slot_index", ReadInt(animal, "harvest_tool_slot_index").ToString()),
            Parameter("qualified_item_id", ReadString(animal, "harvest_output_qualified_item_id")),
            Parameter("quantity", ReadInt(animal, "harvest_output_quantity").ToString()),
            Parameter("expected_output_quality", ReadInt(animal, "harvest_output_quality").ToString()),
            Parameter("expected_output_items_json", ReadString(animal, "harvest_expected_output_items_json")),
            Parameter("expected_stat_increments_json", ReadString(animal, "harvest_stat_increments_json")),
            Parameter("expected_animal_cracker_multiplier", ReadBool(animal, "has_eaten_animal_cracker") == true ? "2" : "1"),
            Parameter("expected_skill_id", "farming"),
            Parameter("expected_skill_experience_delta", ReadInt(animal, "harvest_farming_experience_delta").ToString()),
            Parameter("expected_energy_delta", (-ReadInt(animal, "harvest_energy_cost")).ToString()),
            Parameter("expected_friendship_before", ReadInt(animal, "friendship_toward_farmer").ToString()),
            Parameter("expected_friendship_after", ReadInt(animal, "friendship_after_harvest").ToString()),
            Parameter("max_movement_tiles", "512")
        };
    }

    private static string AnimalProductExpectedEffect(JsonElement animal, string animalId, CandidateTile? stand)
    {
        return (stand is null ? string.Empty : "animal_harvest_stand_tile=" + stand.X + "," + stand.Y + ";") +
            "animal_id=" + animalId +
            ";animal.current_produce=null" +
            ";required_tool_kind=" + ReadString(animal, "harvest_tool") +
            ";qualified_item_id=" + ReadString(animal, "harvest_output_qualified_item_id") +
            ";quantity=" + ReadInt(animal, "harvest_output_quantity") +
            ";expected_output_quality=" + ReadInt(animal, "harvest_output_quality") +
            ";expected_output_items_json=" + ReadString(animal, "harvest_expected_output_items_json") +
            ";expected_stat_increments_json=" + ReadString(animal, "harvest_stat_increments_json") +
            ";expected_skill_id=farming" +
            ";expected_skill_experience_delta=" + ReadInt(animal, "harvest_farming_experience_delta") +
            ";expected_energy_delta=-" + ReadInt(animal, "harvest_energy_cost") +
            ";expected_friendship_before=" + ReadInt(animal, "friendship_toward_farmer") +
            ";expected_friendship_after=" + ReadInt(animal, "friendship_after_harvest");
    }

    private static string ReadStringOrNumber(JsonElement value, string propertyName)
    {
        if (!value.TryGetProperty(propertyName, out var property))
        {
            return string.Empty;
        }
        return property.ValueKind == JsonValueKind.String ? property.GetString() ?? string.Empty : property.GetRawText();
    }
}
