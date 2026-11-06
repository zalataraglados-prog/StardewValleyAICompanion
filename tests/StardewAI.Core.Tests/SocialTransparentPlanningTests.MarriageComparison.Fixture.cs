using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Core.Tests;

public sealed partial class SocialTransparentPlanningTests
{
    private sealed record MarriageComparisonFixture(string Snapshot, string Intake, string Ledger, string Calibration, string Preference);

    private static string MarriageComparisonSummary(CurrentMarriageTeacherPreference report) => JsonSerializer.Serialize(new
    {
        report.Status, report.BlockingReasons,
        MissingSources = report.Intake.Availability.Options.Where(row => !row.ReadEligible).Select(row => new { row.OptionId, row.MissingStateFactors }),
        Candidates = report.Candidates.Select(row => new { row.CandidateId, row.Reasons })
    });

    private static MarriageComparisonFixture MarriageComparisonFiles(int houseLevel = 2, int constructionDays = -1)
    {
        var old = FriendshipTeacherFiles(gifts: true, targets: 1);
        var snapshot = JsonSerializer.Deserialize<SnapshotEnvelope>(File.ReadAllText(old.Snapshot), JsonOptions)!;
        var marriage = CurrentMarriageHouseStateTests.Snapshot(false, houseLevel, spouse: "", days: constructionDays);
        var player = JsonNode.Parse(snapshot.State["player"].GetRawText())!;
        foreach (var field in JsonNode.Parse(marriage.State["player"].GetRawText())!.AsObject()) player[field.Key] = field.Value!.DeepClone();
        var items = player["inventory"]!["value"]!.AsArray();
        items[0]!["sell_to_store_price"] = 100;
        var pumpkin = items[0]!.DeepClone();
        pumpkin["slot_index"] = 1; pumpkin["item_id"] = "276"; pumpkin["qualified_item_id"] = "(O)276"; pumpkin["sell_to_store_price"] = 352;
        var pendant = items[0]!.DeepClone();
        pendant["slot_index"] = 2; pendant["item_id"] = "460"; pendant["qualified_item_id"] = "(O)460"; pendant["protected_from_auto_sell"] = true;
        items.Add(pumpkin); items.Add(pendant);
        snapshot.State["player"] = JsonSerializer.SerializeToElement(player);
        snapshot.SaveId.Value = "fixture"; snapshot.PlayerId.Value = "1";
        snapshot.SaveId.Status = FieldStatus.Available; snapshot.PlayerId.Status = FieldStatus.Available;
        snapshot.State["identity"] = JsonSerializer.SerializeToElement(new
        { save_id = CurrentMarriageHouseStateTests.Field("fixture"), player_id = CurrentMarriageHouseStateTests.Field("1") });
        var time = JsonNode.Parse(snapshot.State["time"].GetRawText())!;
        time["season"] = JsonSerializer.SerializeToNode(CurrentMarriageHouseStateTests.Field("winter"));
        time["day"] = JsonSerializer.SerializeToNode(CurrentMarriageHouseStateTests.Field(17));
        time["total_days"]!["value"] = 212;
        snapshot.State["time"] = JsonSerializer.SerializeToElement(time);
        var world = JsonNode.Parse(marriage.State["world_progress"].GetRawText())!;
        world["game_state_query_calendar_state"] = JsonNode.Parse(CurrentMarriageHouseSettlementTests.Snapshot()
            .State["world_progress"].GetProperty("game_state_query_calendar_state").GetRawText());
        snapshot.State["world_progress"] = JsonSerializer.SerializeToElement(world);
        CurrentMarriageHouseStateTests.Mutate(snapshot, "menus", node => node["menu_specific_state"] =
            JsonSerializer.SerializeToNode(CurrentMarriageHouseStateTests.Field(new { })));
        CurrentMarriageHouseStateTests.Mutate(snapshot, "current_location", node => node["home_context"] =
            JsonSerializer.SerializeToNode(CurrentMarriageHouseStateTests.Field(new { is_player_home = false })));
        CurrentMarriageHouseStateTests.Mutate(snapshot, "locations", node => node["social_route_date_evidence"]!["value"]!["capture_total_days"] = 212);
        CurrentMarriageHouseStateTests.Mutate(snapshot, "npcs", node =>
        {
            var npc = node["social_interaction"]!["value"]![0]!;
            npc["is_datably_flagged"] = true; npc["is_married_or_engaged"] = false;
            npc["friendship_points"] = 2420; npc["expected_talk_friendship_points_after"] = 2440;
            var friendship = node["friendships"]!["value"]![0]!;
            friendship["points"] = 2420; friendship["status"] = "Dating"; friendship["is_dating"] = true;
            friendship["is_engaged"] = false; friendship["roommate_marriage"] = false;
            node["schedule_catalog"]!["value"]!["current_selection_context"]!["capture_total_days"] = 212;
            var taste = node["gift_tastes"]!["value"]![0]!.DeepClone();
            taste["slot_index"] = 1; taste["qualified_item_id"] = "(O)276";
            node["gift_tastes"]!["value"]!.AsArray().Add(taste);
        });
        var graph = new MaterialInventoryGraph
        {
            PlayerId = 1, PhysicalInventoryCount = 1, InventoryNodes = new[] { new MaterialInventoryNode
            {
                NodeId = "player:1", InventoryKind = "player_inventory", OwnerPlayerId = 1, ActorUseAuthorized = true,
                SupplyState = "available", Slots = items.Select(item => new MaterialInventorySlot
                {
                    SlotIndex = (int)item!["slot_index"]!, QualifiedItemId = (string)item["qualified_item_id"]!,
                    ItemId = (string)item["item_id"]!, Stack = 1, Quality = 0
                }).ToArray()
            } }
        };
        snapshot.State["farm"] = JsonSerializer.SerializeToElement(new { material_inventory_graph = CurrentMarriageHouseStateTests.Field(graph) });
        snapshot.StateHash = SnapshotHash.ComputeStateHash(snapshot.State);
        var files = MarriageIntakeFiles(snapshot);
        var ledger = Path.Combine(Path.GetDirectoryName(files.Intake)!, "ledger.json");
        File.WriteAllText(ledger, JsonSerializer.Serialize(new StrategyCommitmentLedger
        { LedgerId = "declared-local-current-obligations", SaveId = "fixture", PlayerId = "1", SourceStateHash = snapshot.StateHash }, JsonOptions));
        File.WriteAllText(files.Intake, JsonSerializer.Serialize(CurrentMarriageHouseIntakeBuilder.Build(files.Snapshot, files.Knowledge, files.Dependencies), JsonOptions));
        return new(files.Snapshot, files.Intake, ledger, old.Calibration, Path.Combine(Path.GetDirectoryName(ledger)!, "preference.json"));
    }

    private static void ReserveMarriageFixture(MarriageComparisonFixture f, int slot)
    {
        var snapshot = JsonSerializer.Deserialize<SnapshotEnvelope>(File.ReadAllText(f.Snapshot), JsonOptions)!;
        var ledger = JsonSerializer.Deserialize<StrategyCommitmentLedger>(File.ReadAllText(f.Ledger), JsonOptions)!;
        var item = snapshot.State["player"].GetProperty("inventory").GetProperty("value")[slot];
        ledger.MaterialReservations = new[] { new MaterialReservation
        {
            ReservationId = "other-goal", Revision = 1, SourceDecisionId = "other", SourceStateHash = snapshot.StateHash,
            GoalId = "other-goal", OwnerPlayerId = 1, NodeId = "player:1", SlotIndex = slot,
            QualifiedItemId = item.GetProperty("qualified_item_id").GetString()!, Quantity = 1, Purpose = "retain_for_other_goal"
        } };
        File.WriteAllText(f.Ledger, JsonSerializer.Serialize(ledger, JsonOptions));
    }
}
