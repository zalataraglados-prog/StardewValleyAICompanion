using StardewValley;
using StardewValley.Objects;
using StardewObject = StardewValley.Object;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class CurrentLocationReadAdapter
{
    private static CrabPotHarvestProjection ReadCrabPotHarvest(
        Microsoft.Xna.Framework.Vector2 tile,
        StardewObject item,
        Farmer player)
    {
        if (item is not CrabPot pot)
        {
            return CrabPotHarvestProjection.NotApplicable();
        }
        if (item.GetType() != typeof(CrabPot))
        {
            return CrabPotHarvestProjection.Blocked("unsupported_crab_pot_runtime_type", pot);
        }

        var output = pot.heldObject.Value;
        if (pot.tileIndexToShow != 714 || !pot.readyForHarvest.Value)
        {
            return CrabPotHarvestProjection.Blocked("crab_pot_not_ready", pot, output);
        }
        if (output is null)
        {
            return CrabPotHarvestProjection.Blocked("crab_pot_output_unavailable", pot);
        }

        var baseStack = Math.Max(1, output.Stack);
        var acceptsBase = player.couldInventoryAcceptThisItem(output.QualifiedItemId, baseStack, output.Quality);
        var bookRoll = Utility.CreateDaySaveRandom(
                Game1.uniqueIDForThisGame,
                Game1.stats.DaysPlayed * 77,
                tile.X * 777f + tile.Y)
            .NextDouble() < 0.25;
        var bookOwned = Game1.player.stats.Get("Book_Crabbing") != 0;
        var acceptsDouble = player.couldInventoryAcceptThisItem(output.QualifiedItemId, baseStack * 2, output.Quality);
        var doubleApplied = bookRoll && bookOwned && acceptsDouble;
        var collectStack = doubleApplied ? baseStack * 2 : baseStack;
        var acceptsCollect = player.couldInventoryAcceptThisItem(output.QualifiedItemId, collectStack, output.Quality);
        var outputState = ProjectInventoryOutput(output);
        var baitState = pot.bait.Value is null ? null : ClearanceOutputItemProjection.From(pot.bait.Value);

        var hasFishData = DataLoader.Fish(Game1.content).TryGetValue(output.ItemId, out var fishData);
        var catchSizeMin = 0;
        var catchSizeMax = 0;
        if (hasFishData)
        {
            var fields = fishData!.Split('/');
            catchSizeMin = fields.Length <= 5 ? 1 : ParseIntOrDefault(fields[5], 1);
            catchSizeMax = fields.Length > 6 ? ParseIntOrDefault(fields[6], 10) : 10;
        }

        var metadata = ItemRegistry.GetMetadata(output.QualifiedItemId);
        var parsedData = metadata.GetParsedData();
        var collectionEligible = hasFishData &&
            metadata.Exists() &&
            !ItemContextTagManager.HasBaseTag(metadata.QualifiedItemId, "trash_item") &&
            metadata.QualifiedItemId != "(O)167" &&
            (parsedData?.ObjectType == "Fish" || metadata.QualifiedItemId == "(O)372");
        var caughtBefore = player.fishCaught.TryGetValue(metadata.QualifiedItemId, out var caught)
            ? caught
            : null;
        var caughtCountBefore = caughtBefore?[0] ?? 0;
        var caughtMaxBefore = caughtBefore?[1] ?? 0;

        var status = acceptsBase && acceptsCollect ? "ready" : "crab_pot_inventory_cannot_accept_output";
        var outputItemsJson = System.Text.Json.JsonSerializer.Serialize(new[]
        {
            outputState with { Quantity = collectStack }
        });
        return new CrabPotHarvestProjection
        {
            Status = status,
            TileIndex = pot.tileIndexToShow,
            ReadyForHarvest = pot.readyForHarvest.Value,
            OwnerId = pot.owner.Value,
            BaitQualifiedItemId = pot.bait.Value?.QualifiedItemId ?? string.Empty,
            BaitUnitStateSha256 = baitState?.UnitStateSha256 ?? string.Empty,
            OutputRuntimeType = outputState.RuntimeType,
            OutputQualifiedItemId = outputState.QualifiedItemId,
            OutputQuality = outputState.Quality,
            OutputUnitStateSha256 = outputState.UnitStateSha256,
            OutputItemsJson = outputItemsJson,
            OutputStackBefore = baseStack,
            OutputStackOnCollect = collectStack,
            BookDoubleRollSucceeded = bookRoll,
            BookCrabbingOwned = bookOwned,
            BookDoubleApplied = doubleApplied,
            InventoryAcceptsBaseStack = acceptsBase,
            InventoryAcceptsCollectStack = acceptsCollect,
            FishingExperience = 5,
            ExperienceStatus = "exact",
            FishCollectionEligible = collectionEligible,
            FishCaughtCountBefore = caughtCountBefore,
            FishCaughtCountAfter = collectionEligible ? caughtCountBefore + collectStack : caughtCountBefore,
            FishCaughtMaxSizeBefore = caughtMaxBefore,
            CatchSizeMin = hasFishData ? catchSizeMin : 0,
            CatchSizeMax = hasFishData ? catchSizeMax : 0,
            CatchSizeProjectionStatus = hasFishData ? "runtime_rng_observed" : "not_applicable"
        };
    }

    private static int ParseIntOrDefault(string value, int fallback)
    {
        return int.TryParse(value, out var parsed) ? parsed : fallback;
    }

    private static ClearanceOutputItemProjection ProjectInventoryOutput(Item output)
    {
        var inventoryUnit = output.getOne();
        inventoryUnit.Stack = 1;
        inventoryUnit.HasBeenInInventory = true;
        return ClearanceOutputItemProjection.From(inventoryUnit);
    }
}

