using System.Reflection;
using System.Text.Json;
using StardewAI.Contracts.Capabilities;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.Training;
using StardewAI.Core.Training;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Core.Tests;

public sealed class MuseumPolicyRowAdmissionTests
{
    [Fact]
    public void MuseumStillRequiresConfirmationAndCannotEnterTheOriginalPolicyBuilder()
    {
        var donor = OptionCapabilityRegistrySource.GetRequired("museum.donate_items");
        Assert.True(donor.PlayerConfirmationRequired);
        Assert.Equal(OptionTrainingEligibility.EvaluationOnly, donor.TrainingEligibility);
        Assert.Empty(new PolicyTrainingAdmissionFilter().FilterOptionIds(new[] { "museum.donate_items", "executor.wait_ticks" }));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void ComparisonReferencesNeverBecomeOriginalPolicyAlternatives(bool selectedAllowed, bool referenceAllowed)
    {
        var donor = new MuseumPolicyCandidateAdmission(new PolicyEventCandidatePrediction
            { CandidateId = "donor", OptionId = "museum.donate_items", Available = true }, true, true, selectedAllowed, Array.Empty<string>());
        var wait = new MuseumPolicyCandidateAdmission(new PolicyEventCandidatePrediction
            { CandidateId = "reference", OptionId = "executor.wait_ticks", Available = true }, false, true, referenceAllowed, Array.Empty<string>());
        var pairs = new[] { new CurrentCollectionTeacherPairwisePreference("donor", "reference", "achievement_5", "preferred_over_current_eligible_counterfactual") };
        var method = typeof(MuseumPolicyRowAdmissionBuilder).GetMethod("AdmissionReasons", BindingFlags.Static | BindingFlags.NonPublic)!;
        var reasons = Assert.IsType<string[]>(method.Invoke(null, new object[] { new[] { donor, wait }, "donor", pairs }));
        Assert.Contains("no_strict_pair_between_original_policy_admitted_candidates", reasons);
        Assert.Equal(!selectedAllowed, reasons.Contains(PolicyTrainingAdmissionFilter.OptionNotAdmittedReason + ":museum.donate_items"));
    }

    [Fact]
    public void AdmissionReportNeverClaimsItBuiltATrajectoryOrAuthorizedFormalTraining()
    {
        var report = new MuseumPolicyRowAdmission("join.json", new string('a', 64), new string('b', 64), "run", "hash", "native", "train",
            new PolicyTrajectoryContext(), new FeatureVector(), Array.Empty<MuseumPolicyCandidateAdmission>(), "donor",
            Array.Empty<CurrentCollectionTeacherPairwisePreference>(), new[] { "blocked" });
        var json = JsonSerializer.SerializeToElement(report);
        Assert.False(json.GetProperty("policy_row_built").GetBoolean());
        Assert.False(json.GetProperty("formal_training_authorized").GetBoolean());
        Assert.Equal("verified_evidence_only_policy_option_not_admitted", report.Status);
    }
}
