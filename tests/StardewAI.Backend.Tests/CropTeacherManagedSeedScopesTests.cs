using System.Text.Json;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed class CropTeacherManagedSeedScopesTests
{
    [Fact]
    public void PurchaseSelectionReusesOriginalOwnedScopeWithoutMutationOrRequestReparse()
    {
        using var f = new CropTeacherSeedPurchaseSettlementTests.Fixture();
        var step = PurchaseStep(f);
        var before = File.ReadAllText(f.LedgerPath);
        var scopes = CropTeacherManagedSeedScopes.Open(f.RequestPath, f.SourcePath, f.LedgerPath, step, "managed.purchase");
        Assert.NotNull(scopes.Purchase);
        Assert.Null(scopes.Transfer);
        Assert.Null(scopes.Planting);
        scopes.ValidateStep(step);
        Assert.Equal("managed.purchase", scopes.Purchase.MarkerRequest.PortfolioId);
        Assert.Equal(before, File.ReadAllText(f.LedgerPath));
        Assert.False(Directory.Exists(f.RuntimePath));
    }

    [Theory]
    [InlineData("day")]
    [InlineData("seed")]
    [InlineData("shop")]
    [InlineData("price")]
    [InlineData("quantity")]
    [InlineData("oversize")]
    [InlineData("stage")]
    [InlineData("foreign_claim")]
    public void PurchaseSelectionRejectsAChangedOriginalIntentBeforeMutation(string fault)
    {
        using var f = new CropTeacherSeedPurchaseSettlementTests.Fixture();
        var step = PurchaseStep(f);
        step = fault switch
        {
            "day" => step with { TotalDay = step.TotalDay + 1 },
            "seed" => step with { SeedQualifiedItemId = "(O)472" },
            "shop" => step with { ShopId = "JojaMart" },
            "price" => step with { MaxUnitPrice = step.MaxUnitPrice + 1 },
            "quantity" => step with { RemainingQuantity = 0 },
            "oversize" => step with { RemainingQuantity = f.Open().MaximumPurchaseQuantity + 1 },
            "stage" => step with { Stage = CropTeacherRunStageKinds.NativeSleep },
            _ => step with { SeedReservationId = "other-seeds" }
        };
        Assert.Throws<InvalidDataException>(() => CropTeacherManagedSeedScopes.Open(
            f.RequestPath, f.SourcePath, f.LedgerPath, step, "managed.purchase"));
        Assert.False(Directory.Exists(f.RuntimePath));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SelectedScopeRejectsPhysicalInputOrStepDriftBeforeControl(bool physical)
    {
        using var f = new CropTeacherSeedPurchaseSettlementTests.Fixture();
        var step = PurchaseStep(f);
        var scopes = CropTeacherManagedSeedScopes.Open(f.RequestPath, f.SourcePath, f.LedgerPath, step, "managed.purchase");
        if (physical) File.AppendAllText(f.SourcePath, "\n");
        else step = step with { Reason = "reused for another decision" };
        Assert.Throws<InvalidDataException>(() => scopes.ValidateStep(step));
    }

    [Fact]
    public void TransferSelectionUsesTheOriginalChestClaimAndCompleteProjection()
    {
        using var f = new CropTeacherSeedTransferTests.Fixture();
        var original = f.Open();
        var intent = original.PreparationRequest.MaterialTransferPreparation!.Intent;
        var step = new CropTeacherRunStep(CropTeacherRunStageKinds.TransferSeedSupply, original.TotalDay, original.CommitmentId,
            "Farm", "FarmHouse", intent.QualifiedItemId, "", 0, intent.Quantity, null, "owned_supply", intent, original.ReservationId);
        var scopes = CropTeacherManagedSeedScopes.Open(f.RequestPath, f.SourcePath, f.LedgerPath, step,
            original.PreparationRequest.PortfolioId);
        Assert.NotNull(scopes.Transfer);
        Assert.Null(scopes.Purchase);
        Assert.Null(scopes.Planting);
        Assert.Equal(JsonSerializer.Serialize(original.PreparationRequest), JsonSerializer.Serialize(scopes.Transfer.PreparationRequest));
        scopes.ValidateStep(step);
        intent.Quantity++;
        Assert.Throws<InvalidDataException>(() => scopes.ValidateStep(step));
    }

    [Fact]
    public void PlantingSelectionUsesOriginalPlayerClaimAndRetainsTheMechanicalBatch()
    {
        using var f = new CropTeacherSeedPlantingTests.Fixture();
        var original = f.Open();
        var step = new CropTeacherRunStep(CropTeacherRunStageKinds.ExecuteCropBatch, original.TotalDay, f.Id,
            "Farm", "FarmHouse", "(O)745", "", 0, 8, f.Batch(), "owned_supply", SeedReservationId: f.OwnedSlotOne);
        var scopes = CropTeacherManagedSeedScopes.Open(f.RequestPath, f.SourcePath, f.LedgerPath, step, original.MarkerRequest.PortfolioId);
        Assert.NotNull(scopes.Planting);
        Assert.Null(scopes.Purchase);
        Assert.Null(scopes.Transfer);
        scopes.ValidateStep(step);
        Assert.Equal(8, step.ActionBatch!.Tiles.Length);
        step.ActionBatch.Tiles[0] = new(999, 999);
        Assert.Throws<InvalidDataException>(() => scopes.ValidateStep(step));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TransferSelectionRejectsMissingClaimOrChangedProjectionBeforeAnyRuntime(bool missingClaim)
    {
        using var f = new CropTeacherSeedTransferTests.Fixture();
        var original = f.Open();
        var intent = original.PreparationRequest.MaterialTransferPreparation!.Intent;
        var quantity = intent.Quantity;
        if (!missingClaim) intent.Quantity--;
        var step = new CropTeacherRunStep(CropTeacherRunStageKinds.TransferSeedSupply, original.TotalDay, original.CommitmentId,
            "Farm", "FarmHouse", intent.QualifiedItemId, "", 0, quantity, null, "owned_supply", intent,
            missingClaim ? null : original.ReservationId);
        Assert.Throws<InvalidDataException>(() => CropTeacherManagedSeedScopes.Open(f.RequestPath, f.SourcePath,
            f.LedgerPath, step, original.PreparationRequest.PortfolioId));
        Assert.False(Directory.Exists(f.RuntimePath));
    }

    private static CropTeacherRunStep PurchaseStep(CropTeacherSeedPurchaseSettlementTests.Fixture f)
    {
        var scope = f.Open();
        var intent = scope.AcquisitionIntent;
        return new(CropTeacherRunStageKinds.AcquireSeed, scope.TotalDay, scope.CommitmentId, "", "FarmHouse",
            intent.SeedQualifiedItemId, intent.ShopId, intent.MaxUnitPrice, 1, null, "owned_supply");
    }
}
