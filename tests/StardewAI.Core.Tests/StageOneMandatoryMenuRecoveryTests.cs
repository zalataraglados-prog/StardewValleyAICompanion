using StardewAI.Contracts.Training;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed class StageOneMandatoryMenuRecoveryTests
{
    [Fact]
    public void AvailableFishingRewardRecoveryPreemptsCollectionCandidates()
    {
        var ranking = Ranking(
            Candidate(
                "collection",
                "fishing.catch_fish",
                "catch_fish_current",
                rank: 1,
                score: 1,
                available: true),
            Candidate(
                "reward-recovery",
                "recovery.stabilize_day",
                StageOneMandatoryMenuRecovery.FishingItemGrabRewardKind,
                rank: 2,
                score: 0.3,
                available: true));

        var selected = StageOneMandatoryMenuRecovery.TrySelect(ranking);

        Assert.NotNull(selected);
        Assert.Equal("reward-recovery", selected.CandidateId);
    }

    [Fact]
    public void UnavailableOrNonMenuRecoveryDoesNotPreemptCollection()
    {
        var ranking = Ranking(
            Candidate(
                "unavailable-menu",
                "recovery.stabilize_day",
                StageOneMandatoryMenuRecovery.CloseMenuKind,
                rank: 1,
                score: 1,
                available: false),
            Candidate(
                "day-recovery",
                "recovery.stabilize_day",
                "recovery_return_home",
                rank: 2,
                score: 0.5,
                available: true));

        Assert.Null(StageOneMandatoryMenuRecovery.TrySelect(ranking));
    }

    [Fact]
    public void LowestRankWinsWithStableTieBreaking()
    {
        var ranking = Ranking(
            Candidate(
                "close-menu",
                "recovery.stabilize_day",
                StageOneMandatoryMenuRecovery.CloseMenuKind,
                rank: 2,
                score: 0.9,
                available: true),
            Candidate(
                "reward-b",
                "recovery.stabilize_day",
                StageOneMandatoryMenuRecovery.FishingItemGrabRewardKind,
                rank: 1,
                score: 0.3,
                available: true),
            Candidate(
                "reward-a",
                "recovery.stabilize_day",
                StageOneMandatoryMenuRecovery.FishingItemGrabRewardKind,
                rank: 1,
                score: 0.4,
                available: true));

        var selected = StageOneMandatoryMenuRecovery.TrySelect(ranking);

        Assert.NotNull(selected);
        Assert.Equal("reward-a", selected.CandidateId);
    }

    [Fact]
    public void LiveLoopDispatchesMandatoryMenuRecoveryBeforeMembershipFiltering()
    {
        var source = Read(
            "tools",
            "StardewAI.LiveTrainingLoop",
            "Program.StageOneCollectionTeacher.cs");
        var selection = source.IndexOf(
            "StageOneMandatoryMenuRecovery.TrySelect(typedRanking)",
            StringComparison.Ordinal);
        var continuationFilter = source.IndexOf(
            "activeObjectiveContinuation is not null",
            selection,
            StringComparison.Ordinal);
        var membership = source.IndexOf(
            "CurrentStageOneCollectionTeacherPreferenceBuilder.Build(",
            continuationFilter,
            StringComparison.Ordinal);

        Assert.True(selection >= 0);
        Assert.True(continuationFilter > selection);
        Assert.True(membership > continuationFilter);
        Assert.Contains(
            "ready_mandatory_menu_recovery_control",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "teacher_preference_label_eligible = false",
            source,
            StringComparison.Ordinal);
    }

    private static AvailabilityAwarePolicyPredictionEnvelope Ranking(
        params PolicyEventCandidatePrediction[] candidates) => new()
        {
            RankedEventCandidates = candidates
        };

    private static PolicyEventCandidatePrediction Candidate(
        string id,
        string optionId,
        string kind,
        int rank,
        double score,
        bool available) => new()
        {
            CandidateId = id,
            OptionId = optionId,
            Kind = kind,
            Rank = rank,
            Score = score,
            Available = available
        };

    private static string Read(params string[] segments)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var path = Path.Combine(
                new[] { directory.FullName }.Concat(segments).ToArray());
            if (File.Exists(path))
            {
                return File.ReadAllText(path);
            }
            directory = directory.Parent;
        }
        throw new FileNotFoundException(
            "Repository file was not found: " + Path.Combine(segments));
    }
}
