using System;

namespace StardewAI.RuntimePrimitives
{
    public sealed class MovementLease
    {
        public string? Owner { get; private set; }
        public int? Direction { get; private set; }
        public long LastTransitionTick { get; private set; }
        public string LastTransitionReason { get; private set; } = "not_started";

        public bool Acquire(string owner, int direction, long tick, out string reason)
        {
            if (string.IsNullOrWhiteSpace(owner))
            {
                reason = "movement_lease_owner_required";
                return false;
            }

            if (direction < 0 || direction > 3)
            {
                reason = "movement_lease_direction_out_of_range";
                return false;
            }

            if (Owner != null && !string.Equals(Owner, owner, StringComparison.Ordinal))
            {
                reason = "movement_lease_owned_by:" + Owner;
                return false;
            }

            Owner = owner;
            if (Direction != direction)
            {
                Direction = direction;
                LastTransitionTick = tick;
                LastTransitionReason = "direction_acquired_or_switched";
            }

            reason = string.Empty;
            return true;
        }

        public bool Release(string owner, string reason, long tick)
        {
            if (Owner is null)
            {
                return true;
            }

            if (!string.Equals(Owner, owner, StringComparison.Ordinal))
            {
                return false;
            }

            ForceRelease(reason, tick);
            return true;
        }

        public void ForceRelease(string reason, long tick)
        {
            Owner = null;
            Direction = null;
            LastTransitionTick = tick;
            LastTransitionReason = string.IsNullOrWhiteSpace(reason)
                ? "unspecified_release"
                : reason;
        }
    }
}
