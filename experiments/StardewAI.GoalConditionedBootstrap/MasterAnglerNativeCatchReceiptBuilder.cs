using System.Text.Json;
using System.Text.Json.Nodes;
using static StardewAI.GoalConditionedBootstrap.NativeExecutionSourceSupport;

namespace StardewAI.GoalConditionedBootstrap;

public static class MasterAnglerNativeCatchReceiptBuilder
{
    public static MasterAnglerNativeCatchReceipt Build(string requestPath)
    {
        requestPath = RequiredFile(requestPath);
        var request = ReadTyped<MasterAnglerNativeCatchRequest>(requestPath, "master_angler_native_catch_request.v1");
        Require(!string.IsNullOrWhiteSpace(request.RunId), "Master Angler native run is missing.");
        var inventoryPath = RequiredFile(request.RequirementInventoryPath);
        var loweringPath = RequiredFile(request.AcquisitionLoweringPath);
        var inventory = ReadTyped<AuthoritativeRequirementInventoryReport>(inventoryPath, "authoritative_goal_requirement_inventory.v1");
        var lowering = ReadTyped<AcquisitionRouteOptionLoweringReport>(loweringPath, "acquisition_route_option_lowering.v1");
        CurrentTeacherFrontierSupport.ValidateAuthority(inventoryPath, inventory, lowering, "Master Angler native catch");
        var set = CurrentTeacherFrontierSupport.SingleSet(inventory.RequirementSets, row => row.RequirementSetId,
            "master_angler", "native catch authority");
        var loweredSet = CurrentTeacherFrontierSupport.SingleSet(lowering.RequirementSets, row => row.RequirementSetId,
            "master_angler", "native catch lowering");
        CurrentMasterAnglerTeacherFrontierBuilder.ValidateSets(set, loweredSet);
        var action = NativeExecutionActionSourceReader.Read(request.ActionQueuePath, request.ExecutionReceiptPath, request.AfterSnapshotPath);
        Require(inventory.GameVersion == action.Before.GameVersion, "Master Angler native authority version drifted.");
        Require(action.Result.OptionId is "executor.catch_fish" or "executor.collect_crab_pot",
            "Master Angler native collection requires a rod catch or trap harvest, not reward recovery or preparation.");
        var day = TotalDay(action.Before);
        Require(action.Result.RunId == request.RunId && day is >= 0 and < 224 && TotalDay(action.After) == day,
            "Master Angler native source has a foreign run or date.");
        var before = CurrentMasterAnglerTeacherFrontierBuilder.ReadExactProgress(action.Before, set);
        var after = CurrentMasterAnglerTeacherFrontierBuilder.ReadExactProgress(action.After, set);
        Require(before.All(row => !row.Value || after[row.Key]), "Master Angler native collection regressed.");
        var newlyCaught = before.Where(row => !row.Value && after[row.Key]).Select(row => row.Key).ToArray();
        Require(newlyCaught.Length == 1, "Master Angler native source has no single exact newly collected species.");
        var itemId = newlyCaught[0];
        VerifyNativeCatch(action, itemId);
        var hashes = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["request"] = CurrentTeacherFrontierSupport.HashFile(requestPath),
            ["requirement_inventory"] = CurrentTeacherFrontierSupport.HashFile(inventoryPath),
            ["acquisition_lowering"] = CurrentTeacherFrontierSupport.HashFile(loweringPath)
        };
        foreach (var hash in action.SourceSha256) hashes[hash.Key] = hash.Value;
        return new(requestPath, hashes, request.RunId, Identity(action.Before.SaveId), Identity(action.Before.PlayerId),
            action.Before.GameVersion, action.Before.BridgeVersion, day, action.Result.OptionId, action.Queue.QueueId,
            action.Item.QueueItemId, action.Request.RequestNonce, itemId, action.Before.StateHash, action.After.StateHash,
            action.Before.GameTick, action.After.GameTick);
    }

    public static MasterAnglerNativeCatchReceipt Verify(string receiptPath)
    {
        var raw = ReadTyped<JsonElement>(RequiredFile(receiptPath), "master_angler_native_catch_receipt.v1");
        var requestPath = CurrentTeacherFrontierSupport.RequiredString(raw, "request_path");
        var rebuilt = Build(requestPath);
        Require(JsonNode.DeepEquals(JsonNode.Parse(raw.GetRawText()), JsonSerializer.SerializeToNode(rebuilt, JsonDefaults.Options)),
            "Master Angler native receipt differs from its original sources.");
        return rebuilt;
    }

    private static void VerifyNativeCatch(VerifiedNativeExecutionAction action, string itemId)
    {
        var reasons = action.Result.PrimitiveVerificationReasons ?? Array.Empty<string>();
        var facts = reasons.ToHashSet(StringComparer.Ordinal);
        if (action.Result.OptionId == "executor.collect_crab_pot")
        {
            Require(new[] { "native_checkAction_collected_exact_crab_pot_output", "inventory_unit_state_multiset_matched",
                "bait_and_ready_state_reset", "fishing_experience_matched", "caughtFish_arguments_observed" }.All(facts.Contains) &&
                action.Request.ExpectedFishCollectionEligible == 1 && action.Request.ExpectedFishCaughtCountBefore == 0 &&
                action.Request.ExpectedFishCaughtCountAfter > 0 && action.Request.QualifiedItemId == itemId,
                "Master Angler trap source lacks the exact native collection call.");
            return;
        }
        using var domain = JsonDocument.Parse(action.Request.PossibleQualifiedItemIdsJson);
        var observed = reasons.Where(row => row.StartsWith("observed_qualified_item_id=", StringComparison.Ordinal)).ToArray();
        Require(facts.Contains("fish_caught_hold_observed") && facts.Contains("inventory_or_collection_updated") &&
            facts.Contains("action_idle_cleanup_complete") && facts.Contains("observed_outcome_in_compiled_distribution") &&
            facts.Overlaps(new[] { "bobber_bar_success_observed", "special_catch_without_bobber_bar_observed" }) &&
            observed.Length == 1 && observed[0] == "observed_qualified_item_id=" + itemId &&
            domain.RootElement.ValueKind == JsonValueKind.Array && domain.RootElement.EnumerateArray().Any(row =>
                row.ValueKind == JsonValueKind.String && row.GetString() == itemId),
            "Master Angler rod source lacks the exact observed native outcome.");
    }
}
