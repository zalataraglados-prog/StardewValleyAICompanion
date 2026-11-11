using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Training;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Backend.Tests;

public sealed class NativeWildTreeClearanceBoundaryTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(90)]
    [InlineData(374)]
    [InlineData(15000)]
    public void ReconcilesNativeTreeProgressAndOriginalPersistentResourcesWithoutToolXml(int experience)
    {
        var (request, receipt, before, after) = Fixture(experience);
        var firstHash = before.StateHash; var nextHash = after.StateHash;
        var result = Read(request, receipt, before, after);
        Assert.True(result.SpecificPersistentResourceBoundaryVerified);
        Assert.True(result.Terrain.TerrainRemovalVerified);
        Assert.True(result.Terrain.Consumption.StaminaContinuityVerified);
        Assert.False(result.ProducerProvenanceVerified);
        Assert.False(result.ResourceCreditAuthorized);
        Assert.False(result.NativeOutcomeVerified);
        Assert.False(result.FormalProductTrainingAuthorized);
        Assert.False(request.ActionOriginInventoryPayload);
        Assert.Equal(firstHash, before.StateHash); Assert.Equal(nextHash, after.StateHash);
        Assert.Equal(firstHash, SnapshotHash.ComputeStateHash(before.State));
        Assert.Equal(nextHash, SnapshotHash.ComputeStateHash(after.State));
    }

    [Theory]
    [InlineData("xp")]
    [InlineData("xp-next-level")]
    [InlineData("pending-level")]
    [InlineData("unrelated-skill")]
    [InlineData("profession")]
    [InlineData("level")]
    [InlineData("stat")]
    [InlineData("foreign-stat")]
    [InlineData("foreign-player")]
    [InlineData("request-xp")]
    [InlineData("request-stat")]
    [InlineData("request-contract")]
    [InlineData("tool-target")]
    [InlineData("inventory")]
    [InlineData("currency")]
    [InlineData("unknown-persistent")]
    [InlineData("energy-envelope")]
    public void RefusesUnaccountedOrContradictoryTreeEffects(string change)
    {
        var (request, receipt, before, after) = Fixture(90);
        switch (change)
        {
            case "xp": Edit(after, "player", "skills_detail", node => node["skills"]![2]!["experience"] = 107); break;
            case "xp-next-level": Edit(after, "player", "skills_detail", node => node["skills"]![2]!["experience_to_next_level"] = 99); break;
            case "pending-level": Edit(after, "player", "skills_detail", node => node["new_levels"] = new JsonArray()); break;
            case "unrelated-skill": Edit(after, "player", "skills_detail", node => node["skills"]![0]!["experience"] = 101); break;
            case "profession": Edit(after, "player", "skills_detail", node => node["profession_ids"]!.AsArray().Add(12)); break;
            case "level": Put(after, "player", "level", JsonValue.Create(99)!); break;
            case "stat": Edit(after, "world_progress", "game_state_query_unlock_state", node => node["players"]![0]!["stats"]!["TreesChopped"] = 2); break;
            case "foreign-stat": Edit(after, "world_progress", "game_state_query_unlock_state", node => node["players"]![0]!["stats"]!["foreign"] = 1); break;
            case "foreign-player": Edit(after, "world_progress", "game_state_query_unlock_state", node => node["players"]![1]!["stats"]!["TreesChopped"] = 1); break;
            case "request-xp": request.ExpectedForagingExperienceDelta = 17; break;
            case "request-stat": request.ExpectedTreesChoppedBefore = 1; break;
            case "request-contract": request.TreeChopProjectionStatus = "guessed"; break;
            case "tool-target": receipt.ClearanceToolCalls![0].X = 9999; break;
            case "inventory": Edit(after, "player", "inventory", node => node[0]!["special_state"] = "changed"); break;
            case "currency": Put(after, "player", "money", JsonValue.Create(501)!); break;
            case "unknown-persistent": Put(after, "farm", "unknown", JsonValue.Create("changed")!); break;
            case "energy-envelope":
                var fields = JsonNode.Parse(after.State["player"].GetRawText())!;
                fields["energy"]!["future_semantic"] = "changed";
                after.State["player"] = JsonSerializer.SerializeToElement(fields); break;
        }
        Rebind(request, receipt, before, after);
        Assert.Throws<InvalidDataException>(() => Read(request, receipt, before, after));
    }

    [Fact]
    public void FarmTerrainCannotBeUsedAsTownClearanceEvidence()
    {
        var (request, receipt, before, after) = Fixture(0);
        foreach (var snapshot in new[] { before, after }) Put(snapshot, "player", "location_id", JsonValue.Create("Town")!);
        receipt.LocationId = "Town";
        foreach (var call in receipt.ClearanceToolCalls!) call.LocationId = "Town";
        Rebind(request, receipt, before, after);
        Assert.True(NativeOutputObservationBoundary.ReadClearanceConsumption(request, receipt, before, after, "Town").StaminaContinuityVerified);
        Assert.Throws<InvalidDataException>(() => NativeOutputObservationBoundary.ReadClearanceTerrain(request, receipt, before, after, "Town"));
    }

    private static NativeWildTreeClearanceSummary Read(TrainingExecutionRequest request, ActionOriginObservationReceipt receipt,
        SnapshotEnvelope before, SnapshotEnvelope after) => NativeOutputObservationBoundary.ReadWildTreeClearanceResources(request, receipt, before, after, "Farm");

    private static (TrainingExecutionRequest Request, ActionOriginObservationReceipt Receipt, SnapshotEnvelope Before, SnapshotEnvelope After) Fixture(int experience)
    {
        var f = NativeClearanceTerrainBoundaryTests.Fixture();
        f.Receipt.Collections = [];
        f.Receipt.InventoryAfter[0].Quantity = 10;
        Edit(f.After, "player", "inventory", rows => rows[0]!["stack"] = 10);
        f.Request.ClearCompletionMode = "wild_tree_removed";
        f.Request.TreeChopProjectionStatus = "exact_live_tree_and_locked_wild_tree_chop_domain";
        f.Request.TreeChopDataContractStatus = "exact_locked_base_1.6.15_chop";
        f.Request.TreeChopProtectionStatus = "unprotected";
        f.Request.ExpectedForagingExperienceBefore = experience;
        f.Request.ExpectedForagingExperienceDelta = 16;
        f.Request.ExpectedForagingExperienceAfter = experience + 16;
        f.Request.ExpectedTreesChoppedBefore = 0; f.Request.ExpectedTreesChoppedDelta = 1; f.Request.ExpectedTreesChoppedAfter = 1;
        foreach (var call in f.Receipt.ClearanceToolCalls!) { call.X = 10 * 64; call.Y = 11 * 64; }
        var ids = new[] { "farming", "fishing", "foraging", "mining", "combat", "luck" };
        int Level(int xp) => Enumerable.Range(1, 10).Count(level => NativeSkillExperienceRules.ExperienceForLevel(level) <= xp);
        foreach (var snapshot in new[] { f.Before, f.After })
        {
            var isAfter = ReferenceEquals(snapshot, f.After);
            var xp = experience + (isAfter ? 16 : 0); var treeLevel = Level(xp);
            var rows = Enumerable.Range(0, 6).Select(index =>
            {
                var current = index == 2 ? treeLevel : index == 0 ? 1 : 0;
                var currentXp = index == 2 ? xp : index == 0 ? 100 : 0;
                int? next = current < 10 ? current + 1 : null;
                int? threshold = next.HasValue ? NativeSkillExperienceRules.ExperienceForLevel(next.Value) : null;
                return new { skill_id = ids[index], skill_index = index, unmodified_level = current, effective_level = current,
                    temporary_buff_delta = 0, experience = currentXp, next_level = next, next_level_experience = threshold,
                    experience_to_next_level = threshold - currentXp, at_vanilla_level_cap = current == 10 };
            }).ToArray();
            var pending = new JsonArray();
            if (isAfter) for (var level = Level(experience) + 1; level <= treeLevel; level++)
                pending.Add(new JsonObject { ["skill_index"] = 2, ["level"] = level });
            var score = (treeLevel + 1) / 2;
            Put(snapshot, "player", "level", JsonValue.Create(score)!);
            Put(snapshot, "player", "skills_detail", new JsonObject { ["scoring_level"] = score, ["vanilla_level_cap"] = 10,
                ["skills"] = JsonSerializer.SerializeToNode(rows), ["new_levels"] = pending, ["profession_ids"] = new JsonArray() });
            foreach (var name in new[] { "money", "club_coins", "total_money_earned" }) Put(snapshot, "player", name, JsonValue.Create(500)!);
            Put(snapshot, "player", "shop_currency_balances", new JsonObject());
            Put(snapshot, "farm", "shipping_bins", new JsonObject());
            Put(snapshot, "farm", "unknown", JsonValue.Create("retained")!);
            Put(snapshot, "world_progress", "game_state_query_unlock_state", new JsonObject { ["current_player_id"] = "123", ["players"] = new JsonArray(
                new JsonObject { ["player_id"] = "123", ["is_current"] = true, ["stats"] = isAfter ? new JsonObject { ["TreesChopped"] = 1 } : new JsonObject() },
                new JsonObject { ["player_id"] = "999", ["is_current"] = false, ["stats"] = new JsonObject() }) });
        }
        Rebind(f.Request, f.Receipt, f.Before, f.After);
        return f;
    }

    private static void Put(SnapshotEnvelope snapshot, string domain, string name, JsonNode value)
    {
        var fields = snapshot.State.TryGetValue(domain, out var raw) ? JsonNode.Parse(raw.GetRawText())!.AsObject() : new JsonObject();
        fields[name] = new JsonObject { ["status"] = "available", ["value"] = value };
        snapshot.State[domain] = JsonSerializer.SerializeToElement(fields);
    }
    private static void Edit(SnapshotEnvelope snapshot, string domain, string name, Action<JsonNode> change)
    {
        var fields = JsonNode.Parse(snapshot.State[domain].GetRawText())!;
        change(fields[name]!["value"]!);
        snapshot.State[domain] = JsonSerializer.SerializeToElement(fields);
    }
    private static void Rebind(TrainingExecutionRequest request, ActionOriginObservationReceipt receipt, SnapshotEnvelope before, SnapshotEnvelope after)
    {
        before.StateHash = SnapshotHash.ComputeStateHash(before.State); after.StateHash = SnapshotHash.ComputeStateHash(after.State);
        request.BeforeStateHash = receipt.BeforeStateHash = before.StateHash;
    }
}
