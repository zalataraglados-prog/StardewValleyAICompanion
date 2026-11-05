using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Core.Tests;

public sealed class MasterAnglerNativeReceiptProgressTests
{
    [Fact]
    public void ExactNativeCollectionTransitionRetainsOriginalCreditSemantics()
    {
        var transition = Verify(Snapshot(caughtTarget: false), Snapshot(caughtTarget: true));
        Assert.True(transition.Verified);
        Assert.Equal("native_collection_false_to_true", transition.TransitionKind);
        Assert.Equal("False", transition.BeforeValue);
        Assert.Equal("True", transition.AfterValue);
    }

    [Theory]
    [InlineData("shrunk_denominator")]
    [InlineData("unknown_species")]
    [InlineData("duplicate_other_species")]
    [InlineData("missing_other_caught")]
    [InlineData("aggregate_disagreement")]
    [InlineData("missing_list_disagreement")]
    [InlineData("regressed_existing_collection")]
    public void NativeCollectionCreditRejectsFalseAfterStateEvenWhenTargetBecomesCaught(string mode)
    {
        var before = Snapshot(caughtTarget: false);
        var after = Snapshot(caughtTarget: true);
        var world = JsonNode.Parse(after.State["world_progress"].GetRawText())!;
        var value = world["fish_collection_progress"]!["value"]!;
        var items = value["items"]!.AsArray();
        switch (mode)
        {
            case "shrunk_denominator": value["items"] = new JsonArray(items[0]!.DeepClone()); break;
            case "unknown_species":
                items[1]!["item_id"] = "unknown"; items[1]!["qualified_item_id"] = "(O)unknown"; break;
            case "duplicate_other_species": items[2] = items[1]!.DeepClone(); break;
            case "missing_other_caught": items[1]!.AsObject().Remove("caught"); break;
            case "aggregate_disagreement": value["caught_eligible_species_count"] = 71; break;
            case "missing_list_disagreement": value["missing_item_ids"] = new JsonArray("unknown"); break;
            case "regressed_existing_collection":
                items[1]!["caught"] = false;
                value["caught_eligible_species_count"] = 71;
                value["missing_species_count"] = 1;
                value["missing_item_ids"] = new JsonArray("fixture_fish_1");
                break;
        }
        after.State["world_progress"] = JsonSerializer.SerializeToElement(world);
        Assert.False(Verify(before, after).Verified);
    }

    [Fact]
    public void RewardInventoryOrAnUnavailableBeforeProfileCannotStandInForCollectionProgress()
    {
        var before = Snapshot(caughtTarget: false);
        before.State.Remove("world_progress");
        Assert.False(Verify(before, Snapshot(caughtTarget: true)).Verified);
        Assert.False(Verify(Snapshot(caughtTarget: true), Snapshot(caughtTarget: true)).Verified);
    }

    private static PolicyTeacherRequirementTransition Verify(SnapshotEnvelope before, SnapshotEnvelope after)
    {
        var credit = new CurrentCollectionRequirementCredit("master_angler", "fish:fixture_fish_0", 0,
            "(O)fixture_fish_0", "authoritative_window_terminal_attempt", 1, 0, "native_collection");
        var method = typeof(CurrentStageOneCollectionTeacherReceiptBuilder).GetMethod("VerifyTransition",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        return (PolicyTeacherRequirementTransition)method.Invoke(null,
            new object?[] { credit, Array.Empty<ActionQueueItem>(), null, before, after, Inventory() })!;
    }

    private static GoalRequirementSet Inventory() => new()
    {
        RequirementSetId = "master_angler", RequiredGroupCount = 72,
        Groups = Enumerable.Range(0, 72).Select(index => new GoalRequirementGroup
        {
            RequirementId = "fish:fixture_fish_" + index, SelectionRule = "all_required", RequiredAlternativeCount = 1,
            Alternatives = new[] { new GoalRequirementAlternative
                { ItemId = "fixture_fish_" + index, QualifiedItemId = "(O)fixture_fish_" + index, Amount = 1, MatchKind = "item_id" } }
        }).ToArray()
    };

    private static SnapshotEnvelope Snapshot(bool caughtTarget)
    {
        var value = new
        {
            eligible_species_count = 72,
            caught_eligible_species_count = caughtTarget ? 72 : 71,
            missing_species_count = caughtTarget ? 0 : 1,
            missing_item_ids = caughtTarget ? Array.Empty<string>() : new[] { "fixture_fish_0" },
            items = Enumerable.Range(0, 72).Select(index => new
            {
                item_id = "fixture_fish_" + index,
                qualified_item_id = "(O)fixture_fish_" + index,
                caught = index != 0 || caughtTarget
            }).ToArray()
        };
        return new SnapshotEnvelope
        {
            State = new() { ["world_progress"] = JsonSerializer.SerializeToElement(new
                { fish_collection_progress = new { status = "available", value } }) }
        };
    }
}
