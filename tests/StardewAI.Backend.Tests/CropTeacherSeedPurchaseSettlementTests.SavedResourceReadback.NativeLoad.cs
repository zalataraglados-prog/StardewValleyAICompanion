using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.Strategy;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SavedResourceReadback_NativeLoadInitializationKeepsOriginalSnapshotsAndRebind(bool wallet)
    {
        var f = NativeLoadResourceFixture(wallet);
        var source = JsonSerializer.Serialize(f.Before, JsonOptions);
        var loaded = JsonSerializer.Serialize(f.After, JsonOptions);
        Assert.Throws<InvalidDataException>(() => f.Open());
        var leaf = OpenNativeLoad(f);
        Assert.True(leaf.SnapshotResourceContentMatches);
        Assert.False(leaf.NativeReloadVerified); Assert.False(leaf.SemanticSavedContentVerified);
        Assert.False(leaf.FormalProductTrainingAuthorized);
        var reply = new ReservationPortfolioLedgerService().Commit(f.Ledger, f.After, leaf.RebindRequest, RecordedAt);
        Assert.True(reply.Accepted, string.Join(",", reply.Errors));
        Assert.Equal(f.After.StateHash, leaf.VerifyRebindReply(reply).SourceStateHash);
        Assert.Equal(source, JsonSerializer.Serialize(f.Before, JsonOptions));
        Assert.Equal(loaded, JsonSerializer.Serialize(f.After, JsonOptions));
        var projector = typeof(CropTeacherSavedResourceReadback).Assembly.GetType("StardewAI.GoalConditionedBootstrap.FullShipmentRecoverySettlementBoundaryProjector")!
            .GetMethod("TryProject", BindingFlags.NonPublic | BindingFlags.Static)!;
        object Projection(SnapshotEnvelope snapshot)
        {
            object?[] arguments = [snapshot, null];
            Assert.True((bool)projector.Invoke(null, arguments)!);
            return arguments[1]!;
        }
        Assert.NotEqual(Projection(f.Before), Projection(f.After));
        leaf.ValidateInputsUnchanged();
    }

    [Theory]
    [InlineData("hay")]
    [InlineData("capacity")]
    [InlineData("building")]
    [InlineData("type")]
    [InlineData("name")]
    [InlineData("unknown")]
    [InlineData("duplicate")]
    [InlineData("noncanonical")]
    [InlineData("ordinary_location")]
    [InlineData("existing_row")]
    [InlineData("removed_row")]
    [InlineData("unreadable")]
    public void SavedResourceReadback_NativeLoadCellarsNeverHideRealOrUnknownStorageChanges(string fault)
    {
        var f = NativeLoadResourceFixture();
        ChangeState(f.After, "farm", farm =>
        {
            var rows = farm["hay_storage"]!["value"]!["locations"]!.AsArray();
            var added = rows.Last()!.AsObject();
            switch (fault)
            {
                case "hay": added["stored_hay"] = 1; break;
                case "capacity": added["hay_capacity"] = 240; break;
                case "building": added["building_count"] = 1; break;
                case "type": added["runtime_type"] = "Modded.Cellar"; break;
                case "name": added["name"] = "Other"; break;
                case "unknown": added["new_resource"] = 1; break;
                case "duplicate": rows.Add(added.DeepClone()); break;
                case "noncanonical": added["location_id"] = "Cellar02"; added["name"] = "Cellar02"; break;
                case "ordinary_location": added["location_id"] = "Forest"; added["name"] = "Forest"; break;
                case "existing_row": rows[0]!["stored_hay"] = 5; break;
                case "removed_row": rows.RemoveAt(0); break;
                case "unreadable": farm["hay_storage"]!["status"] = "unavailable"; break;
            }
        });
        f.Save();
        Assert.Throws<InvalidDataException>(() => OpenNativeLoad(f));
    }

    [Theory]
    [InlineData("money")]
    [InlineData("crop")]
    [InlineData("unknown")]
    public void SavedResourceReadback_NativeLoadInitializationStillRejectsOtherPersistentDrift(string fault)
    {
        var f = NativeLoadResourceFixture(wallet: true);
        if (fault == "crop") ChangeState(f.After, "farm", farm => farm["crops"]!["value"]!.AsArray().Add(new JsonObject { ["unexpected_crop"] = true }));
        else ChangeState(f.After, "player", player =>
        {
            if (fault == "money") player["money"]!["value"] = player["money"]!["value"]!.GetValue<int>() + 1;
            else player["unknown_saved_resource"] = JsonSerializer.SerializeToNode(new { status = "available", value = 1 });
        });
        f.Save();
        Assert.Contains("persistent resources differ", Assert.Throws<InvalidDataException>(() => OpenNativeLoad(f)).Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("other_version")]
    [InlineData("other_season")]
    [InlineData("other_object")]
    [InlineData("unexpected_target")]
    [InlineData("wrong_qualified_id")]
    [InlineData("tile")]
    [InlineData("stack")]
    [InlineData("held_item")]
    [InlineData("object_removed")]
    public void SavedResourceReadback_NativeSummerWeedLoadOnlyIgnoresNativeIdChanges(string? fault)
    {
        var f = new SavedResourceFixture();
        var season = fault == "other_season" ? "spring" : "summer";
        JsonObject ObjectField(string id, bool after)
        {
            var row = new JsonObject
            {
                ["tile_x"] = after && fault == "tile" ? 8 : 7, ["tile_y"] = 7,
                ["item_id"] = id, ["qualified_item_id"] = after && fault == "wrong_qualified_id" ? "(O)343" : "(O)" + id,
                ["name"] = "Weeds", ["display_name"] = "Weeds", ["stack"] = after && fault == "stack" ? 2 : 1,
                ["big_craftable"] = false, ["held_item"] = after && fault == "held_item" ? new JsonObject { ["item_id"] = "388" } : null
            };
            return new JsonObject
            {
                ["status"] = "available", ["value"] = after && fault == "object_removed" ? new JsonArray() : new JsonArray(row)
            };
        }

        f.Before.GameVersion = f.After.GameVersion = fault == "other_version" ? "1.6.14" : "1.6.15";
        foreach (var snapshot in new[] { f.Before, f.After })
            ChangeState(snapshot, "time", time => time["season"]!["value"] = season);
        ChangeState(f.Before, "farm", farm => farm["objects"] = ObjectField(fault == "other_object" ? "343" : "674", false));
        ChangeState(f.After, "farm", farm => farm["objects"] = ObjectField(fault == "unexpected_target" ? "678" : "676", true));
        f.Ledger.SourceStateHash = f.Before.StateHash;
        f.Save();
        var rawAfter = File.ReadAllBytes(f.AfterPath);
        Assert.Throws<InvalidDataException>(() => f.Open());
        if (fault is not null)
        {
            Assert.Contains(fault == "other_version" ? "saved-resource before snapshot is invalid" : "farm.objects",
                Assert.Throws<InvalidDataException>(() => OpenNativeLoad(f)).Message);
            return;
        }

        var readback = OpenNativeLoad(f);
        Assert.True(readback.SnapshotResourceContentMatches);
        Assert.False(readback.NativeReloadVerified);
        Assert.False(readback.FormalProductTrainingAuthorized);
        Assert.Equal(rawAfter, File.ReadAllBytes(f.AfterPath));
    }

    private static CropTeacherSavedResourceReadback OpenNativeLoad(SavedResourceFixture f) =>
        CropTeacherSavedResourceReadback.OpenNativeLoad(f.BeforePath, f.AfterPath, f.LedgerPath, SavedResourceFixture.Step);

    private static SavedResourceFixture NativeLoadResourceFixture(bool wallet = false)
    {
        var f = new SavedResourceFixture();
        JsonObject StorageRow(string id, bool farm = false) => new()
        {
            ["location_id"] = id, ["name"] = id,
            ["runtime_type"] = farm ? "StardewValley.Farm" : "StardewValley.Locations.Cellar",
            ["building_count"] = farm ? 1 : 0, ["stored_hay"] = 0, ["hay_capacity"] = farm ? 240 : 0,
            ["capacity_status"] = "exact_native_getter"
        };
        foreach (var snapshot in new[] { f.Before, f.After })
            ChangeState(snapshot, "farm", farm => farm["hay_storage"] = new JsonObject
            {
                ["status"] = "available", ["value"] = new JsonObject
                {
                    ["schema_version"] = "hay_storage_inventory.v1", ["status"] = "available",
                    ["location_scope"] = "noninterior_locations_and_farm", ["diagnostics"] = new JsonArray(),
                    ["locations"] = new JsonArray(StorageRow("Farm", farm: true), StorageRow("Cellar"))
                }
            });
        ChangeState(f.After, "farm", farm => farm["hay_storage"]!["value"]!["locations"]!.AsArray().Add(StorageRow("Cellar2")));
        if (wallet)
        {
            var money = f.Before.State["player"].GetProperty("money").GetProperty("value").GetInt32();
            foreach (var snapshot in new[] { f.Before, f.After })
                ChangeState(snapshot, "player", player => player["multiplayer_wallet"] = new JsonObject
                {
                    ["status"] = "available", ["value"] = new JsonObject
                    {
                        ["schema_version"] = "multiplayer_wallet.v1", ["projection_status"] = "complete_locked_base_1.6.15",
                        ["projection_fingerprint"] = new string('a', 64), ["local_player_id"] = f.Before.PlayerId.Value,
                        ["wallet_mode"] = "shared", ["use_separate_wallets"] = false, ["shared_money"] = money,
                        ["local_effective_money"] = money, ["total_money_gifted"] = 0,
                        ["participants"] = new JsonArray(new JsonObject
                        {
                            ["player_id"] = f.Before.PlayerId.Value, ["is_local_player"] = true,
                            ["individual_entry_exists"] = false, ["individual_balance"] = null,
                            ["native_default_if_missing"] = null, ["effective_balance"] = money
                        }),
                        ["recipients"] = new JsonArray(), ["unknown_future_invariant"] = 7
                    }
                });
            ChangeState(f.After, "player", player =>
            {
                var value = player["multiplayer_wallet"]!["value"]!;
                value["participants"]![0]!["individual_entry_exists"] = true;
                value["participants"]![0]!["individual_balance"] = money;
                value["projection_fingerprint"] = new string('b', 64);
            });
        }
        f.Ledger.SourceStateHash = SnapshotHash.ComputeStateHash(f.Before.State);
        f.Save();
        return f;
    }
}
