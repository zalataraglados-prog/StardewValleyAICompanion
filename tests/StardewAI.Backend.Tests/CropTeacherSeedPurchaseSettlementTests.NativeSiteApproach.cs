using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Fact]
    public void NativeSiteApproachRequiresExplicitFactoryAndKeepsOriginalPreparation()
    {
        var f = SiteObservationFixture(); using var host = f.Runtime;
        var files = CompileSitePreparation(f);
        var legacy = OpenSitePreparation(f, files);
        var native = OpenNativeSitePreparation(f, files);
        Assert.False(legacy.UsesNativeActionEffects);
        Assert.True(native.UsesNativeActionEffects);
        Assert.Equal(legacy.CandidateId, native.CandidateId);
        Assert.Equal(legacy.SiteApproachTarget, native.SiteApproachTarget);
        Assert.Equal(legacy.InputHashes, native.InputHashes);
        var transition = SitePreparationTransition(f, files, legacy);
        Assert.False(legacy.VerifyTransition(transition).UsesNativeActionEffects);
        Assert.Throws<InvalidDataException>(() => native.VerifyTransition(transition));
        Assert.Equal(0, f.Runtime.Handler.Commits);
    }

    private static CropTeacherSupportingRoute.Preparation OpenNativeSitePreparation(OwnedRouteFixture f, SitePreparationFiles files) =>
        CropTeacherSupportingRoute.Preparation.OpenNativeSiteApproach(f.Inputs.Plan.RequestPath, f.Source.SourcePath,
            f.Source.LedgerPath, f.Inputs.Plan.CoveredCommitmentIds, Id, "site.preparation", files.Observation, files.Model, files.QueuePath);
}
