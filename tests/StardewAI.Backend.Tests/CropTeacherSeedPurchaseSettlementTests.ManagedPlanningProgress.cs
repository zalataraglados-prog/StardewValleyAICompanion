using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Fact]
    public async Task ManagedPlanningProgressDoesNotPromoteRouteOrMaintenanceAndDoesNotWrite()
    {
        using var f = new OvernightFixture();
        var writes = 0;
        await using var owner = await f.Open(async (path, json) =>
        {
            writes++;
            await InvokeLive<Task>("WritePortfolioJournalFileAsync", path, json);
        });
        Assert.Empty(ReadManagedPlanningProgress(owner).Nights);
        await f.Home(owner);
        var before = (writes, f.Shared.Runtime.Handler.Commits, f.Shared.Runtime.Handler.LedgerReads);
        var actual = ReadManagedPlanningProgress(owner);
        Assert.Empty(actual.Nights);
        Assert.Empty(actual.Harvest);
        Assert.Equal(before, (writes, f.Shared.Runtime.Handler.Commits, f.Shared.Runtime.Handler.LedgerReads));
    }

    [Fact]
    public async Task ManagedPlanningProgressRetainsOriginalAcceptedNightAcrossNativeReadback()
    {
        using var f = new SavedReadbackOwnerFixture();
        await using var owner = await f.Night.Open();
        await f.CompleteNight(owner);
        var prior = ReadManagedPlanningProgress(owner);
        Assert.Single(prior.Nights);
        Assert.Empty(prior.Harvest);
        await f.Run(owner, await f.Admit(owner));
        var after = ReadManagedPlanningProgress(owner);
        Assert.True(prior.Nights.SetEquals(after.Nights));
        Assert.Empty(after.Harvest);
        Assert.Equal(1, f.Night.NativeNightCalls);
    }

    [Fact]
    public async Task ManagedPlanningProgressReturnsOnlyOriginalAcceptedHarvestTiles()
    {
        using var f = new OvernightFixture();
        await using var owner = await f.Open();
        await f.Home(owner);
        var night = await f.PrepareNight(owner);
        await owner.ExecuteOriginalOvernightAsync(night, f.Shared.Runtime.Client, () => f.EmitNight(owner));
        await f.RebindNight(owner, night);
        await f.Farm(owner);
        var harvest = await f.PrepareHarvest(owner);
        var admission = Path.Combine(f.Options.EvidenceDirectory, $"operation-{harvest.Sequence:D6}-intent.json");
        var step = JsonNode.Parse(File.ReadAllText(admission))!["harvest_step"]!.Deserialize<CropTeacherRunStep>(JsonOptions)!;
        await owner.ExecuteOriginalHarvestAsync(harvest, f.Shared.Runtime.Client, () => f.EmitHarvest(owner));
        await f.RebindHarvest(owner, harvest);
        var actual = ReadManagedPlanningProgress(owner);
        Assert.True(actual.Harvest.SetEquals(step.ActionBatch!.Tiles.Select(tile =>
            CropTeacherTransitionLedger.HarvestTileKey(step.CommitmentId, step.TotalDay, tile.TileX, tile.TileY))));
        Assert.Contains(CropTeacherTransitionLedger.DayBoundaryKey(step.TotalDay - 1), actual.Nights);
        Assert.Equal(1, f.NativeHarvestCalls);
    }

    [Fact]
    public async Task ManagedPlanningProgressRequiresOriginalRestoreOnReopen()
    {
        using var f = new SavedReadbackOwnerFixture();
        await using (var owner = await f.Night.Open()) await f.CompleteNight(owner);
        await using var reopened = await f.Night.Open();
        Assert.Throws<InvalidOperationException>(() => ReadManagedPlanningProgress(reopened));
        await reopened.RestoreHeadAsync(f.Night.Shared.Runtime.Client, f.Night.Shared.Files.Recovery, f.Night.Shared.Files.Post);
        Assert.Single(ReadManagedPlanningProgress(reopened).Nights);
        Assert.Equal(1, f.Night.NativeNightCalls);
    }

    [Fact]
    public async Task ManagedPlanningProgressRejectsPendingNightWithoutGrantingDayCredit()
    {
        using var f = new OvernightFixture();
        await using var owner = await f.Open();
        await f.Home(owner);
        var night = await f.PrepareNight(owner);
        Assert.Same(night, owner.Pending);
        Assert.Throws<InvalidOperationException>(() => ReadManagedPlanningProgress(owner));
        Assert.Equal(0, f.NativeNightCalls);
    }

    [Fact]
    public async Task ManagedPlanningProgressRejectsHistoricalCertificateTamperingOutsideCurrentFrontier()
    {
        using var f = new SavedReadbackOwnerFixture();
        await using var owner = await f.Night.Open();
        await f.CompleteNight(owner);
        var path = Path.Combine(f.Night.Options.EvidenceDirectory, "operation-000001-accepted.json");
        var bytes = File.ReadAllBytes(path);
        try
        {
            File.AppendAllText(path, " ");
            Assert.Throws<InvalidDataException>(() => ReadManagedPlanningProgress(owner));
        }
        finally { File.WriteAllBytes(path, bytes); }
    }

    [Fact]
    public async Task ManagedPlanningProgressReturnsIndependentSets()
    {
        using var f = new SavedReadbackOwnerFixture();
        await using var owner = await f.Night.Open();
        await f.CompleteNight(owner);
        var actual = ReadManagedPlanningProgress(owner);
        Assert.IsAssignableFrom<ISet<string>>(actual.Nights).Clear();
        Assert.Single(ReadManagedPlanningProgress(owner).Nights);
    }

    private static (IReadOnlySet<string> Harvest, IReadOnlySet<string> Nights) ReadManagedPlanningProgress(CropTeacherManagedSeedOwner owner)
    {
        var method = typeof(CropTeacherManagedSeedOwner).GetMethod("ReadPlanningProgress", BindingFlags.NonPublic | BindingFlags.Instance)!;
        object value;
        try { value = method.Invoke(owner, null)!; }
        catch (TargetInvocationException error) when (error.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(error.InnerException).Throw();
            throw;
        }
        return (Assert.IsAssignableFrom<IReadOnlySet<string>>(value.GetType().GetProperty("CompletedHarvestTileKeys")!.GetValue(value)),
            Assert.IsAssignableFrom<IReadOnlySet<string>>(value.GetType().GetProperty("CompletedDayBoundaryKeys")!.GetValue(value)));
    }
}
