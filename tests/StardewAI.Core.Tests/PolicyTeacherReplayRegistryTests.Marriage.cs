using System.Text.Json;
using StardewAI.Contracts.Training;
using StardewAI.Core.Training;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Core.Tests;

public sealed partial class PolicyTeacherReplayRegistryTests
{
    [Fact]
    public void MarriageProviderIsExplicitAndDoesNotChangeOldSerializedRows()
    {
        var registry = Assert.IsType<PolicyTeacherSupervisionReplayRegistry>(KnownTeacherSupervisionReplayVerifiers.Create());
        Assert.IsType<MarriageTeacherSupervisionReplayVerifier>(registry.Resolve(MarriageTeacherSupervisionReplayVerifier.ProviderId, "v1"));
        Assert.Null(registry.Resolve(MarriageTeacherSupervisionReplayVerifier.ProviderId, "v2"));
        foreach (var old in new[] { SkillsRow(), FriendshipRow() })
            Assert.DoesNotContain("marriage_", JsonSerializer.Serialize(old, Json));
        var row = MarriageRow();
        Assert.Null(PolicyTrajectoryDatasetBuilder.ValidateEnvelope(row));
        Assert.Null(new PolicyPairwiseSupervisionAdmission(new PolicyTeacherSupervisionReplayRegistry(new FrozenVerifier(row))).Validate(row, out var pairs));
        Assert.Single(pairs);
    }

    [Theory]
    [InlineData("path")]
    [InlineData("hash")]
    [InlineData("index")]
    [InlineData("negative_index")]
    [InlineData("large_index")]
    [InlineData("skills_path")]
    [InlineData("skills_hash")]
    [InlineData("friendship_path")]
    [InlineData("friendship_hash")]
    [InlineData("collection_hash")]
    [InlineData("old_schema")]
    public void MarriageRowsCannotBorrowAnotherSourceShape(string change)
    {
        var row = MarriageRow(); var teacher = row.Audit.TeacherSupervision!;
        switch (change)
        {
            case "path": teacher.Sources!.MarriageNativeJointReceiptPath = null; break;
            case "hash": teacher.MarriageNativeJointReceiptSha256 = null; break;
            case "index": teacher.Sources!.MarriagePreparationIndex = null; break;
            case "negative_index": teacher.Sources!.MarriagePreparationIndex = -1; break;
            case "large_index": teacher.Sources!.MarriagePreparationIndex = 64; break;
            case "skills_path": teacher.Sources!.SkillNativeThresholdReceiptPath = "skills.json"; break;
            case "skills_hash": teacher.SkillNativeThresholdReceiptSha256 = new string('a', 64); break;
            case "friendship_path": teacher.Sources!.FriendshipNativeThresholdReceiptPath = "friends.json"; break;
            case "friendship_hash": teacher.FriendshipNativeThresholdReceiptSha256 = new string('a', 64); break;
            case "collection_hash": teacher.CommunityCenterDenominatorSha256 = new string('a', 64); break;
            case "old_schema": teacher.SchemaVersion = PolicyTeacherSupervisionSchemas.Friendships; break;
        }
        Assert.NotNull(PolicyTrajectoryDatasetBuilder.ValidateEnvelope(row));
    }

    [Theory]
    [InlineData("hash")]
    [InlineData("index")]
    [InlineData("candidate")]
    public void MarriageReplayDoesNotTrustSelfConsistentRowEdits(string change)
    {
        var row = MarriageRow(); var verifier = new FrozenVerifier(row);
        if (change == "hash") row.Audit.TeacherSupervision!.MarriageNativeJointReceiptSha256 = new string('f', 64);
        if (change == "index") row.Audit.TeacherSupervision!.Sources!.MarriagePreparationIndex = 1;
        if (change == "candidate") row.Candidates = row.Candidates.Where(value => value.CandidateId != "uncompared").ToArray();
        row.Audit.TeacherSupervision!.CandidateDenominatorSha256 = PolicyPairwiseSupervisionAdmission.CandidateDenominatorSha256(row);
        Assert.NotNull(new PolicyPairwiseSupervisionAdmission(new PolicyTeacherSupervisionReplayRegistry(verifier)).Validate(row, out var pairs));
        Assert.Empty(pairs);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(-1)]
    [InlineData(64)]
    public void MarriageProviderCannotOpenAnUndeclaredPreparation(int? index)
    {
        var row = MarriageRow(); row.Audit.TeacherSupervision!.Sources!.MarriagePreparationIndex = index;
        Assert.Throws<InvalidDataException>(() => new MarriageTeacherSupervisionReplayVerifier().Rebuild(row));
    }

    private static PolicyDecisionTrajectoryEnvelope MarriageRow()
    {
        var row = FriendshipRow(); var teacher = row.Audit.TeacherSupervision!;
        teacher.SchemaVersion = PolicyTeacherSupervisionSchemas.MarriagePreparation;
        teacher.TeacherId = MarriageTeacherSupervisionReplayVerifier.ProviderId;
        teacher.FriendshipNativeThresholdReceiptSha256 = null; teacher.Sources!.FriendshipNativeThresholdReceiptPath = null;
        teacher.MarriageNativeJointReceiptSha256 = new string('e', 64);
        teacher.Sources.MarriageNativeJointReceiptPath = "fixture-marriage-joint.json"; teacher.Sources.MarriagePreparationIndex = 0;
        return row;
    }
}
