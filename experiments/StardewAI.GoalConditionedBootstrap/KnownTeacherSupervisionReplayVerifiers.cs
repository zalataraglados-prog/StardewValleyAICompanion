using StardewAI.Core.Training;

namespace StardewAI.GoalConditionedBootstrap;

public static class KnownTeacherSupervisionReplayVerifiers
{
    public static IPolicyTeacherSupervisionReplayVerifier Create() => new PolicyTeacherSupervisionReplayRegistry(
        new CollectionTeacherSupervisionReplayVerifier(), new SkillTeacherSupervisionReplayVerifier(),
        new FriendshipTeacherSupervisionReplayVerifier(), new MarriageTeacherSupervisionReplayVerifier());
}
