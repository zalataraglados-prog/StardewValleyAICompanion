using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Strategy;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedLifecycleRuntimeTests
{
    [Theory]
    [InlineData(false, false, false, false)]
    [InlineData(true, false, false, false)]
    [InlineData(false, false, true, false)]
    [InlineData(true, false, true, false)]
    [InlineData(false, true, true, false)]
    [InlineData(false, false, false, true)]
    [InlineData(true, false, false, true)]
    public async Task OriginalLifecycleRecoveryReplaysRetainedClosureBeforeReleaseAndNeverResends(
        bool completion, bool lostCancel, bool missingRelease, bool noResources)
    {
        using var f = new RuntimeFixture(completion: completion, loseCancel: lostCancel, noResources: noResources,
            otherExpires: lostCancel);
        var final = await f.Run();
        if (lostCancel)
            Assert.Throws<InvalidDataException>(() => f.Files.Open().BeginCancellation(f.Reason).ResumeFromReadback(final.Ledger));
        if (missingRelease)
        {
            var path = Path.Combine(f.Output, "release-reply.json");
            File.Move(path, path + ".retained-test");
        }
        var cancelCount = f.Handler.Cancellations;
        var releases = f.Handler.Releases;
        var lookupCount = f.Handler.Lookups;
        var reads = f.Handler.Reads;
        var eventCount = f.Handler.Events.Count;
        var result = await f.Recover();
        Assert.Equal(JsonSerializer.Serialize(final.Ledger, JsonOptions), JsonSerializer.Serialize(result.ReadLedger(), JsonOptions));
        Assert.Equal(f.Files.SnapshotPath, result.SnapshotPath);
        Assert.Equal(f.Files.Step, result.ReleaseStepId);
        Assert.Equal(f.Options.RunId, result.RunId);
        Assert.Equal(cancelCount, f.Handler.Cancellations);
        Assert.Equal(releases, f.Handler.Releases);
        Assert.Equal(lookupCount + (missingRelease ? 1 : 0), f.Handler.Lookups);
        Assert.Equal(reads + 1, f.Handler.Reads);
        Assert.Equal(missingRelease ? new[] { "lookup", "get" } : new[] { "get" }, f.Handler.Events.Skip(eventCount));
        var summary = JsonNode.Parse(File.ReadAllText(Path.Combine(f.RecoveryOutput, "recovery-summary.json")))!;
        Assert.Equal("original_crop_seed_lifecycle_recovered", summary["status"]!.GetValue<string>());
        foreach (var name in new[] { "cancellation_resent", "portfolio_mutation_requested", "native_outcome_verified",
                     "execution_authorized", "formal_product_training_authorized" })
            Assert.False(summary[name]!.GetValue<bool>());
        await Assert.ThrowsAsync<IOException>(() => f.Recover());
        Assert.Equal(reads + 1, f.Handler.Reads);
        var detached = result.ReadLedger();
        detached.MaterialReservations.Single(row => row.ReservationId == "outside").Quantity = 999;
        Assert.NotEqual(999, result.ReadLedger().MaterialReservations.Single(row => row.ReservationId == "outside").Quantity);
        File.AppendAllText(Path.Combine(f.Output, "verified-closure-ledger.json"), "\n");
        Assert.Throws<InvalidDataException>(() => result.ReadLedger());
    }

    [Theory]
    [InlineData("intent")]
    [InlineData("run")]
    [InlineData("reason")]
    [InlineData("closure")]
    [InlineData("cancel_reply")]
    [InlineData("cancel_journal")]
    [InlineData("release_reply")]
    [InlineData("release_journal")]
    public async Task OriginalLifecycleRecoveryRejectsChangedOriginalEvidenceWithoutFallbackOrMutation(string fault)
    {
        using var f = new RuntimeFixture();
        await f.Run();
        var options = f.RecoveryOptions;
        var reads = f.Handler.Reads;
        var lookups = f.Handler.Lookups;
        switch (fault)
        {
            case "intent": options = options with { ExpectedIntentSha256 = new string('a', 64) }; break;
            case "run": options = options with { RunId = "another-run" }; break;
            case "reason": options = options with { CancellationReason = "another-reason" }; break;
            case "closure":
                Change("verified-closure-ledger.json", node => node["material_reservations"]!.AsArray()
                    .Single(row => row!["reservation_id"]!.GetValue<string>() == "outside")!["quantity"] = 999);
                break;
            case "cancel_reply": Change("crop-cancel-reply.json", node => node["accepted"] = false); break;
            case "release_reply": Change("release-reply.json", node => node["accepted"] = false); break;
            case "cancel_journal": Change("crop-cancel-journal.json", node => node["run_id"] = "another-run"); break;
            case "release_journal":
                var path = Directory.GetFiles(f.Options.SnapshotDir, "*-rpc-journal.json").Single();
                var journal = JsonNode.Parse(File.ReadAllText(path))!;
                journal["backend_url"] = "http://another-backend";
                File.WriteAllText(path, journal.ToJsonString());
                break;
        }
        await Assert.ThrowsAsync<InvalidDataException>(() => f.Recover(options));
        Assert.Equal(1, f.Handler.Cancellations);
        Assert.Equal(1, f.Handler.Releases);
        Assert.Equal(reads, f.Handler.Reads);
        Assert.Equal(lookups, f.Handler.Lookups);

        void Change(string name, Action<JsonNode> change)
        {
            var path = Path.Combine(f.Output, name);
            var node = JsonNode.Parse(File.ReadAllText(path))!;
            change(node);
            File.WriteAllText(path, node.ToJsonString());
        }
    }

    [Theory]
    [InlineData("missing_closure")]
    [InlineData("missing_release_journal")]
    [InlineData("missing_lookup")]
    [InlineData("final_readback")]
    public async Task OriginalLifecycleRecoveryPreservesUnknownOrUnverifiedBoundariesWithoutResend(string fault)
    {
        using var f = new RuntimeFixture(fault: fault == "missing_lookup" ? "release_lookup" : fault == "final_readback" ? fault : "");
        if (fault == "final_readback") await Assert.ThrowsAsync<InvalidDataException>(() => f.Run());
        else await f.Run();
        var reads = f.Handler.Reads;
        var lookups = f.Handler.Lookups;
        if (fault == "missing_closure") Move(Path.Combine(f.Output, "verified-closure-ledger.json"));
        if (fault is "missing_release_journal" or "missing_lookup") Move(Path.Combine(f.Output, "release-reply.json"));
        if (fault == "missing_release_journal") Move(Directory.GetFiles(f.Options.SnapshotDir, "*-rpc-journal.json").Single());
        if (fault is "missing_closure" or "missing_release_journal") await Assert.ThrowsAsync<FileNotFoundException>(() => f.Recover());
        else await Assert.ThrowsAsync<InvalidDataException>(() => f.Recover());
        Assert.Equal(reads + (fault == "final_readback" ? 1 : 0), f.Handler.Reads);
        Assert.Equal(lookups + (fault == "missing_lookup" ? 1 : 0), f.Handler.Lookups);
        Assert.Equal(1, f.Handler.Cancellations);
        Assert.Equal(1, f.Handler.Releases);
        static void Move(string path) => File.Move(path, path + ".retained-test");
    }
}
