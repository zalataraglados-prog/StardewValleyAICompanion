using System;

namespace StardewAI.Contracts.Training
{
    public static class TrainingCombatIntents
    {
        public const string TargetDefeat = "target_defeat";
        public const string TransitSelfDefense =
            "transit_self_defense";
        public const string TransitRouteClearance =
            "transit_route_clearance";
    }

    public static class TrainingCombatIntentRules
    {
        public const int ImmediateThreatDistance = 4;
        public const int RouteClearanceTargetDriftDistance = 2;
        public const int SelfDefenseMovementBudget = 16;
        public const int RouteClearanceMovementHeadroom = 16;
        public const int RouteClearanceMaximumMovementBudget = 128;

        public static string Normalize(string intent)
        {
            return string.IsNullOrWhiteSpace(intent)
                ? TrainingCombatIntents.TargetDefeat
                : intent;
        }

        public static bool IsSupported(string intent)
        {
            return intent is
                TrainingCombatIntents.TargetDefeat or
                TrainingCombatIntents.TransitSelfDefense or
                TrainingCombatIntents.TransitRouteClearance;
        }

        public static int BoundMovementBudget(
            string intent,
            int estimatedMovementTiles,
            int targetDefeatMovementBudget)
        {
            return Normalize(intent) switch
            {
                TrainingCombatIntents.TransitSelfDefense =>
                    SelfDefenseMovementBudget,
                TrainingCombatIntents.TransitRouteClearance =>
                    Math.Clamp(
                        estimatedMovementTiles +
                            RouteClearanceMovementHeadroom,
                        RouteClearanceMovementHeadroom,
                        RouteClearanceMaximumMovementBudget),
                _ => Math.Clamp(targetDefeatMovementBudget, 1, 512)
            };
        }

        public static bool ShouldDisengage(
            string intent,
            int playerTargetDistance,
            int targetOriginDistance)
        {
            var normalized = Normalize(intent);
            if (normalized == TrainingCombatIntents.TargetDefeat ||
                playerTargetDistance <= ImmediateThreatDistance)
            {
                return false;
            }

            return normalized == TrainingCombatIntents.TransitSelfDefense ||
                (normalized ==
                    TrainingCombatIntents.TransitRouteClearance &&
                targetOriginDistance >
                    RouteClearanceTargetDriftDistance);
        }
    }
}
