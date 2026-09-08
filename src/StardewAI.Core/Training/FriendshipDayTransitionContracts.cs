using System;

namespace StardewAI.Core.Training
{
    public sealed class FriendshipDayTransitionInput
    {
        public string NpcName { get; set; } = string.Empty;
        public bool? FriendshipRowExists { get; set; }
        public bool? CharacterExists { get; set; }
        public bool? IsVillager { get; set; }
        public bool? IsChild { get; set; }
        public bool? IsDatable { get; set; }
        public bool? IsNpcMarried { get; set; }
        public bool? IsPlayerSpouse { get; set; }
        public bool? IsDating { get; set; }
        public bool? IsDivorced { get; set; }
        public bool? TalkedToToday { get; set; }
        public bool? SpeaksDwarvish { get; set; }
        public bool? PlayerCanUnderstandDwarves { get; set; }
        public bool? PlayerHasFriendshipBook { get; set; }
        public int? MaximumHearts { get; set; }
        public int? Points { get; set; }
        public int? GiftsToday { get; set; }
        public int? GiftsThisWeek { get; set; }
        public bool LastGiftDateStateComplete { get; set; }
        public int? LastGiftDateTotalDays { get; set; }
        public int? LastGiftDateTotalSundayWeeks { get; set; }
        public int? TransitionDateTotalDays { get; set; }
        public int? TransitionDateTotalSundayWeeks { get; set; }
    }

    public sealed class FriendshipPointTransition
    {
        public int Sequence { get; set; }
        public string Reason { get; set; } = string.Empty;
        public int RequestedDelta { get; set; }
        public int EffectiveDeltaBeforeClamp { get; set; }
        public int PointsBefore { get; set; }
        public int PointsAfter { get; set; }
        public int AppliedDelta { get; set; }
        public string ModifierStatus { get; set; } = string.Empty;
    }

    public sealed class FriendshipDayTransitionResult
    {
        public string Status { get; set; } = string.Empty;
        public string NpcName { get; set; } = string.Empty;
        public bool FriendshipRowExistsAfter { get; set; }
        public int? PointsBefore { get; set; }
        public int? PointsAfter { get; set; }
        public bool? TalkedToTodayAfter { get; set; }
        public int? GiftsTodayAfter { get; set; }
        public int? GiftsThisWeekAfter { get; set; }
        public FriendshipPointTransition[] PointTransitions { get; set; } = Array.Empty<FriendshipPointTransition>();
        public string[] Issues { get; set; } = Array.Empty<string>();
        public string SourceOrder { get; set; } =
            "Farmer.resetFriendshipsForNewDay_then_Farmer.updateFriendshipGifts";
    }
}
