using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Characters;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private sealed class ActivePetInteraction
    {
        public ActivePetInteraction(
            PendingExecution pending,
            GameLocation location,
            Pet pet,
            Point target,
            Point stand,
            List<Point> path,
            int maxMovementTiles,
            int safeSlotIndex,
            int friendshipBefore,
            bool lastPetDayBeforeMissing,
            int? lastPetDayBefore,
            int timesPetBefore,
            bool grantedFriendshipBefore,
            bool petLoveMailBefore,
            bool marniePetAdoptionMailBeforeOrPending,
            int giftDebrisCountBefore)
        {
            Pending = pending;
            Location = location;
            Pet = pet;
            Target = target;
            Stand = stand;
            Path = path;
            MaxMovementTiles = maxMovementTiles;
            SafeSlotIndex = safeSlotIndex;
            FriendshipBefore = friendshipBefore;
            LastPetDayBeforeMissing = lastPetDayBeforeMissing;
            LastPetDayBefore = lastPetDayBefore;
            TimesPetBefore = timesPetBefore;
            GrantedFriendshipBefore = grantedFriendshipBefore;
            PetLoveMailBefore = petLoveMailBefore;
            MarniePetAdoptionMailBeforeOrPending = marniePetAdoptionMailBeforeOrPending;
            GiftDebrisCountBefore = giftDebrisCountBefore;
            LastObservedTile = Game1.player.TilePoint;
        }

        public PendingExecution Pending { get; }
        public GameLocation Location { get; }
        public Pet Pet { get; }
        public Point Target { get; set; }
        public Point Stand { get; set; }
        public List<Point> Path { get; set; }
        public int MaxMovementTiles { get; }
        public int SafeSlotIndex { get; }
        public int FriendshipBefore { get; }
        public bool LastPetDayBeforeMissing { get; }
        public int? LastPetDayBefore { get; }
        public int TimesPetBefore { get; }
        public bool GrantedFriendshipBefore { get; }
        public bool PetLoveMailBefore { get; }
        public bool MarniePetAdoptionMailBeforeOrPending { get; }
        public int GiftDebrisCountBefore { get; }
        public string StartedAt { get; } = DateTimeOffset.UtcNow.ToString("O");
        public int ElapsedTicks { get; set; }
        public int PathIndex { get; set; }
        public int MovementTiles { get; set; }
        public int ReplanCount { get; set; }
        public int StuckTicks { get; set; }
        public Point LastObservedTile { get; set; }
        public bool InteractionIssued { get; set; }
        public int SettleTicks { get; set; }
    }
}
