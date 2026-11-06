using StardewAI.Contracts.Training;
using StardewAI.Core.Training;

namespace StardewAI.GoalConditionedBootstrap;

public sealed class FriendshipTeacherSupervisionReplayVerifier : IPolicyTeacherSupervisionReplayVerifier
{
    public const string ProviderId = "deterministic_teacher.current_friendships";
    public const string ProviderVersion = "v1";
    public string TeacherId => ProviderId;
    public string TeacherVersion => ProviderVersion;

    public PolicyDecisionTrajectoryEnvelope Rebuild(PolicyDecisionTrajectoryEnvelope claimed)
    {
        var teacher = claimed.Audit?.TeacherSupervision;
        if (teacher?.SchemaVersion != PolicyTeacherSupervisionSchemas.Friendships ||
            string.IsNullOrWhiteSpace(teacher.Sources?.FriendshipNativeThresholdReceiptPath))
            throw new InvalidDataException("Friendship Teacher requires its typed original native threshold receipt.");
        return FriendshipTeacherRowBuilder.Build(teacher.Sources.FriendshipNativeThresholdReceiptPath, claimed.TrajectoryId);
    }
}
