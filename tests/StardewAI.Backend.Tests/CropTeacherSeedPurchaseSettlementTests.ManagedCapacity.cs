using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.OptionRegistry;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Theory]
    [InlineData(1, false)]
    [InlineData(3, true)]
    public async Task ManagedHarvestCapacityUsesOriginalSelectedShippingWithoutHarvestCredit(int stack, bool move)
    {
        using var template = new NativeVariantTerminalFixture(nativeWire: true);
        using var f = new OwnedRouteFixture(productExecutor: true, prepareSource: source =>
            SetManagedCapacitySource(source, template.Shared.Source.Before, template.SilverSlot, stack, move));
        f.Runtime.LiveOptions.Goal = "goal.grandpa_max_score_year3";
        f.Runtime.LiveOptions.UseJointDailyCalendar = true;
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(f.Source.Before, ["economy.ship_items"], commitmentLedger: f.Source.Ledger);
        var shipping = Assert.Single(availability.Options);
        Assert.True(shipping.EventCandidates.Any(row => row.Available), "missing=" + string.Join(",", shipping.MissingStateFactors) +
            "; blocked=" + string.Join(",", shipping.BlockingReasons.Concat(shipping.EventCandidates.SelectMany(row => row.BlockReasons))));
        await using var owner = await f.Files.Open();
        var originalHead = owner.CurrentSnapshotPath;
        var originalLedger = File.ReadAllBytes(owner.CurrentLedgerPath);
        var batch = f.Inputs.Plan.Days.SelectMany(day => day.ActionBatches).First(row => row.OptionId == "executor.harvest_crop");
        var step = f.Step with { Stage = CropTeacherRunStageKinds.ExecuteCropBatch, ActionBatch = batch };
        var operation = await BuildManagedOperation(f.Runtime, f.Inputs, owner, step, inspectBuild: build =>
        {
            Assert.Equal("crop_teacher_harvest_inventory_capacity_recovery", build.GetType().GetProperty("InterruptKind")!.GetValue(build));
            Assert.Equal("full", build.GetType().GetProperty("SnapshotProfile")!.GetValue(build));
            var queue = (JsonObject)build.GetType().GetProperty("Queue")!.GetValue(build)!;
            Assert.Contains(queue["items"]!.AsArray(), row => row!["option_id"]!.GetValue<string>() == "executor.ship_inventory_item_to_bin");
            Assert.DoesNotContain(queue["items"]!.AsArray(), row => row!["option_id"]!.GetValue<string>() == "executor.harvest_crop");
        });
        Assert.NotNull(operation);
        Assert.True(operation.IsSharedSourceReturn);
        Assert.False(operation.IsHarvest);
        Assert.False(operation.IsControlRecovery);
        Assert.Same(operation, owner.Pending);
        Assert.Equal(originalHead, owner.CurrentSnapshotPath);
        Assert.Equal(originalLedger, File.ReadAllBytes(owner.CurrentLedgerPath));
        Assert.Equal(0, f.Runtime.Handler.Commits);
        Assert.False(File.Exists(Path.Combine(operation.RuntimeDirectory, "shared-source-native-claim.json")));
        var intent = JsonNode.Parse(File.ReadAllText(Path.Combine(f.Files.OwnerRoot, $"operation-{operation.Sequence:D6}-intent.json")))!;
        Assert.Equal("selected_native_work_yield.v1", intent["shared_source_execution_protocol"]!.GetValue<string>());
        var selected = intent["shared_source_selection"]!.Deserialize<CropTeacherSharedSourceSelection>(JsonOptions)!;
        Assert.Null(selected.DecisionReference);
        Assert.Equal(originalHead, selected.DecisionSnapshotPath);
        var ranking = JsonNode.Parse(File.ReadAllText(selected.DecisionRankingPath))!;
        var original = LifecycleRead<SmallModelPlanEnvelope>(selected.DecisionPlanPath);
        var candidate = SelectedQueueDecisionLease.CreateFromPlan(original, ranking.AsObject()).CandidateAt(selected.SelectedQueueIndex);
        var choice = Assert.Single(ranking["ranked_event_candidates"]!.AsArray().Where(row =>
            row!["candidate_id"]!.GetValue<string>() == candidate.CandidateId));
        Assert.Equal("economy.ship_items", choice!["option_id"]!.GetValue<string>());
        Assert.Equal(1, choice["quantity"]!.GetValue<int>());
        Assert.Equal(stack, choice["available_stack"]!.GetValue<int>());
    }

    private static void SetManagedCapacitySource(Fixture source, SnapshotEnvelope template, int selectedSlot, int stack, bool move)
    {
        foreach (var field in template.State) source.Before.State[field.Key] = field.Value.Clone();
        var progress = source.Before.State.TryGetValue("world_progress", out var retained)
            ? JsonNode.Parse(retained.GetRawText())!.AsObject() : new JsonObject();
        progress["shipping_collection"] = JsonSerializer.SerializeToNode(SaleStockCandidateField(source.Before, Array.Empty<object>()));
        source.Before.State["world_progress"] = JsonSerializer.SerializeToElement(progress, JsonOptions);
        ChangeState(source.Before, "player", player =>
        {
            player["energy"] = JsonSerializer.SerializeToNode(new { status = "available", confidence = 1d, value = 270 });
            player["tile_x"]!["value"] = move ? 0 : 4;
            player["tile_y"]!["value"] = move ? 0 : 4;
            var inventory = player["inventory"]!["value"]!.AsArray();
            inventory.Single(row => row!["slot_index"]!.GetValue<int>() == selectedSlot)!["stack"] = stack;
            var seed = inventory.OfType<JsonObject>().First(row => row["qualified_item_id"]?.GetValue<string>() == "(O)745");
            for (var index = 0; index < 12; index++)
            {
                var row = inventory.FirstOrDefault(item => item!["slot_index"]!.GetValue<int>() == index);
                if (row?["is_empty"]?.GetValue<bool>() == false) continue;
                var filled = seed.DeepClone(); filled["slot_index"] = index;
                if (row is null) inventory.Add(filled); else inventory[inventory.IndexOf(row)] = filled;
            }
            player["inventory_capacity"]!["value"] = JsonSerializer.SerializeToNode(new
                { max_items = 12, occupied_item_stacks = 12, empty_slots = 0, has_empty_slot = false });
        });
        ChangeState(source.Before, "farm", farm =>
        {
            var graph = farm["material_inventory_graph"]!["value"]!;
            graph["inventory_nodes"]![0]!["slots"] = JsonSerializer.SerializeToNode(
                JsonNode.Parse(source.Before.State["player"].GetProperty("inventory").GetProperty("value").GetRawText())!
                    .AsArray().Select(row => row!.Deserialize<MaterialInventorySlot>(JsonOptions)!).ToArray(), JsonOptions);
            var slots = graph["inventory_nodes"]!.AsArray().SelectMany(node => node!["slots"]!.AsArray()).OfType<JsonObject>();
            graph["quantity_rows"] = new JsonArray(slots.GroupBy(row => new
                { Id = row["qualified_item_id"]!.GetValue<string>(), Quality = row["quality"]!.GetValue<int>() })
                .Select(group => (JsonNode)new JsonObject
                {
                    ["qualified_item_id"] = group.Key.Id, ["quality"] = group.Key.Quality,
                    ["available_quantity"] = group.Sum(row => row["stack"]!.GetValue<int>()),
                    ["restricted_quantity"] = 0, ["ready_output_quantity"] = 0,
                    ["in_process_quantity"] = 0, ["source_slot_count"] = group.Count()
                }).ToArray());
        });
    }
}
