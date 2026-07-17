using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Objects;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private sealed class ActiveCrabPotCollect
    {
        public ActiveCrabPotCollect(
            PendingExecution pending,
            GameLocation location,
            CrabPot pot,
            Point target,
            Point stand,
            List<Point> path,
            ClearanceOutputItemKey outputKey,
            int outputCountBefore,
            int expectedQuantity,
            int expectedFishingExperience,
            int expectedFishCaughtCountBefore,
            int expectedFishCaughtCountAfter,
            int expectedFishCaughtMaxSizeBefore,
            int expectedCatchSizeMin,
            int expectedCatchSizeMax,
            bool caughtFishCallExpected,
            int maxMovementTiles)
        {
            Pending = pending;
            Location = location;
            Pot = pot;
            Target = target;
            Stand = stand;
            Path = path;
            OutputKey = outputKey;
            OutputCountBefore = outputCountBefore;
            ExpectedQuantity = expectedQuantity;
            ExpectedFishingExperience = expectedFishingExperience;
            ExpectedFishCaughtCountBefore = expectedFishCaughtCountBefore;
            ExpectedFishCaughtCountAfter = expectedFishCaughtCountAfter;
            ExpectedFishCaughtMaxSizeBefore = expectedFishCaughtMaxSizeBefore;
            ExpectedCatchSizeMin = expectedCatchSizeMin;
            ExpectedCatchSizeMax = expectedCatchSizeMax;
            CaughtFishCallExpected = caughtFishCallExpected;
            MaxMovementTiles = maxMovementTiles;
            FishingExperienceBefore = Game1.player.experiencePoints[Farmer.fishingSkill];
            InventoryBefore = InventoryStackSignature();
            LastPosition = Game1.player.Position;
            LastObservedTile = Game1.player.TilePoint;
            RequestedEffect = "current_location.objects[" + target.X + "," + target.Y + "].crab_pot_ready_for_harvest=false;qualified_item_id=" + outputKey.QualifiedItemId + ";quantity=" + expectedQuantity;
        }

        public PendingExecution Pending { get; }
        public GameLocation Location { get; }
        public CrabPot Pot { get; }
        public Point Target { get; }
        public Point Stand { get; }
        public List<Point> Path { get; }
        public ClearanceOutputItemKey OutputKey { get; }
        public int OutputCountBefore { get; }
        public int ExpectedQuantity { get; }
        public int ExpectedFishingExperience { get; }
        public int FishingExperienceBefore { get; }
        public int ExpectedFishCaughtCountBefore { get; }
        public int ExpectedFishCaughtCountAfter { get; }
        public int ExpectedFishCaughtMaxSizeBefore { get; }
        public int ExpectedCatchSizeMin { get; }
        public int ExpectedCatchSizeMax { get; }
        public bool CaughtFishCallExpected { get; }
        public int MaxMovementTiles { get; }
        public string InventoryBefore { get; }
        public string RequestedEffect { get; }
        public string StartedAt { get; } = DateTimeOffset.UtcNow.ToString("O");
        public int ElapsedTicks { get; set; }
        public int PathIndex { get; set; }
        public int StuckTicks { get; set; }
        public int MovementTiles { get; set; }
        public Vector2 LastPosition { get; set; }
        public Point LastObservedTile { get; set; }
    }
}

internal static class CrabPotCaughtFishPatch
{
    public static bool Active { get; set; }
    public static string ItemId { get; private set; } = string.Empty;
    public static int Size { get; private set; }
    public static int NumberCaught { get; private set; }
    public static bool Called { get; private set; }

    public static void Reset()
    {
        Active = false;
        ItemId = string.Empty;
        Size = 0;
        NumberCaught = 0;
        Called = false;
    }

    public static void Prefix(string itemId, int size, bool from_fish_pond, int numberCaught)
    {
        if (!Active || from_fish_pond)
        {
            return;
        }
        ItemId = ItemRegistry.QualifyItemId(itemId) ?? itemId;
        Size = size;
        NumberCaught = numberCaught;
        Called = true;
    }
}
