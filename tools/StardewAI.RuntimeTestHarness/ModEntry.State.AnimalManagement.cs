using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Menus;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private sealed class ActiveAnimalManagement
    {
        public ActiveAnimalManagement(
            PendingExecution pending,
            GameLocation location,
            FarmAnimal animal,
            Point target,
            Point stand,
            List<Point> path,
            int restoreSlotIndex,
            Building? targetHome,
            AnimalHouse? targetHouse,
            int maxMovementTiles)
        {
            Pending = pending;
            Location = location;
            Animal = animal;
            Target = target;
            Stand = stand;
            Path = path;
            RestoreSlotIndex = restoreSlotIndex;
            TargetHome = targetHome;
            TargetHouse = targetHouse;
            MaxMovementTiles = maxMovementTiles;
            LastObservedTile = Game1.player.TilePoint;
            NameBefore = animal.displayName;
            AllowReproductionBefore = animal.allowReproduction.Value;
            MoneyBefore = Game1.player.Money;
            HomeBefore = animal.home;
            TargetHomeOccupantsBefore = targetHouse?.animalsThatLiveHere.Count;
        }

        public PendingExecution Pending { get; }
        public GameLocation Location { get; }
        public FarmAnimal Animal { get; }
        public Point Target { get; set; }
        public Point Stand { get; set; }
        public List<Point> Path { get; set; }
        public int RestoreSlotIndex { get; }
        public Building? TargetHome { get; }
        public AnimalHouse? TargetHouse { get; }
        public int MaxMovementTiles { get; }
        public string NameBefore { get; }
        public bool AllowReproductionBefore { get; }
        public int MoneyBefore { get; }
        public Building? HomeBefore { get; }
        public int? TargetHomeOccupantsBefore { get; }
        public string StartedAt { get; } = DateTimeOffset.UtcNow.ToString("O");
        public AnimalQueryMenu? Menu { get; set; }
        public AnimalManagementStage Stage { get; set; } = AnimalManagementStage.Navigate;
        public int ElapsedTicks { get; set; }
        public int StageEnteredTick { get; set; }
        public int PathIndex { get; set; }
        public int MovementTiles { get; set; }
        public int ReplanCount { get; set; }
        public Point LastObservedTile { get; set; }
    }

    private enum AnimalManagementStage
    {
        Navigate,
        OpenQuery,
        WaitAfterInitialPet,
        ApplyMenuOperation,
        ConfirmSale,
        WaitForPlacementMode,
        SelectTargetHome,
        WaitForReceipt
    }
}
