using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Core.Strategy;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Fact]
    public async Task NativeReload_CaptureRejectsAProcessNotRunningTheBoundGameBeforeHttpOrOutput()
    {
        var f = new NativeReloadFixture();
        var output = Path.Combine(f.Root, "rejected-capture");
        using var client = new HttpClient(new RejectReloadCaptureHttp());
        await Assert.ThrowsAsync<InvalidDataException>(() => InvokeLive<Task<string>>("CaptureCropTeacherNativeReloadAsync", client,
            f.Observation.RunId, f.Resources.BeforePath, f.Resources.LedgerPath, f.Observation.OvernightSaveBoundaryPath,
            f.Observation.SaveBinding, Environment.ProcessId, f.Observation.Process.ExecutablePath, f.LogPath,
            "http://localhost:18888/api/v1/snapshot", output));
        Assert.False(Directory.Exists(output));
    }

    private sealed class RejectReloadCaptureHttp : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) =>
            throw new InvalidOperationException("Wrong process must be rejected before any bridge request.");
    }

    [Fact]
    public void NativeReload_SeparatesCanonicalSavedContentsFromCurrentAcceptedVolatileHead()
    {
        var f = new NativeReloadFixture();
        var canonicalPath = Path.Combine(f.Root, "canonical-saved-snapshot.json");
        File.Copy(f.Resources.BeforePath, canonicalPath);
        var saved = JsonNode.Parse(File.ReadAllText(f.Observation.OvernightSaveBoundaryPath))!;
        saved["after_snapshot_path"] = canonicalPath; Fixture.Save(f.Observation.OvernightSaveBoundaryPath, saved);
        ChangeState(f.Resources.Before, "player", player => player["tile_x"]!["value"] = 9);
        f.Resources.Before.RealTimestamp = "2026-11-12T00:00:30Z";
        f.Resources.Before.StateHash = SnapshotHash.ComputeStateHash(f.Resources.Before.State);
        f.Resources.Ledger.SourceStateHash = f.Resources.Before.StateHash; f.Resources.Save();
        foreach (var path in new[] { canonicalPath, f.Resources.BeforePath, f.Resources.LedgerPath, f.Observation.OvernightSaveBoundaryPath }) f.PinAgain(path);
        f.SaveObservation();
        Assert.True(f.Open().SemanticSavedContentVerified);
        var changed = JsonNode.Parse(File.ReadAllText(canonicalPath))!;
        changed["state"]!["player"]!["money"]!["value"] = 1;
        var snapshot = changed.Deserialize<StardewAI.Contracts.State.SnapshotEnvelope>(JsonOptions)!;
        snapshot.StateHash = SnapshotHash.ComputeStateHash(snapshot.State); Fixture.Save(canonicalPath, snapshot);
        f.PinAgain(canonicalPath); f.SaveObservation();
        Assert.Contains("persistent resources differ", Assert.Throws<InvalidDataException>(() => f.Open()).Message);
    }

    [Fact]
    public void NativeReload_DeclaredIndependentLoadWitnessAndContentsPermitOriginalRebindWithoutTrainingCredit()
    {
        var f = new NativeReloadFixture(); var scope = f.Open();
        Assert.True(scope.NativeReloadVerified); Assert.True(scope.SemanticSavedContentVerified);
        Assert.False(scope.FormalProductTrainingAuthorized);
        var reply = new ReservationPortfolioLedgerService().Commit(f.Resources.Ledger, f.Resources.After, scope.RebindRequest, RecordedAt);
        Assert.True(reply.Accepted, string.Join(",", reply.Errors));
        AssertOtherRootsUnchanged(f.Resources.Ledger, scope.VerifyRebindReply(reply));
        File.AppendAllText(f.LogPath, " ");
        Assert.Throws<InvalidDataException>(() => scope.ValidateInputsUnchanged());
    }

    [Theory]
    [InlineData("changed_save", "exact original completed save")]
    [InlineData("old_process", "newly started original game process")]
    [InlineData("executable", "newly started original game process")]
    [InlineData("log_slot", "save-root/slot load observations")]
    [InlineData("event_hash", "event hash/sequence/time")]
    [InlineData("no_load", "one original SaveLoaded")]
    [InlineData("no_publish", "untouched post-load snapshot")]
    [InlineData("partial", "fresh process chain")]
    [InlineData("resource", "persistent resources differ")]
    [InlineData("copied_snapshot", "newly started original game process")]
    [InlineData("before_load", "untouched post-load snapshot")]
    public void NativeReload_RejectsDeclaredProvenanceOrContentDriftAtItsActualBoundary(string fault, string expected)
    {
        var f = new NativeReloadFixture();
        switch (fault)
        {
            case "changed_save": f.Observation = f.Observation with { AfterReadSave = f.Observation.AfterReadSave with { Sha256 = new string('0', 64) } }; break;
            case "old_process": f.Observation = f.Observation with { Process = f.Observation.Process with { StartedAtUtc = "2026-11-11T23:59:00Z" } }; break;
            case "executable": f.Observation = f.Observation with { Process = f.Observation.Process with { ExecutableSha256 = new string('0', 64) } }; break;
            case "log_slot":
                File.AppendAllText(f.LogPath, "suffix");
                File.WriteAllText(f.LogPath, File.ReadAllText(f.LogPath).Replace(f.Observation.SaveBinding.SaveSlot + "\n", f.Observation.SaveBinding.SaveSlot + "-other\n"));
                f.PinAgain(f.LogPath); break;
            case "event_hash": f.Events.Events[0].GameTick++; Fixture.Save(f.EventsPath, f.Events); f.PinAgain(f.EventsPath); break;
            case "no_load": f.Events.Events[0].EventType = "Other"; f.RehashEvents(); f.PinAgain(f.EventsPath); break;
            case "no_publish": f.Events.Events[1].PublishedSnapshotHash = "foreign"; f.RehashEvents(); f.PinAgain(f.EventsPath); break;
            case "partial": f.Events.Events = f.Events.Events.Skip(1).ToArray(); f.RehashEvents(); f.PinAgain(f.EventsPath); break;
            case "resource":
                ChangeState(f.Resources.After, "player", player => player["money"]!["value"] = player["money"]!["value"]!.GetValue<int>() + 1);
                f.Resources.Save(); f.Events.Events[1].PublishedSnapshotHash = f.Resources.After.StateHash;
                f.Events.Events[1].ObservedSnapshotHash = f.Resources.After.StateHash; f.RehashEvents();
                f.PinAgain(f.Resources.AfterPath); f.PinAgain(f.EventsPath); break;
            case "copied_snapshot": f.Observation = f.Observation with { LoadedSnapshotPath = f.Resources.BeforePath }; break;
            case "before_load":
                f.Resources.After.RealTimestamp = "2026-11-12T00:01:00.5Z"; f.Resources.Save(); f.PinAgain(f.Resources.AfterPath); break;
        }
        f.SaveObservation();
        Assert.Contains(expected, Assert.Throws<InvalidDataException>(() => f.Open()).Message);
    }
}
