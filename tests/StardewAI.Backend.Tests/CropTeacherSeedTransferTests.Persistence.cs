using System.Text.Json;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Execution;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedTransferTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OriginalDurablePreparationAndReplyLookupSurviveRepositoryRestart(bool multipleDestinations)
    {
        using var fixture = new Fixture(multipleDestinations);
        var repository = new FileStrategyCommitmentRepository(fixture.RepositoryPath);
        Assert.True(repository.Upsert(fixture.Before, fixture.CropRequest).Accepted);
        Assert.True(repository.CommitReservationPortfolio(fixture.Before, fixture.ResourceRequest).Accepted);
        fixture.AdoptLedger(repository.Get(fixture.Before));
        var scope = fixture.Open();
        var request = scope.PreparationRequest;
        var result = repository.CommitReservationPortfolio(fixture.Before, request);
        var marker = scope.VerifyPreparationReply(result);
        var bytes = File.ReadAllBytes(Assert.Single(Directory.GetFiles(fixture.RepositoryPath)));
        var restarted = new FileStrategyCommitmentRepository(fixture.RepositoryPath);
        Assert.Equal(JsonSerializer.Serialize(marker.Ledger), JsonSerializer.Serialize(restarted.Get(fixture.Before)));
        var lookup = restarted.LookupPortfolioMutationReply(fixture.Before, "commit", JsonSerializer.SerializeToElement(request, JsonOptions));
        Assert.Equal("found", lookup["status"]!.GetValue<string>());
        Assert.False(lookup["execution_performed"]!.GetValue<bool>());
        Assert.False(lookup["ledger_mutation_performed"]!.GetValue<bool>());
        var recovered = lookup["reply"]!.Deserialize<ReservationPortfolioCommitResult>(JsonOptions)!;
        var recoveredMarker = scope.VerifyPreparationReply(recovered);
        Assert.True(CropSeedMaterialTransferBinding.IsBound(Assert.Single(fixture.Compile(recoveredMarker).Items), recoveredMarker.Ledger));
        var repeated = restarted.CommitReservationPortfolio(fixture.Before, request);
        Assert.False(repeated.Accepted);
        Assert.Equal(bytes, File.ReadAllBytes(Assert.Single(Directory.GetFiles(fixture.RepositoryPath))));
    }

    [Fact]
    public void APreviouslyUsedPartitionIdentityIsNotRepurposed()
    {
        using var fixture = new Fixture();
        var id = "owned-seeds:transfer-split:" + Step + ":1";
        var prior = JsonSerializer.Deserialize<MaterialReservation>(JsonSerializer.Serialize(fixture.Ledger.MaterialReservations[0]))!;
        prior.ReservationId = id;
        prior.Status = StrategyCommitmentStatuses.Completed;
        fixture.Ledger.MaterialReservations = fixture.Ledger.MaterialReservations.Append(prior).ToArray();
        fixture.AdoptLedger(fixture.Ledger);
        Assert.Throws<InvalidDataException>(() => fixture.Open());
    }
}
