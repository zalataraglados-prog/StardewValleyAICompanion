using StardewAI.Contracts.Training;
using StardewAI.Core.Training;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Core.Tests;

public sealed class SkillTeacherReplayVerifierTests
{
    [Fact]
    public void ProductionRegistryHasOnlyExactExplicitTeacherIdentities()
    {
        var registry = Assert.IsType<PolicyTeacherSupervisionReplayRegistry>(KnownTeacherSupervisionReplayVerifiers.Create());
        Assert.IsType<CollectionTeacherSupervisionReplayVerifier>(registry.Resolve(CollectionTeacherSupervisionReplayVerifier.ProviderId, "v1"));
        Assert.IsType<SkillTeacherSupervisionReplayVerifier>(registry.Resolve(SkillTeacherSupervisionReplayVerifier.ProviderId, "v1"));
        Assert.Null(registry.Resolve(SkillTeacherSupervisionReplayVerifier.ProviderId, "v2"));
        Assert.Null(registry.Resolve("unknown", "v1"));
    }

    [Theory]
    [InlineData("policy_teacher_supervision.v2", "native.json")]
    [InlineData(PolicyTeacherSupervisionSchemas.Skills, null)]
    [InlineData(PolicyTeacherSupervisionSchemas.Skills, " ")]
    public void SkillsReplayCannotReplaceNativeProofWithLegacyOrMissingSources(string schema, string? nativePath)
    {
        var row = new PolicyDecisionTrajectoryEnvelope();
        row.Audit.TeacherSupervision = new PolicyTrajectoryTeacherSupervision
        {
            SchemaVersion = schema,
            Sources = new PolicyTeacherEvidenceSources { SkillNativeThresholdReceiptPath = nativePath }
        };
        var error = Assert.Throws<InvalidDataException>(() => new SkillTeacherSupervisionReplayVerifier().Rebuild(row));
        Assert.Contains("typed original native threshold receipt", error.Message);
    }
}
