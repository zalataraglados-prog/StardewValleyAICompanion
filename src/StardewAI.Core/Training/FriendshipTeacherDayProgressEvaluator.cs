using System;

namespace StardewAI.Core.Training
{
    public sealed class FriendshipTeacherDayProgress
    {
        public int DayStartFriendshipPointSum { get; set; }

        public int NextDayStartFriendshipPointSum { get; set; }

        public int NetPointDelta { get; set; }

        public bool MadeNetProgress { get; set; }

        public int VerifiedGoalDirectedObjectiveCount { get; set; }

        public bool MadeGoalDirectedProgress { get; set; }

        public bool MadeProgress { get; set; }

        public int ConsecutiveNoProgressDays { get; set; }
    }

    public sealed class FriendshipTeacherDayProgressEvaluator
    {
        public FriendshipTeacherDayProgress Evaluate(
            int dayStartFriendshipPointSum,
            int nextDayStartFriendshipPointSum,
            int previousConsecutiveNoProgressDays,
            int verifiedGoalDirectedObjectiveCount)
        {
            if (previousConsecutiveNoProgressDays < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(previousConsecutiveNoProgressDays));
            }
            if (verifiedGoalDirectedObjectiveCount < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(verifiedGoalDirectedObjectiveCount));
            }

            var delta = nextDayStartFriendshipPointSum -
                dayStartFriendshipPointSum;
            var madeNetProgress = delta > 0;
            var madeGoalDirectedProgress =
                verifiedGoalDirectedObjectiveCount > 0;
            var madeProgress = madeNetProgress || madeGoalDirectedProgress;
            return new FriendshipTeacherDayProgress
            {
                DayStartFriendshipPointSum = dayStartFriendshipPointSum,
                NextDayStartFriendshipPointSum =
                    nextDayStartFriendshipPointSum,
                NetPointDelta = delta,
                MadeNetProgress = madeNetProgress,
                VerifiedGoalDirectedObjectiveCount =
                    verifiedGoalDirectedObjectiveCount,
                MadeGoalDirectedProgress = madeGoalDirectedProgress,
                MadeProgress = madeProgress,
                ConsecutiveNoProgressDays = madeProgress
                    ? 0
                    : checked(previousConsecutiveNoProgressDays + 1)
            };
        }
    }
}
