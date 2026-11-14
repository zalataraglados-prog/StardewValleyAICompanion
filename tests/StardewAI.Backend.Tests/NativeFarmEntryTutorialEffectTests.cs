using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Infrastructure;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.Testing;

namespace StardewAI.Backend.Tests;

public sealed class NativeFarmEntryTutorialEffectTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public void VerifiedFirstFarmConnectorMatchesOnlyTheExactTutorialEffectWithoutMutatingSnapshots()
    {
        var f = new Fixture();
        var before = JsonSerializer.Serialize(f.Before, Json);
        var after = JsonSerializer.Serialize(f.After, Json);
        Assert.True(f.Matches());
        Assert.Equal(before, JsonSerializer.Serialize(f.Before, Json));
        Assert.Equal(after, JsonSerializer.Serialize(f.After, Json));
        var projector = typeof(MoneyRevenueCropSeedAcquisitionProofBuilder).Assembly
            .GetType("StardewAI.GoalConditionedBootstrap.FullShipmentRecoverySettlementBoundaryProjector")!;
        var project = projector.GetMethod("TryProject", BindingFlags.NonPublic | BindingFlags.Static)!;
        var left = new object?[] { f.Before, null }; var right = new object?[] { f.After, null };
        Assert.True((bool)project.Invoke(null, left)!); Assert.True((bool)project.Invoke(null, right)!);
        Assert.NotEqual(left[1], right[1]);
    }

    [Theory]
    [InlineData("root_only")]
    [InlineData("mirror_only")]
    [InlineData("other_mail")]
    [InlineData("other_player")]
    [InlineData("wrong_current_player")]
    [InlineData("money")]
    [InlineData("inventory")]
    [InlineData("unknown_progress")]
    [InlineData("mirror_stat")]
    [InlineData("duplicate_flag")]
    [InlineData("preexisting_flag")]
    [InlineData("wrong_option")]
    [InlineData("unverified")]
    [InlineData("stale")]
    [InlineData("wrong_source")]
    [InlineData("wrong_destination")]
    [InlineData("wrong_version")]
    [InlineData("wrong_actor")]
    [InlineData("wrong_save")]
    [InlineData("wrong_hash")]
    [InlineData("wrong_tick")]
    [InlineData("unreadable_mail")]
    [InlineData("unreadable_mirror")]
    [InlineData("wrong_current_marker")]
    [InlineData("duplicate_current_player")]
    public void TutorialEffectCannotExcuseOtherProgressOrUnverifiedNativeContext(string changed)
    {
        var f = new Fixture();
        switch (changed)
        {
            case "root_only": Change(f.After, "world_progress", "game_state_query_unlock_state", node => CurrentMail(node).RemoveAt(1)); break;
            case "mirror_only": Change(f.After, "quests", "mail_received", node => node.AsArray().RemoveAt(1)); break;
            case "other_mail": Change(f.After, "quests", "mail_received", node => node.AsArray().Add("real_reward")); break;
            case "other_player": Change(f.After, "world_progress", "game_state_query_unlock_state", node => node["players"]![1]!["mail_received"]!.AsArray().Add("button_tut_2")); break;
            case "wrong_current_player": Change(f.After, "world_progress", "game_state_query_unlock_state", node => node["current_player_id"] = "other-player"); break;
            case "money": Set(f.After, "player", "money", JsonValue.Create(501)!); break;
            case "inventory": Change(f.After, "player", "inventory", node => node.AsArray().Add(new JsonObject { ["slot_index"] = 8, ["qualified_item_id"] = "(O)388", ["stack"] = 1 })); break;
            case "unknown_progress": Set(f.After, "quests", "unknown_native_progress", JsonValue.Create(1)!); break;
            case "mirror_stat": Change(f.After, "world_progress", "game_state_query_unlock_state", node => node["players"]![0]!["stats"]!["MonstersKilled"] = 1); break;
            case "duplicate_flag": Change(f.After, "quests", "mail_received", node => node.AsArray().Add("button_tut_2")); break;
            case "preexisting_flag": Change(f.Before, "quests", "mail_received", node => node.AsArray().Add("button_tut_2")); break;
            case "wrong_option": f.Item.OptionId = f.Step.OptionId = "executor.wait_ticks"; break;
            case "unverified": f.Step.PrimitiveVerificationStatus = "unverified"; break;
            case "stale": f.Step.AfterSnapshotFresh = false; break;
            case "wrong_source": Set(f.Before, "player", "location_id", JsonValue.Create("Town")!); break;
            case "wrong_destination": Set(f.After, "player", "location_id", JsonValue.Create("Town")!); break;
            case "wrong_version": f.Before.GameVersion = f.After.GameVersion = "1.6.16"; break;
            case "wrong_actor": f.After.PlayerId.Value = "other-player"; break;
            case "wrong_save": f.After.SaveId.Value = "other-save"; break;
            case "unreadable_mail": MarkUnavailable(f.Before, "quests", "mail_received"); MarkUnavailable(f.After, "quests", "mail_received"); break;
            case "unreadable_mirror": MarkUnavailable(f.Before, "world_progress", "game_state_query_unlock_state"); MarkUnavailable(f.After, "world_progress", "game_state_query_unlock_state"); break;
            case "wrong_current_marker":
                Change(f.Before, "world_progress", "game_state_query_unlock_state", node => node["players"]![0]!["is_current"] = false);
                Change(f.After, "world_progress", "game_state_query_unlock_state", node => node["players"]![0]!["is_current"] = false);
                break;
            case "duplicate_current_player":
                Change(f.Before, "world_progress", "game_state_query_unlock_state", node => node["players"]!.AsArray().Add(node["players"]![0]!.DeepClone()));
                Change(f.After, "world_progress", "game_state_query_unlock_state", node => node["players"]!.AsArray().Add(node["players"]![0]!.DeepClone()));
                break;
        }
        f.Stamp();
        if (changed == "wrong_hash") f.Step.AfterStateHash = "different";
        if (changed == "wrong_tick") f.Step.AfterGameTick++;
        Assert.False(f.Matches());
    }

    private static JsonArray CurrentMail(JsonNode node) => node["players"]![0]!["mail_received"]!.AsArray();
    private static void Set(SnapshotEnvelope snapshot, string domain, string field, JsonNode value)
    {
        var group = snapshot.State.TryGetValue(domain, out var raw) ? JsonNode.Parse(raw.GetRawText())!.AsObject() : new JsonObject();
        group[field] = new JsonObject { ["status"] = "available", ["value"] = value };
        snapshot.State[domain] = JsonSerializer.SerializeToElement(group, Json);
    }
    private static void Change(SnapshotEnvelope snapshot, string domain, string field, Action<JsonNode> change)
    {
        var group = JsonNode.Parse(snapshot.State[domain].GetRawText())!.AsObject();
        change(group[field]!["value"]!);
        snapshot.State[domain] = JsonSerializer.SerializeToElement(group, Json);
    }
    private static void MarkUnavailable(SnapshotEnvelope snapshot, string domain, string field)
    {
        var group = JsonNode.Parse(snapshot.State[domain].GetRawText())!.AsObject();
        group[field]!["status"] = "unavailable";
        snapshot.State[domain] = JsonSerializer.SerializeToElement(group, Json);
    }

    private sealed class Fixture
    {
        internal SnapshotEnvelope Before { get; }
        internal SnapshotEnvelope After { get; }
        internal ActionQueueItem Item { get; } = new() { OptionId = "executor.traverse_connector" };
        internal QueueExecutionStepReceipt Step { get; } = new()
        {
            OptionId = "executor.traverse_connector", Status = "applied", AfterSnapshotFresh = true,
            PrimitiveVerificationStatus = "verified"
        };
        internal Fixture()
        {
            Before = MoneyCropTeacherFixture.CreateSnapshot("MoneyCropProductionSnapshot");
            CropTeacherNativeSupportFixture.CompleteResources(Before, 500);
            Before.GameVersion = "1.6.15";
            Set(Before, "player", "location_id", JsonValue.Create("FarmHouse")!);
            Set(Before, "quests", "mail_received", new JsonArray("prior_mail"));
            Set(Before, "world_progress", "game_state_query_unlock_state", new JsonObject
            {
                ["current_player_id"] = Before.PlayerId.Value, ["host_player_id"] = Before.PlayerId.Value,
                ["target_player_resolution_status"] = "current",
                ["players"] = new JsonArray(
                    Player(Before.PlayerId.Value!, true), Player("other-player", false))
            });
            After = JsonSerializer.Deserialize<SnapshotEnvelope>(JsonSerializer.Serialize(Before, Json), Json)!;
            After.GameTick = Before.GameTick + 10;
            Set(After, "player", "location_id", JsonValue.Create("Farm")!);
            Change(After, "quests", "mail_received", node => node.AsArray().Add("button_tut_2"));
            Change(After, "world_progress", "game_state_query_unlock_state", node => CurrentMail(node).Add("button_tut_2"));
            Stamp();
        }
        private static JsonObject Player(string id, bool current) => new()
        {
            ["player_id"] = id, ["is_current"] = current, ["is_host"] = current,
            ["mail_received"] = new JsonArray("prior_mail"), ["mailbox"] = new JsonArray(),
            ["mail_for_tomorrow"] = new JsonArray(), ["stats"] = new JsonObject { ["stepsTaken"] = 0 }
        };
        internal void Stamp()
        {
            Before.StateHash = SnapshotHash.ComputeStateHash(Before.State);
            After.StateHash = SnapshotHash.ComputeStateHash(After.State);
            Step.SourceStateHash = Before.StateHash; Step.AfterStateHash = After.StateHash;
            Step.BeforeGameTick = Before.GameTick; Step.AfterGameTick = After.GameTick;
        }
        internal bool Matches() => (bool)typeof(MoneyRevenueCropSeedAcquisitionProofBuilder)
            .GetMethod("IsVerifiedNativeFarmTutorialStep", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, new object[] { Before, After, Step, Item })!;
    }
}
