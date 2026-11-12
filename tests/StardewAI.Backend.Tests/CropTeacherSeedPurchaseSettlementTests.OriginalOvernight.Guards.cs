using System.Text.Json.Nodes;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OriginalOvernightRecoveryCannotWriteEvidenceIntoItsNativeSaveSlot(bool restoreAcceptedHead)
    {
        using var f = new OvernightFixture();
        var armed = false;
        await using var owner = await f.Open(async (path, json) =>
        {
            if (armed && !restoreAcceptedHead && Path.GetFileName(path) == "marker-reply.json")
                throw new IOException("Marker committed before local reply.");
            await InvokeLive<Task>("WritePortfolioJournalFileAsync", path, json);
        });
        await f.Home(owner);
        var night = await f.PrepareNight(owner);
        await owner.ExecuteOriginalOvernightAsync(night, f.Shared.Runtime.Client, () => f.EmitNight(owner));
        armed = true;
        if (restoreAcceptedHead) await f.RebindNight(owner, night);
        else await Assert.ThrowsAsync<IOException>(() => f.RebindNight(owner, night));
        await owner.DisposeAsync();
        await using var reopened = await f.Open();
        var binding = f.Options.NativeSaveBinding!;
        var fingerprint = NativeSaveBoundaryVerifier.Capture(binding.SaveIsolationPath, binding.SaveSlot);
        var unsafeOutput = Path.Combine(Path.GetDirectoryName(f.NativeSaveFile)!, "recovery-evidence");
        await Assert.ThrowsAsync<IOException>(() => restoreAcceptedHead
            ? reopened.RestoreHeadAsync(f.Shared.Runtime.Client, unsafeOutput, f.Shared.Files.Post)
            : reopened.RecoverPendingAsync(f.Shared.Runtime.Client, unsafeOutput, f.Shared.Files.Post));
        Assert.False(Directory.Exists(unsafeOutput));
        Assert.Equal(fingerprint, NativeSaveBoundaryVerifier.Capture(binding.SaveIsolationPath, binding.SaveSlot));
        Assert.Equal(0, f.Shared.Runtime.Handler.Lookups);
        if (restoreAcceptedHead) await reopened.RestoreHeadAsync(f.Shared.Runtime.Client, f.Shared.Files.Recovery, f.Shared.Files.Post);
        else await reopened.RecoverPendingAsync(f.Shared.Runtime.Client, f.Shared.Files.Recovery, f.Shared.Files.Post);
        Assert.Equal(3, f.Shared.Runtime.Handler.Commits); Assert.Null(reopened.Pending);
        Assert.False(reopened.HeadRecoveryRequired);
    }

    [Fact]
    public async Task OriginalOvernightDoesNotCountAnUnplantedLayoutAsCropProgress()
    {
        using var f = new OvernightFixture(noCrops: true);
        await using var owner = await f.Open();
        await f.Home(owner, water: false);
        var night = await f.PrepareNight(owner);
        await owner.ExecuteOriginalOvernightAsync(night, f.Shared.Runtime.Client, () => f.EmitNight(owner));
        await f.RebindNight(owner, night);
        var accepted = JsonNode.Parse(File.ReadAllText(Path.Combine(f.Shared.Files.OwnerRoot, $"operation-{night.Sequence:D6}-accepted.json")))!;
        Assert.Equal(0, accepted["verified_crop_tile_count"]!.GetValue<int>());
        Assert.Equal(2, accepted["unverified_layout_tile_count"]!.GetValue<int>());
        Assert.True(accepted["native_save_boundary_verified"]!.GetValue<bool>());
        Assert.False(accepted["native_crop_lifecycle_verified"]!.GetValue<bool>());
        Assert.Equal(2, f.Shared.Runtime.Handler.Commits);
    }

    [Fact]
    public async Task OriginalOvernightCompilerRejectsAChangedLiveSaveSlot()
    {
        using var f = new OvernightFixture();
        await using var owner = await f.Open();
        await f.Home(owner);
        f.Shared.Runtime.LiveOptions.SaveSlot = "DifferentDeclaredSlot";
        await Assert.ThrowsAsync<InvalidDataException>(() => f.PrepareNight(owner));
        Assert.Null(owner.Pending); Assert.Equal(0, f.NativeNightCalls);
        Assert.False(File.Exists(f.NightQueue));
    }

    [Fact]
    public async Task OriginalOvernightSaveSlotCannotBePollutedByOwnerArtifactsOrReboundOnRestart()
    {
        using var f = new OvernightFixture();
        var slot = Path.GetDirectoryName(f.NativeSaveFile)!;
        await Assert.ThrowsAsync<IOException>(() => f.Shared.Files.Open(f.Options with { EvidenceDirectory = Path.Combine(slot, "owner-artifacts") }));
        Assert.False(Directory.Exists(Path.Combine(slot, "owner-artifacts")));
        await using var owner = await f.Open();
        await owner.DisposeAsync();
        await Assert.ThrowsAsync<InvalidDataException>(() => f.Shared.Files.Open(f.Options with
        { NativeSaveBinding = f.Options.NativeSaveBinding! with { SaveSlot = "DifferentDeclaredSlot" } }));
    }

    [Fact]
    public async Task OriginalOvernightRejectsAlteredRetainedSaveEvidenceOnRestart()
    {
        using var f = new OvernightFixture();
        await using var owner = await f.Open();
        await f.Home(owner);
        var night = await f.PrepareNight(owner);
        await owner.ExecuteOriginalOvernightAsync(night, f.Shared.Runtime.Client, () => f.EmitNight(owner));
        await f.RebindNight(owner, night);
        await owner.DisposeAsync();
        var path = Path.Combine(f.NightRuntime, "overnight-save-boundary.json");
        var report = JsonNode.Parse(File.ReadAllText(path))!;
        report["to_total_day"] = report["to_total_day"]!.GetValue<int>() + 1;
        Fixture.Save(path, report);
        await Assert.ThrowsAsync<InvalidDataException>(() => f.Open());
    }
}
