using System.Text.Json.Nodes;
using StardewAI.Contracts.State;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    private static SnapshotEnvelope
        WriteShopPurchaseNavigationProofBeforeSnapshot(
            string templatePath,
            string outputPath)
    {
        _ = WriteMachineCapacityProofBeforeSnapshot(
            templatePath,
            outputPath);
        var root = JsonNode.Parse(File.ReadAllText(outputPath))!.AsObject();
        root["state"]!["player"]!["seed_inventory"] =
            CropPlantingProofField(Array.Empty<object>(), 10);
        root["state"]!["farm"]!["crop_catalog"] =
            CropPlantingProofField(Array.Empty<object>(), 10);
        SetMachineInputPurchaseProofEnvelopeMetadata(
            root["state"]!["time"]!["season"]!.AsObject(),
            10);
        SetMachineInputPurchaseProofEnvelopeMetadata(
            root["state"]!["player"]!["money"]!.AsObject(),
            10);
        SetMachineInputPurchaseProofEnvelopeMetadata(
            root["state"]!["locations"]!["shops"]!.AsObject(),
            10);
        return WriteMachineCapacityProofSnapshot(root, outputPath);
    }

    private static SnapshotEnvelope
        WriteShopPurchaseNavigationProofAfterSnapshot(
            string beforePath,
            string outputPath)
    {
        var root = JsonNode.Parse(File.ReadAllText(beforePath))!.AsObject();
        root["game_tick"] = root["game_tick"]!.GetValue<long>() + 1;
        var player = root["state"]!["player"]!.AsObject();
        player["location_id"]!["value"] = "Town";
        player["tile_x"]!["value"] = 1;
        player["tile_y"]!["value"] = 5;
        root["state"]!["locations"]!["collision_grid"]!["value"]![
            "location_id"] = "Town";
        return WriteMachineCapacityProofSnapshot(root, outputPath);
    }
}
