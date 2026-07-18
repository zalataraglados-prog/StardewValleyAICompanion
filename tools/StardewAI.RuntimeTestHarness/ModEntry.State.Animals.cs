using System.Text.Json.Serialization;
using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Tools;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private sealed class ActiveAnimalProductHarvest
    {
        public ActiveAnimalProductHarvest(
            PendingExecution pending,
            GameLocation location,
            FarmAnimal animal,
            Tool tool,
            Point target,
            Point stand,
            List<Point> path,
            ClearanceOutputItemKey outputKey,
            int outputCountBefore,
            int expectedQuantity,
            int expectedQuality,
            float staminaBefore,
            int farmingExperienceBefore,
            int friendshipBefore,
            ExpectedAnimalStatIncrement[] statIncrements,
            int maxMovementTiles)
        {
            Pending = pending;
            Location = location;
            Animal = animal;
            Tool = tool;
            Target = target;
            Stand = stand;
            Path = path;
            OutputKey = outputKey;
            OutputCountBefore = outputCountBefore;
            ExpectedQuantity = expectedQuantity;
            ExpectedQuality = expectedQuality;
            StaminaBefore = staminaBefore;
            FarmingExperienceBefore = farmingExperienceBefore;
            FriendshipBefore = friendshipBefore;
            StatIncrements = statIncrements;
            MaxMovementTiles = maxMovementTiles;
            LastObservedTile = Game1.player.TilePoint;
        }

        public PendingExecution Pending { get; }
        public GameLocation Location { get; }
        public FarmAnimal Animal { get; }
        public Tool Tool { get; }
        public Point Target { get; set; }
        public Point Stand { get; set; }
        public List<Point> Path { get; set; }
        public ClearanceOutputItemKey OutputKey { get; }
        public int OutputCountBefore { get; }
        public int ExpectedQuantity { get; }
        public int ExpectedQuality { get; }
        public float StaminaBefore { get; }
        public int FarmingExperienceBefore { get; }
        public int FriendshipBefore { get; }
        public ExpectedAnimalStatIncrement[] StatIncrements { get; }
        public int MaxMovementTiles { get; }
        public string StartedAt { get; } = DateTimeOffset.UtcNow.ToString("O");
        public int ElapsedTicks { get; set; }
        public int PathIndex { get; set; }
        public int MovementTiles { get; set; }
        public int ReplanCount { get; set; }
        public Point LastObservedTile { get; set; }
        public bool BeginIssued { get; set; }
        public bool ReleaseIssued { get; set; }
    }

    private sealed class ExpectedAnimalStatIncrement
    {
        [JsonPropertyName("stat_name")]
        public string StatName { get; set; } = string.Empty;

        [JsonPropertyName("amount")]
        public uint Amount { get; set; }

        [JsonPropertyName("before")]
        public uint Before { get; set; }

        [JsonPropertyName("after")]
        public uint After { get; set; }
    }
}
