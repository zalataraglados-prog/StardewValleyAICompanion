using StardewAI.Contracts.Training;
using StardewAI.Core.Training;
using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Options;
using Registry = StardewAI.Core.OptionRegistry.OptionRegistry;
using StardewAI.Core.Verifier;
using StardewAI.Contracts.Capabilities;

namespace StardewAI.Core.Tests;

public sealed class SinglePlayerPolicyTrainingScopeTests
{
    [Theory]
    [InlineData("training_singleplayer", true)]
    [InlineData("coop_companion", false)]
    [InlineData("", false)]
    public void ConfirmationOnlyRestrictionUsesTheRowsExecutionScope(string mode, bool expected)
    {
        var row = new TrainingFeatureRowEnvelope
        {
            StateFeatures = new FeatureVector
            {
                Categorical = new[] { new CategoricalFeature { Name = "world.mode", Value = mode } }
            },
            ActionFeatures = new ActionFeatureVector { OptionIds = new[] { "museum.donate_items" } }
        };
        var decision = new PolicyTrainingAdmissionFilter().Evaluate(row);
        Assert.Equal(expected, decision.Included);
    }

    [Theory]
    [InlineData(false, "available", "authorized")]
    [InlineData(true, "available", "confirmation_required")]
    [InlineData(null, "available", "confirmation_required")]
    [InlineData(false, "unavailable", "confirmation_required")]
    public void RuntimeConfirmationUsesReadableNativeMultiplayerContext(bool? multiplayer, string status, string expected)
    {
        var option = new Registry().GetRequired("museum.donate_items");
        var candidate = new OptionAvailabilityCandidate { OptionId = option.OptionId, ActorIsHost = true };
        var result = SafetyPolicyGate.Evaluate(option, candidate, Snapshot(multiplayer, status));
        Assert.Equal(expected, result.ExecutionAuthorization);
    }

    [Fact]
    public void CooperativeSnapshotDoesNotInheritTheRequestFeaturesSinglePlayerScope()
    {
        var filter = new PolicyTrainingAdmissionFilter();
        Assert.Contains("museum.donate_items", filter.ForState(TrainingState()).Allowlist);
        Assert.DoesNotContain("museum.donate_items", filter.ForSnapshot(Snapshot(true)).Allowlist);
        Assert.DoesNotContain("museum.donate_items", filter.ForSnapshot(new SnapshotEnvelope()).Allowlist);
        var singlePlayer = filter.ForState(TrainingState());
        Assert.DoesNotContain("museum.donate_items", singlePlayer.ForSnapshot(Snapshot(true)).Allowlist);
        Assert.DoesNotContain("museum.donate_items", singlePlayer.ForSnapshot(new SnapshotEnvelope()).Allowlist);
    }

    [Theory]
    [InlineData("executor.donate_museum_item")]
    [InlineData("quest.advance")]
    public void SinglePlayerStillRequiresPolicyAndNativeEvidence(string optionId)
    {
        Assert.Empty(new PolicyTrainingAdmissionFilter().ForState(TrainingState()).FilterOptionIds(new[] { optionId }));
    }

    [Fact]
    public void ExplicitAllowlistIsNotExpandedByMode()
    {
        var filter = new PolicyTrainingAdmissionFilter(new[] { "social.gift_npc" });
        Assert.Same(filter, filter.ForState(TrainingState()));
        Assert.Empty(filter.ForState(TrainingState()).FilterOptionIds(new[] { "museum.donate_items" }));
    }

    [Fact]
    public void SinglePlayerOnlyRemovesConfirmationOnlyExclusions()
    {
        var filter = new PolicyTrainingAdmissionFilter();
        var conservative = filter.Allowlist.ToHashSet(StringComparer.Ordinal);
        var singlePlayer = filter.ForState(TrainingState()).Allowlist.ToHashSet(StringComparer.Ordinal);
        Assert.True(conservative.IsSubsetOf(singlePlayer));
        foreach (var id in singlePlayer.Except(conservative))
        {
            var reasons = OptionCapabilityRegistrySource.GetRequired(id).TrainingExclusionReasons;
            Assert.NotEmpty(reasons);
            Assert.All(reasons, reason => Assert.Equal(
                TrainingAdmissionExclusionReason.ExplicitPlayerConfirmationRequired, reason));
        }
    }

    [Fact]
    public void DuplicateTrainingModeDoesNotProveSinglePlayerScope()
    {
        var features = TrainingState();
        features.Categorical = features.Categorical.Concat(features.Categorical).ToArray();
        Assert.Empty(new PolicyTrainingAdmissionFilter().ForState(features)
            .FilterOptionIds(new[] { "museum.donate_items" }));
    }

    [Fact]
    public void SinglePlayerConfirmationExceptionDoesNotBypassCommandOnlyInvocation()
    {
        var option = new Registry().GetRequired("quest.cancel");
        var result = SafetyPolicyGate.Evaluate(option,
            new OptionAvailabilityCandidate { OptionId = option.OptionId, ActorIsHost = true }, Snapshot(false));
        Assert.Equal("denied", result.ExecutionAuthorization);
        Assert.Contains("player_command_only_option_requires_player_command_source", result.BlockingReasons);
    }

    [Fact]
    public void BaselineTrainingAndRankingUseTheSameSinglePlayerScope()
    {
        var root = Path.Combine(Path.GetTempPath(), "stardewai-scope-baseline", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "rows.jsonl");
        var row = new TrainingFeatureRowEnvelope
        {
            StateFeatures = TrainingState(),
            ActionFeatures = new ActionFeatureVector { OptionIds = new[] { "museum.donate_items" } }
        };
        File.WriteAllText(path, JsonSerializer.Serialize(row));
        var report = new BaselineFeatureRowTrainer().Train(path);
        Assert.Equal(1, report.IncludedRowCount);
        Assert.Contains("museum.donate_items", report.TrainingAllowlist);
        Assert.Equal(new[] { "training_singleplayer" }, report.TrainingAdmissionScopes);
        var ranker = new BaselineOptionRanker();
        Assert.Equal("museum.donate_items", ranker.Rank(report, new[] { "museum.donate_items" }, Snapshot(false))
            .RankedOptions.Single().OptionId);
        Assert.Empty(ranker.Rank(report, new[] { "museum.donate_items" }, Snapshot(true)).RankedOptions);
    }

    private static FeatureVector TrainingState() => new()
    {
        Categorical = new[] { new CategoricalFeature { Name = "world.mode", Value = "training_singleplayer" } }
    };

    private static SnapshotEnvelope Snapshot(bool? multiplayer, string status = "available") => new()
    {
        State = new Dictionary<string, JsonElement>
        {
            ["player"] = JsonSerializer.SerializeToElement(new
            {
                multiplayer_runtime = new { status, value = new { context_is_multiplayer = multiplayer } }
            })
        }
    };
}
