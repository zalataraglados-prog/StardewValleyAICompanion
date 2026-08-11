using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed class QuestRewardClaimMainlineTests
{
    [Fact]
    public void ExactClaimableRewardCompilesToSingleNativeQuestLogPrimitive()
    {
        var snapshot = Snapshot(menuOpen: false, reward: 750);
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(
            snapshot,
            new[] { "quest.claim_reward" },
            includeExecutorCalibrationOptions: true);
        var candidate = Assert.Single(Assert.Single(availability.Options).EventCandidates);

        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Equal("claim_quest_reward", candidate.Kind);
        Assert.Contains(candidate.Parameters, value =>
            value.Name == "quest_reward_fingerprint" && value.Value == Fingerprint(750));
        Assert.Contains(candidate.Parameters, value =>
            value.Name == "expected_money_before" && value.Value == "1250");

        var ranked = new EventCandidateRanker().Rank(new(), availability);
        var plan = new DailyPlanCompiler().Compile(ranked, snapshot.StateHash);
        var step = Assert.Single(plan.Steps);
        Assert.Equal("claim_quest_reward", step.Kind);
        var queue = new ActionQueueCompiler().Compile(plan, snapshot);
        var item = Assert.Single(queue.Items);
        Assert.Equal("executor.claim_quest_reward", item.OptionId);
        Assert.Empty(item.BlockingReasons);
        Assert.Equal("claim_quest_reward", Assert.Single(item.NormalizedCommand.Steps).StepType);
    }

    [Fact]
    public void ClaimableRewardIsExcludedUpstreamWhenAnotherMenuIsOpen()
    {
        var snapshot = Snapshot(menuOpen: true, reward: 750);
        var candidate = Assert.Single(new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "quest.claim_reward" }, true)
            .Options.Single().EventCandidates);

        Assert.False(candidate.Available);
        Assert.Contains("quest_reward_requires_clear_menu", candidate.BlockReasons);
    }

    [Fact]
    public void CompilerRejectsRewardAmountDrift()
    {
        var before = Snapshot(menuOpen: false, reward: 750);
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(
            before,
            new[] { "quest.claim_reward" },
            true);
        var plan = new DailyPlanCompiler().Compile(
            new EventCandidateRanker().Rank(new(), availability),
            before.StateHash);
        var drifted = Snapshot(menuOpen: false, reward: 500);

        var item = Assert.Single(new ActionQueueCompiler().Compile(plan, drifted).Items);
        Assert.Contains("quest_reward_claimable_identity_missing", item.BlockingReasons);
    }

    [Fact]
    public void RuntimeUsesOnlyNativeQuestLogClicksForRewardMutation()
    {
        var source = File.ReadAllText(FindRepositoryFile(
            "tools", "StardewAI.RuntimeTestHarness", "ModEntry.QuestRewards.cs"));
        Assert.Contains("new QuestLog()", source, StringComparison.Ordinal);
        Assert.Contains("receiveLeftClick", source, StringComparison.Ordinal);
        Assert.Contains("QuestLogPagesField.GetValue", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.player.Money +=", source, StringComparison.Ordinal);
        Assert.DoesNotContain("moneyReward.Value =", source, StringComparison.Ordinal);
        Assert.DoesNotContain("destroy.Value =", source, StringComparison.Ordinal);
        Assert.DoesNotContain("OnMoneyRewardClaimed()", source, StringComparison.Ordinal);

        var bridge = File.ReadAllText(FindRepositoryFile(
            "src", "StardewAI.TransparentBridge", "Adapters", "ProgressReadAdapter.QuestRewards.cs"));
        Assert.Contains("quest.ShouldDisplayAsComplete()", bridge, StringComparison.Ordinal);
        Assert.Contains("quest.HasMoneyReward()", bridge, StringComparison.Ordinal);
        Assert.Contains("quest.IsHidden()", bridge, StringComparison.Ordinal);
    }

    private static string Fingerprint(int reward) => QuestRewardClaimIdentity.Compute(
        "reward-fixture",
        "ResourceCollectionQuest",
        "Reward fixture",
        reward,
        42,
        true);

    private static SnapshotEnvelope Snapshot(bool menuOpen, int reward)
    {
        var json = $$"""
        {
          "player": {
            "money":{"value":1250,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "quests": {
            "active_quests":{"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "claimable_rewards":{"value":[{"reward_fingerprint":"{{Fingerprint(reward)}}","quest":{"id":"reward-fixture","title":"Reward fixture","runtime_type":"ResourceCollectionQuest","completed":true,"hidden":false,"daily_quest":true,"day_quest_accepted":42,"money_reward":{{reward}}},"claimable":true,"status":"ready","blocked_diagnostics":[]}],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "menus": {
            "active_menu":{"value":{"is_open":{{menuOpen.ToString().ToLowerInvariant()}},"type":"{{(menuOpen ? "ShopMenu" : "none")}}"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """;
        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, JsonOptions)!;
        return new SnapshotEnvelope
        {
            StateHash = SnapshotHash.ComputeStateHash(state),
            GameTick = 1,
            RealTimestamp = "2026-08-11T00:00:00Z",
            Completeness = "complete",
            State = state
        };
    }

    private static string FindRepositoryFile(params string[] segments)
    {
        var directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        while (directory is not null &&
            !File.Exists(Path.Combine(directory.FullName, "StardewValleyAICompanion.sln")))
        {
            directory = directory.Parent;
        }
        return Path.Combine(
            directory?.FullName ?? throw new InvalidOperationException("Cannot find repository root."),
            Path.Combine(segments));
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}