internal sealed record CrabPotHarvestProjection
{
    public string Status { get; init; } = "not_applicable";
    public int TileIndex { get; init; }
    public bool ReadyForHarvest { get; init; }
    public long OwnerId { get; init; }
    public string BaitQualifiedItemId { get; init; } = string.Empty;
    public string BaitUnitStateSha256 { get; init; } = string.Empty;
    public string OutputRuntimeType { get; init; } = string.Empty;
    public string OutputQualifiedItemId { get; init; } = string.Empty;
    public int OutputQuality { get; init; }
    public string OutputUnitStateSha256 { get; init; } = string.Empty;
    public string OutputItemsJson { get; init; } = string.Empty;
    public int OutputStackBefore { get; init; }
    public int OutputStackOnCollect { get; init; }
    public bool BookDoubleRollSucceeded { get; init; }
    public bool BookCrabbingOwned { get; init; }
    public bool BookDoubleApplied { get; init; }
    public bool InventoryAcceptsBaseStack { get; init; }
    public bool InventoryAcceptsCollectStack { get; init; }
    public int FishingExperience { get; init; }
    public string ExperienceStatus { get; init; } = "not_applicable";
    public bool FishCollectionEligible { get; init; }
    public int FishCaughtCountBefore { get; init; }
    public int FishCaughtCountAfter { get; init; }
    public int FishCaughtMaxSizeBefore { get; init; }
    public int CatchSizeMin { get; init; }
    public int CatchSizeMax { get; init; }
    public string CatchSizeProjectionStatus { get; init; } = "not_applicable";

    public static CrabPotHarvestProjection NotApplicable() => new();

    public static CrabPotHarvestProjection Blocked(string status, CrabPot pot, StardewObject? output = null)
    {
        ClearanceOutputItemProjection? outputState = null;
        if (output is not null)
        {
            var inventoryUnit = output.getOne();
            inventoryUnit.Stack = 1;
            inventoryUnit.HasBeenInInventory = true;
            outputState = ClearanceOutputItemProjection.From(inventoryUnit);
        }
        var baitState = pot.bait.Value is null ? null : ClearanceOutputItemProjection.From(pot.bait.Value);
        return new CrabPotHarvestProjection
        {
            Status = status,
            TileIndex = pot.tileIndexToShow,
            ReadyForHarvest = pot.readyForHarvest.Value,
            OwnerId = pot.owner.Value,
            BaitQualifiedItemId = pot.bait.Value?.QualifiedItemId ?? string.Empty,
            BaitUnitStateSha256 = baitState?.UnitStateSha256 ?? string.Empty,
            OutputRuntimeType = outputState?.RuntimeType ?? string.Empty,
            OutputQualifiedItemId = outputState?.QualifiedItemId ?? string.Empty,
            OutputQuality = outputState?.Quality ?? 0,
            OutputUnitStateSha256 = outputState?.UnitStateSha256 ?? string.Empty,
            OutputStackBefore = output?.Stack ?? 0
        };
    }
}
