using System;

namespace StardewAI.Core.Training;

public sealed class CurrentFriendshipDeficit
{
    public string SchemaVersion { get; set; } = "current_friendship_deficit.v1";

    public string Status { get; set; } = "blocked";

    public string SourceStateHash { get; set; } = string.Empty;

    public long GameTick { get; set; }

    public int? EligibleVillagerCount { get; set; }

    public int? QualifyingVillagerCount { get; set; }

    public int? VillagersMissingForFive { get; set; }

    public int? VillagersMissingForTen { get; set; }

    public CurrentFriendshipDeficitRow[] Villagers { get; set; } =
        Array.Empty<CurrentFriendshipDeficitRow>();

    public string[] DuplicateNpcNames { get; set; } = Array.Empty<string>();

    public string[] BlockingReasons { get; set; } = Array.Empty<string>();

    public bool TrainingLabelEligible => false;

    public bool NativeOutcomeVerified => false;
}

public sealed class CurrentFriendshipDeficitRow
{
    public int NativeIterationIndex { get; set; }

    public string NpcName { get; set; } = string.Empty;

    public bool UniqueNpcIdentity { get; set; }

    public bool FriendshipRowExists { get; set; }

    public int? FriendshipPoints { get; set; }

    public bool Qualifies { get; set; }

    public long? PointsMissingToScoringRange { get; set; }
}
