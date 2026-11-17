using System.Security.Cryptography;
using System.Text.Json;
using StardewAI.Contracts.Training;
using StardewAI.Core.Infrastructure;

namespace StardewAI.Core.Tests;

public sealed partial class GoldenWalnutFreshSaveSourceScheduleVerifierTests
{
    [Fact]
    public void PinnedNativeEvidenceReplaysOriginalSourcesWithoutGrantingAdmission()
    {
        using var fixture = new Fixture(scheduleId: "golden-walnut-fresh-save:run",
            phaseOrderedSources: true, gourmandContinuation: true);
        var binding = NativeBinding(fixture);

        var replay = GoldenWalnutNativeEvidenceReader.Verify(binding);

        Assert.Equal(binding, replay.Binding);
        Assert.Equal("run", replay.SourceSchedule.SourceRunId);
        Assert.Equal("save", replay.SourceSchedule.SourceSaveId);
        Assert.True(replay.Continuation!.Ready);
        Assert.Equal(3, replay.Continuation.VerifiedTransitionCount);
        Assert.Empty(replay.Continuation.BlockingReasons);
        Assert.False(replay.TrainingLabelEligible);
        Assert.False(replay.FormalProductTrainingAuthorized);
    }

    [Fact]
    public void PinnedNativeEvidenceWithoutContinuationDoesNotInventNotApplicable()
    {
        using var fixture = new Fixture(scheduleId: "golden-walnut-fresh-save:run",
            phaseOrderedSources: true, gourmandContinuation: true);
        var binding = NativeBinding(fixture) with
        {
            GourmandContinuationReceiptPath = "",
            GourmandContinuationReceiptSha256 = ""
        };

        var replay = GoldenWalnutNativeEvidenceReader.Verify(binding);

        Assert.Null(replay.Continuation);
        Assert.False(replay.FormalProductTrainingAuthorized);
    }

    [Fact]
    public void PinnedNativeEvidenceRoundTripsItsSourceIdentityAndHashes()
    {
        using var fixture = new Fixture(scheduleId: "golden-walnut-fresh-save:run",
            phaseOrderedSources: true, gourmandContinuation: true);
        var binding = NativeBinding(fixture);
        var json = JsonSerializer.SerializeToElement(binding);
        Assert.Equal("run", json.GetProperty("source_run_id").GetString());
        Assert.Equal("save", json.GetProperty("source_save_id").GetString());
        Assert.Equal("player", json.GetProperty("source_player_id").GetString());
        var restored = json.Deserialize<GoldenWalnutNativeEvidenceBinding>()!;

        Assert.Equal(binding, restored);
        Assert.True(GoldenWalnutNativeEvidenceReader.Verify(restored).Continuation!.Ready);
    }

