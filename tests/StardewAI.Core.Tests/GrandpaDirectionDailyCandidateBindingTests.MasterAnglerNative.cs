using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Training;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Core.Tests;

public sealed partial class GrandpaDirectionDailyCandidateBindingTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MasterAnglerNativeCatchReplaysExactRodAndTrapSourcesWithoutInventingTeacherLabels(bool trap)
    {
        var f = CreateMasterAnglerNativeFixture(trap);
        var result = MasterAnglerNativeCatchReceiptBuilder.Build(f.RequestPath);
        Assert.Equal("(O)fixture_fish_0", result.QualifiedItemId);
        Assert.Equal(trap ? "executor.collect_crab_pot" : "executor.catch_fish", result.OptionId);
        Assert.True(result.NativeCollectionTransitionVerified);
        Assert.False(result.TeacherComparisonVerified);
        Assert.False(result.FormalTrainingAuthorized);
        Assert.Equal(9, result.SourceSha256.Count);
        Assert.Equal(6, result.TotalDay);
        var receiptPath = Path.Combine(f.Join.Root, "catch-receipt.json");
        WriteNativeKeyJson(receiptPath, result);
        Assert.Equal(JsonSerializer.Serialize(result, JsonOptions),
            JsonSerializer.Serialize(MasterAnglerNativeCatchReceiptBuilder.Verify(receiptPath), JsonOptions));
        EditNativeKeyJson(receiptPath, node => node["teacher_comparison_verified"] = true);
        Assert.Throws<InvalidDataException>(() => MasterAnglerNativeCatchReceiptBuilder.Verify(receiptPath));
    }

    [Theory]
    [InlineData("foreign_run")]
    [InlineData("missing_before_progress")]
    [InlineData("shrunk_after_progress")]
    [InlineData("already_caught")]
    [InlineData("collection_regressed")]
    [InlineData("missing_native_observation")]
    [InlineData("different_observed_species")]
    [InlineData("different_outcome_domain")]
    [InlineData("reward_recovery_only")]
    [InlineData("after_capture_substitution")]
    [InlineData("queue_source_substitution")]
    [InlineData("authority_hash_drift")]
    [InlineData("authority_version_drift")]
    [InlineData("lowering_denominator_drift")]
    [InlineData("trap_collection_not_eligible")]
    [InlineData("trap_wrong_item")]
    public void MasterAnglerNativeCatchRejectsMisattributedSources(string mode)
    {
        var f = CreateMasterAnglerNativeFixture(mode.StartsWith("trap_", StringComparison.Ordinal), mode == "reward_recovery_only");
        var source = f.Request;
        void Before(Action<JsonNode> edit)
        {
            EditNativeKeySnapshot(f.Join.BeforePaths[0], edit);
            var snapshot = JsonNode.Parse(File.ReadAllText(f.Join.BeforePaths[0]))!;
            EditNativeKeyJson(source.ActionQueuePath, node =>
            {
                node["state_hash"] = snapshot["state_hash"]!.DeepClone();
                node["items"]![0]!["normalized_command"]!["state_hash"] = snapshot["state_hash"]!.DeepClone();
            });
            f.NativeRequest.BeforeStateHash = snapshot["state_hash"]!.GetValue<string>();
            EditNativeKeyJson(source.ExecutionReceiptPath, node => node["before_state_hash"] = snapshot["state_hash"]!.DeepClone());
            RebindMasterAnglerJournal(f);
        }
        void After(Action<JsonNode> edit)
        {
            EditNativeKeySnapshot(source.AfterSnapshotPath, edit);
            var snapshot = JsonNode.Parse(File.ReadAllText(source.AfterSnapshotPath))!;
            EditNativeKeyJson(source.ExecutionReceiptPath, node => node["after_state_hash"] = snapshot["state_hash"]!.DeepClone());
        }
        switch (mode)
        {
            case "foreign_run": WriteNativeKeyJson(f.RequestPath, source with { RunId = "other-run" }); break;
            case "missing_before_progress": Before(node => node["state"]!.AsObject().Remove("world_progress")); break;
            case "shrunk_after_progress": After(node => node["state"]!["world_progress"]!["fish_collection_progress"]!["value"]!["items"]!.AsArray().RemoveAt(1)); break;
            case "already_caught": Before(node => node["state"]!["world_progress"]!["fish_collection_progress"] = FishProgress(true)); break;
            case "collection_regressed": After(node =>
            {
                var value = node["state"]!["world_progress"]!["fish_collection_progress"]!["value"]!;
                value["items"]![1]!["caught"] = false;
                value["caught_eligible_species_count"] = 71; value["missing_species_count"] = 1;
                value["missing_item_ids"] = new JsonArray("fixture_fish_1");
            }); break;
            case "missing_native_observation": EditNativeKeyJson(source.ExecutionReceiptPath, node => node["primitive_verification_reasons"] = new JsonArray()); break;
            case "different_observed_species": EditNativeKeyJson(source.ExecutionReceiptPath, node =>
            {
                var rows = node["primitive_verification_reasons"]!.AsArray();
                rows[rows.Count - 1] = "observed_qualified_item_id=(O)fixture_fish_1";
            }); break;
            case "different_outcome_domain": f.NativeRequest.PossibleQualifiedItemIdsJson = "[\"(O)fixture_fish_1\"]"; RebindMasterAnglerJournal(f); break;
            case "reward_recovery_only": break;
            case "after_capture_substitution":
                var afterCopy = Path.Combine(f.Join.Root, "copied-after.json");
                WriteNativeKeyJson(afterCopy, JsonNode.Parse(File.ReadAllText(source.AfterSnapshotPath))!);
                WriteNativeKeyJson(f.RequestPath, source with { AfterSnapshotPath = afterCopy }); break;
            case "queue_source_substitution":
                var queueCopy = Path.Combine(f.Join.Root, "copied-queue.json");
                WriteNativeKeyJson(queueCopy, JsonNode.Parse(File.ReadAllText(source.ActionQueuePath))!);
                WriteNativeKeyJson(f.RequestPath, source with { ActionQueuePath = queueCopy }); break;
            case "authority_hash_drift": EditNativeKeyJson(source.RequirementInventoryPath, node => node["admission_policy"] = "changed"); break;
            case "authority_version_drift":
                EditNativeKeyJson(source.RequirementInventoryPath, node => node["game_version"] = "other");
                EditNativeKeyJson(source.AcquisitionLoweringPath, node => node["requirement_inventory_sha256"] = FileSha(source.RequirementInventoryPath)); break;
            case "lowering_denominator_drift": EditNativeKeyJson(source.AcquisitionLoweringPath, node => node["requirement_sets"]![0]!["teacher_admitted_group_count"] = 71); break;
            case "trap_collection_not_eligible": f.NativeRequest.ExpectedFishCollectionEligible = 0; RebindMasterAnglerJournal(f); break;
            case "trap_wrong_item": f.NativeRequest.QualifiedItemId = "(O)fixture_fish_1"; RebindMasterAnglerJournal(f); break;
        }
        Assert.Throws<InvalidDataException>(() => MasterAnglerNativeCatchReceiptBuilder.Build(f.RequestPath));
    }

    private sealed record MasterAnglerNativeFixture(NativeJoinFixture Join, string RequestPath,
        MasterAnglerNativeCatchRequest Request, TrainingExecutionRequest NativeRequest);

    private static MasterAnglerNativeFixture CreateMasterAnglerNativeFixture(bool trap, bool recovery = false)
    {
        var f = CreateNativeJoinFixture();
        var source = f.Request.Actions[0];
        EditNativeKeySnapshot(f.BeforePaths[0], node => node["state"]!["world_progress"]!["fish_collection_progress"] = FishProgress(false));
        EditNativeKeySnapshot(source.AfterSnapshotPath, node => node["state"]!["world_progress"]!["fish_collection_progress"] = FishProgress(true));
        var kind = recovery ? "claim_fishing_item_grab_reward" : trap ? "collect_crab_pot" : "catch_fish";
        var queuePath = WriteJoinQueue(f.Root, kind, f.BeforePaths[0]);
        var queue = JsonNode.Parse(File.ReadAllText(queuePath))!;
        var raw = JsonNode.Parse(File.ReadAllText(source.ExecutionReceiptPath))!;
        raw["queue_id"] = queue["queue_id"]!.DeepClone(); raw["queue_item_id"] = queue["items"]![0]!["queue_item_id"]!.DeepClone();
        raw["option_id"] = queue["items"]![0]!["option_id"]!.DeepClone();
        raw["primitive_kind"] = kind;
        raw["before_state_hash"] = queue["state_hash"]!.DeepClone();
        raw["after_state_hash"] = JsonNode.Parse(File.ReadAllText(source.AfterSnapshotPath))!["state_hash"]!.DeepClone();
        raw["mechanical_compiled_queue_path"] = queuePath;
        raw["primitive_verification_reasons"] = JsonSerializer.SerializeToNode(trap
            ? new[] { "native_checkAction_collected_exact_crab_pot_output", "inventory_unit_state_multiset_matched", "bait_and_ready_state_reset", "fishing_experience_matched", "caughtFish_arguments_observed" }
            : new[] { "bobber_bar_success_observed", "fish_caught_hold_observed", "inventory_or_collection_updated", "action_idle_cleanup_complete", "observed_outcome_in_compiled_distribution", "observed_qualified_item_id=(O)fixture_fish_0" });
        WriteNativeKeyJson(source.ExecutionReceiptPath, raw);
        var native = new TrainingExecutionRequest
        {
            RunId = raw["run_id"]!.GetValue<string>(),
            QueueId = queue["queue_id"]!.GetValue<string>(), QueueItemId = queue["items"]![0]!["queue_item_id"]!.GetValue<string>(),
            OptionId = raw["option_id"]!.GetValue<string>(), BeforeStateHash = queue["state_hash"]!.GetValue<string>(),
            SaveIsolationPath = Path.Combine(f.Root, "isolated-saves"), RequestNonce = Guid.NewGuid().ToString("N"),
            PossibleQualifiedItemIdsJson = "[\"(O)fixture_fish_0\"]", QualifiedItemId = "(O)fixture_fish_0",
            ExpectedFishCollectionEligible = 1, ExpectedFishCaughtCountBefore = 0, ExpectedFishCaughtCountAfter = 1
        };
        var inventoryPath = Path.Combine(f.Root, "fish-inventory.json");
        var set = new GoalRequirementSet
        {
            RequirementSetId = "master_angler", RequiredGroupCount = 72, AcquisitionRoutesComplete = true,
            Groups = Enumerable.Range(0, 72).Select(index => new GoalRequirementGroup
            {
                RequirementId = "fish:" + index, SelectionRule = "all_required", RequiredAlternativeCount = 1,
                Alternatives = new[] { new GoalRequirementAlternative { ItemId = "fixture_fish_" + index,
                    QualifiedItemId = "(O)fixture_fish_" + index, MatchKind = "item_id", Amount = 1 } }
            }).ToArray()
        };
        WriteNativeKeyJson(inventoryPath, new AuthoritativeRequirementInventoryReport
            { Status = "complete", GameVersion = "1.6.15", GoalId = "grandpa.maximum_21", DenominatorComplete = true, AcquisitionRoutesComplete = true, RequirementSets = new[] { set } });
        var lowered = set.Groups.Select(group => new AcquisitionRequirementGroupLowering(group.RequirementId, 1, 1, 1, true, true,
            Array.Empty<string>(), group.Alternatives.Select(item => new AcquisitionRequirementAlternativeLowering(item.ItemId, item.QualifiedItemId,
                "fixture", "item_id", 1, 0, true, true, new[] { new AcquisitionRequirementRouteLowering("fixture", "fixture", "fixture", "fixture",
                    "fixture", "fixture", StageOneCollectionRouteDependencyAxes.Required.ToArray(), new[] { "fishing.catch_fish" }, Array.Empty<string>(), true, true) })).ToArray())).ToArray();
        var loweringPath = Path.Combine(f.Root, "fish-lowering.json");
        WriteNativeKeyJson(loweringPath, new AcquisitionRouteOptionLoweringReport
        {
            Status = "complete", GoalId = "grandpa.maximum_21", RequirementInventorySha256 = FileSha(inventoryPath),
            DependencyAxisInventoryComplete = true, RequiredDownstreamDependencyAxes = StageOneCollectionRouteDependencyAxes.Required.ToArray(),
            RequirementSets = new[] { new AcquisitionRequirementSetLowering("master_angler", 72, 72, 72, lowered) }
        });
        var request = new MasterAnglerNativeCatchRequest { RunId = native.RunId, RequirementInventoryPath = inventoryPath,
            AcquisitionLoweringPath = loweringPath, ActionQueuePath = queuePath, ExecutionReceiptPath = source.ExecutionReceiptPath,
            AfterSnapshotPath = source.AfterSnapshotPath };
        var result = new MasterAnglerNativeFixture(f, Path.Combine(f.Root, "fish-native-request.json"), request, native);
        WriteNativeKeyJson(result.RequestPath, request);
        RebindMasterAnglerJournal(result);
        return result;
    }

    private static void RebindMasterAnglerJournal(MasterAnglerNativeFixture f)
    {
        WriteJoinJournal(f.Join.JournalPaths[0], f.NativeRequest, f.Join.BeforePaths[0]);
        BindJoinJournalResult(f.Join, 0, f.NativeRequest.RequestNonce);
    }

    private static string FileSha(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();

    private static JsonNode FishProgress(bool caught) => JsonSerializer.SerializeToNode(new
    {
        status = "available", value = new { eligible_species_count = 72, caught_eligible_species_count = caught ? 72 : 71,
            missing_species_count = caught ? 0 : 1, missing_item_ids = caught ? Array.Empty<string>() : new[] { "fixture_fish_0" },
            items = Enumerable.Range(0, 72).Select(index => new { item_id = "fixture_fish_" + index,
                qualified_item_id = "(O)fixture_fish_" + index, caught = caught || index != 0 }).ToArray() }
    })!;
}
