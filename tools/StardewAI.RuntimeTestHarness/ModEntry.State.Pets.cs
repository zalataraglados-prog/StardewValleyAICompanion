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

    private sealed class PetBowlReceipt
    {
        [System.Text.Json.Serialization.JsonPropertyName("receipt_id")]
        public string ReceiptId { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("status")]
        public string Status { get; set; } = "pending";

        [System.Text.Json.Serialization.JsonPropertyName("run_id")]
        public string RunId { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("queue_id")]
        public string QueueId { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("queue_item_id")]
        public string QueueItemId { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("request_nonce")]
        public string RequestNonce { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("feedback_appended")]
        public bool FeedbackAppended { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("pet_id")]
        public string PetId { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("location_id")]
        public string LocationId { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("building_tile_x")]
        public int BuildingTileX { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("building_tile_y")]
        public int BuildingTileY { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("source_total_days")]
        public int SourceTotalDays { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("friendship_before")]
        public int FriendshipBefore { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("expected_friendship_after")]
        public int ExpectedFriendshipAfter { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("pet_love_mail_before")]
        public bool PetLoveMailBefore { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("expected_pet_love_mail_after")]
        public bool ExpectedPetLoveMailAfter { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("marnie_adoption_mail_before_or_pending")]
        public bool MarnieAdoptionMailBeforeOrPending { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("expected_marnie_adoption_mail_after_or_pending")]
        public bool ExpectedMarnieAdoptionMailAfterOrPending { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("created_at")]
        public string CreatedAt { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("expires_at")]
        public string ExpiresAt { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("settled_at")]
        public string? SettledAt { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("settlement_reason")]
        public string? SettlementReason { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("settled_total_days")]
        public int? SettledTotalDays { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("settled_friendship")]
        public int? SettledFriendship { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("settled_bowl_watered")]
        public bool? SettledBowlWatered { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("settled_pet_love_mail")]
        public bool? SettledPetLoveMail { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("settled_marnie_adoption_mail_or_pending")]
        public bool? SettledMarnieAdoptionMailOrPending { get; set; }
    }
}