    [Theory]
    [InlineData("source")]
    [InlineData("continuation")]
    public void PinnedNativeEvidenceStillReplaysClaimsAfterTheirHashesAreRefreshed(string role)
    {
        using var fixture = new Fixture(scheduleId: "golden-walnut-fresh-save:run",
            phaseOrderedSources: true, gourmandContinuation: true, gourmandRequests: 1);
        var binding = NativeBinding(fixture);
        if (role == "source")
        {
            fixture.Change(binding.VerificationReceiptPath,
                root => root["schedule"]!["source_save_id"] = "other");
            binding = binding with { VerificationReceiptSha256 = NativeBindingHash(binding.VerificationReceiptPath) };
        }
        else
        {
            fixture.Change(binding.GourmandContinuationReceiptPath, root => root["ready"] = true);
            binding = binding with
            {
                GourmandContinuationReceiptSha256 = NativeBindingHash(binding.GourmandContinuationReceiptPath)
            };
        }

        Assert.Throws<InvalidDataException>(() => GoldenWalnutNativeEvidenceReader.Verify(binding));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void PinnedNativeEvidencePreservesPartialNativeContinuation(int requests)
    {
        using var fixture = new Fixture(scheduleId: "golden-walnut-fresh-save:run",
            phaseOrderedSources: true, gourmandContinuation: true, gourmandRequests: requests);

        var replay = GoldenWalnutNativeEvidenceReader.Verify(NativeBinding(fixture));

        Assert.False(replay.Continuation!.Ready);
        Assert.Equal(requests, replay.Continuation.VerifiedTransitionCount);
        Assert.NotEmpty(replay.Continuation.BlockingReasons);
        Assert.False(replay.FormalProductTrainingAuthorized);
    }

    [Theory]
    [InlineData("schedule")]
    [InlineData("receipt")]
    [InlineData("continuation")]
    [InlineData("snapshot")]
    [InlineData("manifest")]
    [InlineData("boundary")]
    public void PinnedNativeEvidenceRejectsChangedFrozenInputs(string role)
    {
        using var fixture = new Fixture(scheduleId: "golden-walnut-fresh-save:run",
            phaseOrderedSources: true, gourmandContinuation: true);
        var binding = NativeBinding(fixture);
        var path = role switch
        {
            "schedule" => binding.SourceSchedulePath,
            "receipt" => binding.VerificationReceiptPath,
            "continuation" => binding.GourmandContinuationReceiptPath,
            "snapshot" => fixture.SnapshotPath(2),
            "manifest" => fixture.ManifestPath,
            _ => fixture.BoundaryPath
        };
        File.AppendAllText(path, "\n");

        Assert.Throws<InvalidDataException>(() => GoldenWalnutNativeEvidenceReader.Verify(binding));
    }

    [Theory]
    [InlineData("schema")]
    [InlineData("game")]
    [InlineData("run")]
    [InlineData("save")]
    [InlineData("player")]
    [InlineData("missing_run")]
    [InlineData("schedule_hash")]
    [InlineData("receipt_hash")]
    [InlineData("continuation_hash")]
    [InlineData("continuation_path")]
    [InlineData("relative_path")]
    public void PinnedNativeEvidenceRejectsUnboundIdentityAndArtifactClaims(string change)
    {
        using var fixture = new Fixture(scheduleId: "golden-walnut-fresh-save:run",
            phaseOrderedSources: true, gourmandContinuation: true);
        var binding = NativeBinding(fixture);
        binding = change switch
        {
            "schema" => binding with { SchemaVersion = "unknown.v1" },
            "game" => binding with { GameVersion = "1.6.14" },
            "run" => binding with { SourceRunId = "other" },
            "save" => binding with { SourceSaveId = "other" },
            "player" => binding with { SourcePlayerId = "other" },
            "missing_run" => binding with { SourceRunId = "" },
            "schedule_hash" => binding with { SourceScheduleSha256 = "" },
            "receipt_hash" => binding with { VerificationReceiptSha256 = new string('0', 64) },
            "continuation_hash" => binding with { GourmandContinuationReceiptSha256 = "" },
            "continuation_path" => binding with { GourmandContinuationReceiptPath = "" },
            _ => binding with { SourceSchedulePath = "schedule.json" }
        };

        Assert.Throws<InvalidDataException>(() => GoldenWalnutNativeEvidenceReader.Verify(binding));
    }

    [Fact]
    public void PinnedNativeEvidenceCannotReuseAReadinessReceiptFromAnotherFrozenManifest()
    {
        using var fixture = new Fixture(scheduleId: "golden-walnut-fresh-save:run",
            phaseOrderedSources: true, gourmandContinuation: true);
        using var other = new Fixture(scheduleId: "golden-walnut-fresh-save:run",
            phaseOrderedSources: true, gourmandContinuation: true);
        var original = NativeBinding(fixture);
        var otherBinding = NativeBinding(other);
        var mixed = original with
        {
            GourmandContinuationReceiptPath = otherBinding.GourmandContinuationReceiptPath,
            GourmandContinuationReceiptSha256 = otherBinding.GourmandContinuationReceiptSha256
        };

        Assert.Throws<InvalidDataException>(() => GoldenWalnutNativeEvidenceReader.Verify(mixed));
    }

    private static GoldenWalnutNativeEvidenceBinding NativeBinding(Fixture fixture)
    {
        var receipts = WriteContinuationAuditReceipts(fixture);
        return new GoldenWalnutNativeEvidenceBinding(
            GoldenWalnutNativeEvidenceReader.BindingSchemaVersion,
            "run", "save", "player", "1.6.15",
            fixture.SchedulePath, NativeBindingHash(fixture.SchedulePath),
            receipts.Source, NativeBindingHash(receipts.Source),
            receipts.Continuation, NativeBindingHash(receipts.Continuation));
    }

    private static string NativeBindingHash(string path) =>
        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
}
