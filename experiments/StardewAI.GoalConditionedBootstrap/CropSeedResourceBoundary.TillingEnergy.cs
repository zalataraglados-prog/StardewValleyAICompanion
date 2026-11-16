using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Core.Training;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class CropSeedResourceBoundary
{
    internal static SnapshotEnvelope ProjectVerifiedTillingEnergy(SnapshotEnvelope before, SnapshotEnvelope after,
        SnapshotEnvelope observation, int toolSlot, string toolId)
    {
        var inputs = ReadTillingEnergyInputs(before, observation, toolSlot, toolId);
        Require(before.GameVersion == after.GameVersion && NativeExecutionSourceSupport.Identity(before.PlayerId) ==
            NativeExecutionSourceSupport.Identity(after.PlayerId) && NativeExecutionSourceSupport.Identity(before.SaveId) ==
            NativeExecutionSourceSupport.Identity(after.SaveId), "Crop till energy changed native actor/save/version.");
        Require(TillingEnergyProtection(after) == inputs.Protected, "Crop till native stamina protection changed during its action.");
        NativeHoeSingleTileEnergy.Verify(inputs.Before, TillingEnergyFloat(Value(Field(after, "player", "energy")!), "after"),
            inputs.Maximum, inputs.FarmingLevel, inputs.Efficient, inputs.Protected);
        var bowls = VerifyWateringBowlMirrors(before, after);
        var state = new Dictionary<string, JsonElement>(after.State, StringComparer.Ordinal);
        var player = JsonNode.Parse(after.State["player"].GetRawText())!.AsObject();
        player["energy"]!["value"] = Value(Field(before, "player", "energy")!).DeepClone();
        state["player"] = JsonSerializer.SerializeToElement(player, JsonDefaults.Options);
        if (bowls)
        {
            var farm = JsonNode.Parse(after.State["farm"].GetRawText())!.AsObject();
            farm["pet_bowls"]!["value"] = Value(Field(before, "farm", "pet_bowls")!).DeepClone();
            state["farm"] = JsonSerializer.SerializeToElement(farm, JsonDefaults.Options);
        }
        return new SnapshotEnvelope { SaveId = after.SaveId, PlayerId = after.PlayerId, GameVersion = after.GameVersion, State = state };
    }

    internal static void VerifyTillingWithoutAdditionalResourceChanges(SnapshotEnvelope before, SnapshotEnvelope after,
        SnapshotEnvelope observation, string location, MoneyRevenueCropPlantingTile tile, int toolSlot, string toolId)
    {
        var energy = ProjectVerifiedTillingEnergy(before, after, observation, toolSlot, toolId);
        var view = ProjectVerifiedTillingTerrainAndStatistic(before, energy, location, tile);
        SameField(before, after, "farm", "debris", required: true);
        Require(FullShipmentRecoverySettlementBoundaryProjector.TryProject(before, out var first) &&
            FullShipmentRecoverySettlementBoundaryProjector.TryProject(view, out var next) && first == next,
            "Crop till has additional inventory/tool/persistent effects requiring their original resource evidence.");
    }
}
