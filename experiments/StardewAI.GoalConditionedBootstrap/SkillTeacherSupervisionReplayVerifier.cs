using StardewAI.Contracts.Training;
using StardewAI.Core.Training;

namespace StardewAI.GoalConditionedBootstrap;

public sealed class SkillTeacherSupervisionReplayVerifier : IPolicyTeacherSupervisionReplayVerifier
{
    public const string ProviderId = "deterministic_teacher.current_skill_xp";
    public const string ProviderVersion = "v1";
    public string TeacherId => ProviderId;
    public string TeacherVersion => ProviderVersion;

    public PolicyDecisionTrajectoryEnvelope Rebuild(PolicyDecisionTrajectoryEnvelope claimed)
    {
        var teacher = claimed.Audit?.TeacherSupervision;
        var sources = teacher?.Sources;
        if (teacher?.SchemaVersion != PolicyTeacherSupervisionSchemas.Skills ||
            string.IsNullOrWhiteSpace(sources?.SkillNativeThresholdReceiptPath))
            throw new InvalidDataException("Skills Teacher requires its typed original native threshold receipt.");
        return SkillTeacherRowBuilder.Build(sources.SkillNativeThresholdReceiptPath,
            sources.ExecutionReceiptPath, claimed.TrajectoryId);
    }
}
