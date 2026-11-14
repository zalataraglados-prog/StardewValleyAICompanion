using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Backend.Tests;

public sealed class CropTeacherNativeWalletLoadComparisonTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NativeInitializationProjectsOnlyValueAndLeavesInputsUnmodified(bool separate)
    {
        var (before, after) = Wallets(separate);
        var beforeText = before.ToJsonString();
        var afterText = after.ToJsonString();
        var projected = Project(before, after);
        var expected = after.DeepClone();
        expected["value"] = before["value"]!.DeepClone();
        Assert.True(JsonNode.DeepEquals(expected, JsonNode.Parse(projected.GetRawText())));
        Assert.Equal(beforeText, before.ToJsonString());
        Assert.Equal(afterText, after.ToJsonString());
        Assert.Equal("loaded observation", projected.GetProperty("evidence").GetString());
    }

    [Fact]
    public void UnchangedOldAndAbsentValuesDoNotRequireNewWalletFields()
    {
        foreach (var text in new[] { "{}", "{\"status\":\"unavailable\"}", "{\"status\":\"available\",\"value\":null}",
                     "{\"status\":\"available\",\"value\":{\"old_wallet\":true}}" })
        {
            var before = JsonNode.Parse(text)!;
            var after = before.DeepClone();
            after["evidence"] = "after metadata remains";
            Assert.True(JsonNode.DeepEquals(after, JsonNode.Parse(Project(before, after).GetRawText())));
        }
    }

    [Theory]
    [InlineData("money")]
    [InlineData("participant")]
    [InlineData("mode")]
    [InlineData("stat")]
    [InlineData("unknown")]
    [InlineData("fingerprint_only")]
    [InlineData("wrong_balance")]
    [InlineData("wrong_default")]
    [InlineData("recipient")]
    [InlineData("unavailable")]
    [InlineData("duplicate_ids")]
    [InlineData("malformed_balance")]
    [InlineData("bad_fingerprint")]
    [InlineData("missing_value")]
    [InlineData("malformed_entry")]
    public void RejectsAnythingBeyondExactNativeInitialization(string change)
    {
        var (before, after) = Wallets(false);
        var value = after["value"]!;
        switch (change)
        {
            case "money": value["shared_money"] = 21; break;
            case "participant": value["participants"]![1]!["player_id"] = "intruder"; break;
            case "mode": value["wallet_mode"] = "separate"; break;
            case "stat": value["total_money_gifted"] = 1; break;
            case "unknown": value["future"]!["guarded"] = false; break;
            case "fingerprint_only": before["value"] = value.DeepClone(); before["value"]!["projection_fingerprint"] = new string('a', 64); break;
            case "wrong_balance": value["participants"]![0]!["individual_balance"] = 500; break;
            case "wrong_default": value["participants"]![0]!["native_default_if_missing"] = 500; break;
            case "recipient": value["recipients"]![0]!["balance"] = 21; break;
            case "unavailable": before["status"] = "unavailable"; break;
            case "duplicate_ids": before["value"]!["participants"]![1]!["player_id"] = "1"; break;
            case "malformed_balance": before["value"]!["participants"]![0]!["effective_balance"] = 20.5; break;
            case "bad_fingerprint": value["projection_fingerprint"] = "not-a-checksum"; break;
            case "missing_value": before.AsObject().Remove("value"); break;
            case "malformed_entry": before["value"]!["participants"]![0]!["individual_entry_exists"] = "false"; break;
        }
        var error = Assert.Throws<TargetInvocationException>(() => Project(before, after));
        Assert.IsType<InvalidDataException>(error.InnerException);
    }

    private static JsonElement Project(JsonNode before, JsonNode after)
    {
        var method = typeof(CropTeacherSavedResourceReadback).Assembly
            .GetType("StardewAI.GoalConditionedBootstrap.CropTeacherNativeWalletLoadComparison", throwOnError: true)!
            .GetMethod("ProjectEnvelope", BindingFlags.Static | BindingFlags.NonPublic)!;
        return (JsonElement)method.Invoke(null, new object[] { JsonSerializer.SerializeToElement(before), JsonSerializer.SerializeToElement(after) })!;
    }

    private static (JsonNode Before, JsonNode After) Wallets(bool separate)
    {
        // Independent literal observations of the pinned load assignment, not helper-produced expectations.
        var before = JsonNode.Parse("""
            {"status":"available","evidence":"saved observation","value":{
              "schema_version":"multiplayer_wallet.v1","projection_status":"complete_locked_base_1.6.15",
              "projection_fingerprint":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
              "wallet_mode":"shared","use_separate_wallets":false,"local_player_id":"1",
              "shared_money":20,"local_effective_money":20,"total_money_gifted":0,
              "commands":[{"operation":"transfer","available":false}],"future":{"guarded":true},
              "participants":[
                {"player_id":"1","is_local_player":true,"effective_balance":20,"individual_entry_exists":false,"individual_balance":null,"native_default_if_missing":null},
                {"player_id":"2","is_local_player":false,"effective_balance":20,"individual_entry_exists":true,"individual_balance":35,"native_default_if_missing":null}],
              "recipients":[{"player_id":"2","balance":20,"individual_entry_exists":true,"response_key":"Transfer1"}]}}
            """)!;
        var after = JsonNode.Parse("""
            {"status":"available","evidence":"loaded observation","value":{
              "schema_version":"multiplayer_wallet.v1","projection_status":"complete_locked_base_1.6.15",
              "projection_fingerprint":"bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
              "wallet_mode":"shared","use_separate_wallets":false,"local_player_id":"1",
              "shared_money":20,"local_effective_money":20,"total_money_gifted":0,
              "commands":[{"operation":"transfer","available":false}],"future":{"guarded":true},
              "participants":[
                {"player_id":"1","is_local_player":true,"effective_balance":20,"individual_entry_exists":true,"individual_balance":20,"native_default_if_missing":null},
                {"player_id":"2","is_local_player":false,"effective_balance":20,"individual_entry_exists":true,"individual_balance":20,"native_default_if_missing":null}],
              "recipients":[{"player_id":"2","balance":20,"individual_entry_exists":true,"response_key":"Transfer1"}]}}
            """)!;
        if (separate)
        {
            foreach (var envelope in new[] { before, after })
            {
                var v = envelope["value"]!;
                v["wallet_mode"] = "separate"; v["use_separate_wallets"] = true;
                v["local_effective_money"] = 500;
                v["participants"]![0]!["effective_balance"] = 500;
                v["participants"]![1]!["effective_balance"] = 500;
                v["recipients"]![0]!["balance"] = 500;
            }
            before["value"]!["participants"]![0]!["native_default_if_missing"] = 500;
            before["value"]!["participants"]![1]!["individual_entry_exists"] = false;
            before["value"]!["participants"]![1]!["individual_balance"] = null;
            before["value"]!["participants"]![1]!["native_default_if_missing"] = 500;
            before["value"]!["recipients"]![0]!["individual_entry_exists"] = false;
            after["value"]!["participants"]![0]!["individual_balance"] = 500;
            after["value"]!["participants"]![1]!["individual_balance"] = 500;
        }
        return (before, after);
    }
}
