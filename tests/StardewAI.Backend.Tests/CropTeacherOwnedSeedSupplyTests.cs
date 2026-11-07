using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.Strategy;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;
using StardewAI.Testing;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherOwnedSeedSupplyTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void OriginalPlannerConsumesOwnedCountsWithoutChangingTheUnreservedPath()
    {
        using var fixture = new Fixture();
        var before = JsonSerializer.Serialize(fixture.Ledger, JsonOptions);
        var owned = fixture.Supply();
        Assert.Equal(1, owned.PlayerQuantity(new[] { fixture.Id }));
        Assert.Equal(80, owned.PurchaseBudget(new[] { fixture.Id }));
        var step = fixture.Evaluate();
        Assert.Equal(CropTeacherRunStageKinds.AcquireSeed, step.Stage);
        Assert.Equal(1, step.RemainingQuantity);
        Assert.Equal("(O)474", step.SeedQualifiedItemId);
        Assert.Equal("SeedShop", step.ShopId);
        var legacy = CropTeacherRunPlanner.Evaluate(fixture.Plan, fixture.Request, fixture.Source);
        Assert.Equal(CropTeacherRunStageKinds.RouteToFarm, legacy.Stage);
        Assert.DoesNotContain("seedTransferIntent", JsonSerializer.Serialize(legacy, JsonOptions));
        Assert.DoesNotContain("seedReservationId", JsonSerializer.Serialize(legacy, JsonOptions));
        Assert.Equal(before, JsonSerializer.Serialize(fixture.Ledger, JsonOptions));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NativeSeedGrowthIsNotOwnedUntilAnExactAdditionalClaimExists(bool claimAcquired)
    {
        using var fixture = new Fixture();
        fixture.ChangeGraph(graph => graph["inventory_nodes"]![0]!["slots"]![0]!["stack"] = 4,
            player => player["inventory"]!["value"]![0]!["stack"] = 4);
        fixture.Anchor();
        if (claimAcquired)
        {
            var result = new MaterialReservationLedgerService().Upsert(fixture.Ledger, fixture.Source,
                new MaterialReservationUpsertRequest
                {
                    StateHash = fixture.Source.StateHash, ExpectedLedgerRevision = fixture.Ledger.Revision,
                    ReservationId = fixture.Id + ":seed:acquired:1", SourceDecisionId = fixture.DecisionId,
                    GoalId = "earn_money", NodeId = "player:inventory", SlotIndex = 0, QualifiedItemId = "(O)474",
                    Quantity = 1, Purpose = "crop_seed_supply:" + fixture.Id
                }, "2026-11-07T01:00:00Z");
            Assert.True(result.Accepted, string.Join(",", result.Errors));
            fixture.Ledger = result.Ledger!;
        }
        Assert.Equal(claimAcquired ? 2 : 1, fixture.Supply().PlayerQuantity(new[] { fixture.Id }));
        Assert.Equal(claimAcquired ? CropTeacherRunStageKinds.RouteToFarm : CropTeacherRunStageKinds.AcquireSeed,
            fixture.Evaluate().Stage);
    }

    [Fact]
    public void MissingOwnedBudgetCannotSpendTheOtherRootsMoney()
    {
        using var fixture = new Fixture();
        fixture.Ledger.CurrencyReservations = fixture.Ledger.CurrencyReservations.Where(row => row.ReservationId == "other-money").ToArray();
        var step = fixture.Evaluate();
        Assert.Equal(CropTeacherRunStageKinds.ReplanRequired, step.Stage);
        Assert.Equal("crop_teacher_owned_seed_purchase_budget_insufficient", step.Reason);
        Assert.Equal(350, fixture.Ledger.CurrencyReservations[0].Amount);
    }

    [Fact]
    public void AnotherCropOfTheSameSeedAndDecisionCannotSupplyThisCommitment()
    {
        using var fixture = new Fixture();
        fixture.ChangeGraph(graph => graph["inventory_nodes"]![0]!["slots"]![0]!["stack"] = 5,
            player => player["inventory"]!["value"]![0]!["stack"] = 5);
        fixture.Anchor();
        var secondary = new CropCommitmentLedgerService().Upsert(fixture.Ledger, fixture.Source,
            new CropPlantingCommitmentUpsertRequest
            {
                StateHash = fixture.Source.StateHash, ExpectedLedgerRevision = fixture.Ledger.Revision,
                CommitmentId = "secondary-crop", SourceDecisionId = fixture.DecisionId, SeedId = "474", TileCount = 2,
                PlantingYear = 1, PlantingSeason = "spring", PlantingDayOfMonth = 7, LocationContext = "outdoor_seasonal"
            }, "2026-11-07T01:00:00Z");
        Assert.True(secondary.Accepted, string.Join(",", secondary.Errors));
        var reserved = new MaterialReservationLedgerService().Upsert(secondary.Ledger, fixture.Source,
            new MaterialReservationUpsertRequest
            {
                StateHash = fixture.Source.StateHash, ExpectedLedgerRevision = secondary.Ledger!.Revision,
                ReservationId = "secondary-crop:seed:0", SourceDecisionId = fixture.DecisionId, GoalId = "earn_money",
                NodeId = "player:inventory", SlotIndex = 0, QualifiedItemId = "(O)474", Quantity = 2,
                Purpose = "crop_seed_supply:secondary-crop"
            }, "2026-11-07T01:00:00Z");
        Assert.True(reserved.Accepted, string.Join(",", reserved.Errors));
        fixture.Ledger = reserved.Ledger!;
        var both = CropTeacherOwnedSeedSupply.Read(fixture.Source, fixture.Ledger, new[] { fixture.Id, "secondary-crop" });
        Assert.Equal(1, both.PlayerQuantity(new[] { fixture.Id }));
        Assert.Equal(2, both.PlayerQuantity(new[] { "secondary-crop" }));
        Assert.Equal(1, fixture.Evaluate().RemainingQuantity);
        Assert.Equal(CropTeacherRunStageKinds.AcquireSeed, fixture.Evaluate().Stage);
    }

    [Theory]
    [InlineData("stale")]
    [InlineData("actor")]
    [InlineData("inventory")]
    [InlineData("seed_owner")]
    [InlineData("money_owner")]
    [InlineData("layout")]
    [InlineData("seed")]
    [InlineData("quality")]
    [InlineData("quality_type")]
    [InlineData("graph_location")]
    [InlineData("calendar")]
    public void RejectsDriftBeforeChoosingAnAction(string mutation)
    {
        using var fixture = new Fixture();
        if (mutation == "stale") fixture.Ledger.SourceStateHash = new string('e', 64);
        if (mutation == "actor") fixture.Ledger.PlayerId = "999";
        if (mutation == "inventory")
        {
            fixture.ChangeGraph(_ => { }, player => player["inventory"]!["value"]![0]!["stack"] = 4);
            fixture.Anchor();
        }
        if (mutation == "seed_owner") fixture.Ledger.MaterialReservations.Single(row => row.Purpose == "crop_seed_supply:" + fixture.Id).SourceDecisionId = "not-owner";
        if (mutation == "money_owner") fixture.Ledger.CurrencyReservations.Single(row => row.ReservationId != "other-money").SourceDecisionId = "not-owner";
        if (mutation == "layout") fixture.Ledger.CropPlantingCommitments[0].TileCount++;
        if (mutation == "seed") fixture.Ledger.CropPlantingCommitments[0].SeedQualifiedItemId = "(O)472";
        if (mutation == "calendar") fixture.Ledger.CropPlantingCommitments[0].PlantingTotalDay++;
        if (mutation is "quality" or "quality_type" or "graph_location")
        {
            fixture.ChangeGraph(_ => { }, player =>
            {
                if (mutation == "quality") player["inventory"]!["value"]![0]!["quality"] = 1;
                if (mutation == "quality_type") player["inventory"]!["value"]![0]!["quality"] = "not-a-number";
                if (mutation == "graph_location") player["location_id"]!["value"] = "Farm";
            });
            fixture.Anchor();
        }
        var error = Assert.Throws<InvalidDataException>(() => fixture.Evaluate());
        Assert.Contains(mutation switch
        {
            "stale" => "requires current source",
            "actor" => "metadata or identity",
            "inventory" or "quality" => "inventory disagrees",
            "quality_type" => "inventory is invalid",
            "graph_location" => "location disagrees",
            "calendar" => "calendar_drift",
            "seed_owner" or "seed" => "claim owner or seed identity",
            "money_owner" => "budget owner",
            "layout" => "commitment_layout_or_seed_drift",
            _ => throw new InvalidOperationException()
        }, error.Message);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ChestSeedIsRoutedOrTransferredBeforeBuyingMore(bool sameLocation)
    {
        using var fixture = new Fixture();
        fixture.PutOwnedSeedInChest(sameLocation ? "FarmHouse" : "Farm");
        var supply = fixture.Supply();
        Assert.Equal(0, supply.PlayerQuantity(new[] { fixture.Id }));
        Assert.Equal(1, supply.Commitments[0].ExternalQuantity);
        var step = fixture.Evaluate();
        Assert.Equal(sameLocation ? CropTeacherRunStageKinds.TransferSeedSupply : CropTeacherRunStageKinds.RouteToSeedSupply, step.Stage);
        Assert.Equal(sameLocation ? "FarmHouse" : "Farm", step.TargetLocationId);
        Assert.NotNull(step.SeedReservationId);
        if (sameLocation)
        {
            Assert.Equal("chest:seeds", step.SeedTransferIntent!.SourceNodeId);
            Assert.Equal("player:inventory", step.SeedTransferIntent.DestinationNodeId);
            Assert.Equal(1, step.SeedTransferIntent.Quantity);
            Assert.Equal(1, step.SeedTransferIntent.ExpectedSourceStack);
        }
        else Assert.Null(step.SeedTransferIntent);
    }

    [Fact]
    public void LockedChestCannotBeReplacedByAnUnplannedPurchase()
    {
        using var fixture = new Fixture();
        fixture.PutOwnedSeedInChest("FarmHouse");
        fixture.ChangeGraph(graph => graph["access_points"]![0]!["locked_by_other_player"] = true);
        fixture.Anchor();
        var error = Assert.Throws<InvalidDataException>(() => fixture.Evaluate());
        Assert.Contains("material_transfer_chest_locked_by_other_player", error.Message);
    }

}
