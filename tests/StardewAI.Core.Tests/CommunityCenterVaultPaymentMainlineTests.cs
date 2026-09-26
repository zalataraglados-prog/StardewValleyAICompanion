using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed class CommunityCenterVaultPaymentMainlineTests
{
    [Fact]
    public void ExactVaultMoneySourceCompilesThroughNativePurchaseButton()
    {
        var snapshot = Snapshot();
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(
            snapshot,
            new[] { "community_center.donate_bundle_items" },
            true);
        var candidate = Assert.Single(Assert.Single(availability.Options).EventCandidates);

        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Equal("pay_community_center_vault_bundle", candidate.Kind);
        Assert.Equal(2500, candidate.Quantity);
        AssertParameter(candidate.Parameters, "price", "2500");
        AssertParameter(candidate.Parameters, "expected_money_before", "3000");
        AssertParameter(candidate.Parameters, "expected_money_after", "500");
        var sourceJson = AssertParameter(
            candidate.Parameters,
            "authoritative_route_sources_json");
        using var document = JsonDocument.Parse(sourceJson);
        var source = Assert.Single(document.RootElement.EnumerateArray());
        Assert.Equal("native_money_payment", source.GetProperty("route_kind").GetString());
        Assert.Equal("money", source.GetProperty("source_id").GetString());
        Assert.Equal(string.Empty, source.GetProperty("qualified_item_id").GetString());

        var plan = new DailyPlanCompiler().Compile(
            new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability),
            snapshot.StateHash);
        Assert.Equal("pay_community_center_vault_bundle", Assert.Single(plan.Steps).Kind);
        var queue = new ActionQueueCompiler().Compile(plan, snapshot);
        var item = Assert.Single(queue.Items);
        Assert.Equal("pending", queue.Status);
        Assert.Empty(item.BlockingReasons);
        Assert.Equal("executor.pay_community_center_vault_bundle", item.OptionId);
        Assert.Equal(
            "pay_community_center_vault_bundle",
            Assert.Single(item.NormalizedCommand.Steps).StepType);
    }

    [Fact]
    public void CompilerRejectsVaultPaymentWhenMoneyDrifts()
    {
        var original = Snapshot();
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(
            original,
            new[] { "community_center.donate_bundle_items" },
            true);
        var plan = new DailyPlanCompiler().Compile(
            new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability),
            original.StateHash);
        var driftedRoot = JsonSerializer.SerializeToNode(original.State)!.AsObject();
        driftedRoot["player"]!["money"]!["value"] = 2999;
        var drifted = Envelope(driftedRoot);
        plan.StateHash = drifted.StateHash;

        var queue = new ActionQueueCompiler().Compile(plan, drifted);

        Assert.Equal("blocked", queue.Status);
        Assert.Contains(
            "community_center_vault_payment_money_drifted",
            Assert.Single(queue.Items).BlockingReasons);
    }

    [Fact]
    public void RuntimeClicksNativeVaultPurchaseWithoutDirectOutcomeMutation()
    {
        var source = File.ReadAllText(FindRepositoryFile(
            "tools",
            "StardewAI.RuntimeTestHarness",
            "ModEntry.CommunityCenterVaultPayment.cs"));

        Assert.Contains("CommunityCenter.checkBundle", source, StringComparison.Ordinal);
        Assert.Contains("purchaseButton", source, StringComparison.Ordinal);
        Assert.Contains("receiveLeftClick", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.player.Money -=", source, StringComparison.Ordinal);
        Assert.DoesNotContain("bundleRewards[", source, StringComparison.Ordinal);
        Assert.DoesNotContain("bundles.FieldDict", source, StringComparison.Ordinal);
        Assert.DoesNotContain("markAreaAsComplete", source, StringComparison.Ordinal);
    }

    private static SnapshotEnvelope Snapshot()
    {
        const string json = """
        {
          "player": {
            "location_id":{"value":"CommunityCenter","status":"available"},
            "tile_x":{"value":8,"status":"available"},
            "tile_y":{"value":10,"status":"available"},
            "money":{"value":3000,"status":"available"},
            "inventory":{"value":[],"status":"available"}
          },
          "world_progress": {
            "community_center":{"value":{
              "route_state":"undecided",
              "community_center_is_current_location":true,
              "can_read_junimo_text":true,
              "complete_bundle_count":3,
              "bundle_data_row_count":1,
              "projected_bundle_row_count":1,
              "unavailable_bundle_row_count":0,
              "bundle_rows":[{
                "projection_status":"exact",
                "bundle_data_key":"Vault/23",
                "bundle_id":23,
                "area_id":4,
                "area_name":"Vault",
                "required_slot_count":1,
                "completed_ingredient_count":0,
                "complete":false,
                "reward_available":false,
                "area_complete":false,
                "note_appears":true,
                "note_tile_x":10,
                "note_tile_y":10,
                "interaction_tile_x":10,
                "interaction_tile_y":10,
                "area_mutex_locked":false,
                "area_completion_mail_id":"ccVault",
                "area_completion_mail_pending":false,
                "bulletin_thank_you_pending":false,
                "reward":{"projection_status":"exact","action_status":"community_center_bundle_reward_not_available"},
                "money_payment":{
                  "projection_status":"exact",
                  "projection_failure":"",
                  "ingredient_index":0,
                  "required_money":2500,
                  "money_before":3000,
                  "money_after":500,
                  "affordable":true,
                  "completed_ingredient_count_before":0,
                  "completed_ingredient_count_after":1,
                  "completes_bundle":true,
                  "expected_bundle_reward_available_after":true,
                  "expected_complete_bundle_count_after":4,
                  "completes_area":false,
                  "expected_area_complete_after":false,
                  "expected_area_completion_mail_pending_after":false,
                  "expected_bulletin_thank_you_pending_after":false,
                  "expected_all_areas_complete_after":false,
                  "newly_appearing_note_area_ids":[],
                  "action_status":"ready",
                  "authoritative_route_sources":[{
                    "route_kind":"native_money_payment",
                    "source_id":"money",
                    "qualified_item_id":"",
                    "source_asset":"Data/Bundles",
                    "source_path":"payload.Vault/23[ingredients:0]",
                    "native_consumer":"JunimoNoteMenu.receiveLeftClick/purchaseButton"
                  }]
                },
                "ingredients":[{"ingredient_index":0,"item_id_or_category":"-1","required_stack":2500,"minimum_quality":2500,"completed":false}],
                "donation_candidates":[]
              }]
            },"status":"available"}
          },
          "locations": {
            "collision_grid":{"value":{"location_id":"CommunityCenter","width":64,"height":64,"notable_tiles":[]},"status":"available"}
          },
          "menus": {
            "active_menu":{"value":{"is_open":false,"type":"none"},"status":"available"}
          }
        }
        """;
        return Envelope(JsonNode.Parse(json)!.AsObject());
    }

    private static SnapshotEnvelope Envelope(JsonObject root)
    {
        var state = root.Deserialize<Dictionary<string, JsonElement>>()!;
        return new SnapshotEnvelope
        {
            SchemaVersion = "snapshot.v1",
            StateHash = SnapshotHash.ComputeStateHash(state),
            GameTick = 1,
            RealTimestamp = "2026-09-26T00:00:00Z",
            Completeness = "complete",
            State = state
        };
    }

    private static string AssertParameter(
        StardewAI.Contracts.Execution.SmallModelActionParameter[] parameters,
        string name,
        string? expected = null)
    {
        var parameter = Assert.Single(parameters.Where(row => row.Name == name));
        if (expected is not null)
            Assert.Equal(expected, parameter.Value);
        return parameter.Value;
    }

    private static string FindRepositoryFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(new[] { directory.FullName }.Concat(parts).ToArray());
            if (File.Exists(candidate))
                return candidate;
            directory = directory.Parent;
        }
        throw new FileNotFoundException("Repository file not found.", Path.Combine(parts));
    }
}
