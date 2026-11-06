using System.Text.Json;
using StardewAI.Contracts.Training;
using StardewAI.Core.Training;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.Testing;

namespace StardewAI.Core.Tests;

public sealed partial class PolicyTeacherReplayRegistryTests
{
    [Fact]
    public void FriendshipProviderIsExplicitAndLegacyAndSkillsRemainByteCompatible()
    {
        var production = Assert.IsType<PolicyTeacherSupervisionReplayRegistry>(KnownTeacherSupervisionReplayVerifiers.Create());
        Assert.IsType<FriendshipTeacherSupervisionReplayVerifier>(production.Resolve(FriendshipTeacherSupervisionReplayVerifier.ProviderId, "v1"));
        Assert.Null(production.Resolve(FriendshipTeacherSupervisionReplayVerifier.ProviderId, "v2"));
        var legacy = PolicySupervisionFixture.Attach(PolicySupervisionFixture.Observation(), "gift", "talk");
        Assert.DoesNotContain("friendship_native_threshold", JsonSerializer.Serialize(legacy, Json));
        Assert.DoesNotContain("friendship_native_threshold", JsonSerializer.Serialize(SkillsRow(), Json));
        var friendship = FriendshipRow();
        Assert.Null(PolicyTrajectoryDatasetBuilder.ValidateEnvelope(friendship));
        var registry = new PolicyTeacherSupervisionReplayRegistry(PolicySupervisionFixture.Verifier,
            new FrozenVerifier(SkillsRow()), new FrozenVerifier(friendship));
        Assert.Null(new PolicyPairwiseSupervisionAdmission(registry).Validate(friendship, out var pairs));
        Assert.Single(pairs);
        Assert.Equal("teacher_supervision_identity_mismatch",
            new PolicyPairwiseSupervisionAdmission(PolicySupervisionFixture.Verifier).Validate(friendship, out _));
    }

    [Theory]
    [InlineData("path")]
    [InlineData("hash")]
    [InlineData("skills_path")]
    [InlineData("skills_hash")]
    [InlineData("collection_hash")]
    [InlineData("collection_path")]
    [InlineData("wrong_schema")]
    [InlineData("nonfinite_feature")]
    [InlineData("duplicate_feature")]
    public void FriendshipShapeRejectsMissingOrBorrowedProofAndInvalidModelFeatures(string mutation)
    {
        var row = FriendshipRow();
        var teacher = row.Audit.TeacherSupervision!;
        switch (mutation)
        {
            case "path": teacher.Sources!.FriendshipNativeThresholdReceiptPath = null; break;
            case "hash": teacher.FriendshipNativeThresholdReceiptSha256 = null; break;
            case "skills_path": teacher.Sources!.SkillNativeThresholdReceiptPath = "skills.json"; break;
            case "skills_hash": teacher.SkillNativeThresholdReceiptSha256 = new string('a', 64); break;
            case "collection_hash": teacher.CommunityCenterDenominatorSha256 = new string('a', 64); break;
            case "collection_path": teacher.Sources!.AcquisitionLoweringPath = "lowering.json"; break;
            case "wrong_schema": teacher.SchemaVersion = PolicyTeacherSupervisionSchemas.Skills; break;
            case "nonfinite_feature": row.Candidates[0].SourceCandidate.PolicyFeatures = new()
                { Numeric = new[] { new NumericFeature { Name = "arrival", Value = double.NaN } } }; break;
            case "duplicate_feature": row.Candidates[0].SourceCandidate.PolicyFeatures = new()
                { Numeric = new[] { new NumericFeature { Name = "arrival" }, new NumericFeature { Name = "arrival" } } }; break;
        }
        Assert.NotNull(PolicyTrajectoryDatasetBuilder.ValidateEnvelope(row));
    }

    [Theory]
    [InlineData("policy_teacher_supervision.v2", "native.json")]
    [InlineData(PolicyTeacherSupervisionSchemas.Friendships, null)]
    [InlineData(PolicyTeacherSupervisionSchemas.Friendships, " ")]
    public void FriendshipReplayRequiresItsOwnTypedSource(string schema, string? path)
    {
        var row = FriendshipRow();
        row.Audit.TeacherSupervision!.SchemaVersion = schema;
        row.Audit.TeacherSupervision.Sources!.FriendshipNativeThresholdReceiptPath = path;
        var error = Assert.Throws<InvalidDataException>(() => new FriendshipTeacherSupervisionReplayVerifier().Rebuild(row));
        Assert.Contains("typed original native threshold receipt", error.Message);
    }

    [Theory]
    [InlineData("count")]
    [InlineData("candidate_budget")]
    [InlineData("native_hash")]
    [InlineData("uncompared")]
    public void FriendshipRegistryReplaysRatherThanTrustingSelfConsistentEdits(string mutation)
    {
        var row = FriendshipRow();
        var registry = new PolicyTeacherSupervisionReplayRegistry(new FrozenVerifier(row));
        if (mutation == "count") row.StateFeatures.Numeric[0].Value++;
        if (mutation == "candidate_budget") row.Candidates[0].SourceCandidate.PolicyFeatures = new()
            { Numeric = new[] { new NumericFeature { Name = "arrival", Value = 1 } } };
        if (mutation == "native_hash") row.Audit.TeacherSupervision!.FriendshipNativeThresholdReceiptSha256 = new string('f', 64);
        if (mutation == "uncompared") row.Candidates = row.Candidates.Where(candidate => candidate.CandidateId != "uncompared").ToArray();
        row.Audit.TeacherSupervision!.CandidateDenominatorSha256 = PolicyPairwiseSupervisionAdmission.CandidateDenominatorSha256(row);
        Assert.NotNull(new PolicyPairwiseSupervisionAdmission(registry).Validate(row, out var pairs));
        Assert.Empty(pairs);
    }

    private static PolicyDecisionTrajectoryEnvelope FriendshipRow()
    {
        var row = SkillsRow();
        var teacher = row.Audit.TeacherSupervision!;
        teacher.SchemaVersion = PolicyTeacherSupervisionSchemas.Friendships;
        teacher.TeacherId = FriendshipTeacherSupervisionReplayVerifier.ProviderId;
        teacher.Sources!.SkillNativeThresholdReceiptPath = null;
        teacher.SkillNativeThresholdReceiptSha256 = null;
        teacher.Sources.FriendshipNativeThresholdReceiptPath = "fixture-friendship-native.json";
        teacher.FriendshipNativeThresholdReceiptSha256 = new string('c', 64);
        return row;
    }
}
