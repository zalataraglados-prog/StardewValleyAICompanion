using StardewAI.Contracts.Training;
using StardewAI.Core.Training;

namespace StardewAI.GoalConditionedBootstrap;

public sealed class MarriageTeacherSupervisionReplayVerifier : IPolicyTeacherSupervisionReplayVerifier
{
    public const string ProviderId = "deterministic_teacher.current_marriage_preparation";
    public const string ProviderVersion = "v1";
    public string TeacherId => ProviderId;
    public string TeacherVersion => ProviderVersion;

    public PolicyDecisionTrajectoryEnvelope Rebuild(PolicyDecisionTrajectoryEnvelope claimed)
    {
        var teacher = claimed.Audit?.TeacherSupervision;
        if (teacher?.SchemaVersion != PolicyTeacherSupervisionSchemas.MarriagePreparation ||
            string.IsNullOrWhiteSpace(teacher.Sources?.MarriageNativeJointReceiptPath) ||
            teacher.Sources.MarriagePreparationIndex is not (>= 0 and < 64))
            throw new InvalidDataException("Marriage Teacher requires its typed native joint receipt and original preparation index.");
        return MarriageTeacherRowBuilder.Build(teacher.Sources.MarriageNativeJointReceiptPath,
            teacher.Sources.MarriagePreparationIndex.Value, claimed.TrajectoryId);
    }
}
