using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Contracts.Training;
using StardewAI.Core.Infrastructure;

namespace StardewAI.GoalConditionedBootstrap;

public sealed partial class CropTeacherOvernight
{
    private readonly Preparation preparation;
    private readonly CropTeacherRetainedInputs inputs;
    private readonly CropTeacherSupportingRebind rebind;

    private CropTeacherOvernight(Preparation preparation, CropTeacherSupportingSource source, string[] commitmentIds,
        string stepId, MoneyRevenueCropNativeLifecycleTransitionRequest transition)
    {
        this.preparation = preparation;
        preparation.ValidateInputsUnchanged();
        var comparer = CropTeacherSeedTransferNativeOutcome.PathComparer;
        Require(transition.TransitionKind == "native_day_rollover" && transition.TotalDay == preparation.TotalDay &&
                transition.SelectedCandidateId == preparation.CandidateId &&
                new[] { transition.QueuePath, transition.BeforeSnapshotPath, transition.ExecutionReceiptPath, transition.AfterSnapshotPath }.All(Path.IsPathFullyQualified) &&
                comparer.Equals(Path.GetFullPath(transition.QueuePath), preparation.QueuePath) && comparer.Equals(Path.GetFullPath(transition.BeforeSnapshotPath), preparation.SourcePath),
            "Owned crop sleep differs from its original compilation/source/day.");
        var paths = preparation.InputHashes.Keys.Concat(new[] { transition.ExecutionReceiptPath, transition.AfterSnapshotPath })
            .Select(Path.GetFullPath).Distinct(comparer).ToArray();
        using var reads = InvocationReadSession.Start(paths);
        inputs = new(paths);
        var after = MoneyRevenueCropNativeLifecycleProofBuilder.VerifyOwnedSleepBoundary(source.Snapshot, transition);
        var receipt = CurrentTeacherFrontierSupport.Read<QueueExecutionReceiptEnvelope>(transition.ExecutionReceiptPath, "original crop sleep receipt");
        var steps = CropTeacherNativeStepPaths.Read(transition, receipt, paths, "Crop sleep");
        Require(steps.Length == 1 && comparer.Equals(steps[0], Path.GetFullPath(transition.AfterSnapshotPath)),
            "Owned crop sleep lost its single terminal snapshot path.");
        Require(source.Snapshot.State.TryGetValue("farm", out var farm) && farm.ValueKind == JsonValueKind.Object &&
                farm.TryGetProperty("crops", out var envelope) && envelope.ValueKind == JsonValueKind.Object,
            "Owned crop sleep lacks its original farm crop observation.");
        var observed = source.Snapshot.State["farm"].GetProperty("crops").Deserialize<FieldEnvelope<JsonElement>>();
        Require(observed is not null && FieldEnvelopeValidator.IsReadableStatus(observed.Status) && observed.Value.ValueKind == JsonValueKind.Array,
            "Owned crop sleep cannot determine its planted layout tiles.");
        var crops = observed!.Value.EnumerateArray().ToArray();
        foreach (var crop in source.Ledger.CropPlantingCommitments.Where(row => commitmentIds.Contains(row.CommitmentId, StringComparer.Ordinal) &&
                     preparation.TotalDay >= row.PlantingTotalDay && row.HarvestTotalDays.Any(day => day > preparation.TotalDay)))
        {
            var layout = source.RunRequest.LayoutIntents.Single(row => row.CommitmentId == crop.CommitmentId);
            foreach (var tile in layout.Tiles)
            {
                var present = crops.Any(row => row.TryGetProperty("location_id", out var location) && location.GetString() == layout.LocationId &&
                    row.TryGetProperty("tile_x", out var x) && x.GetInt32() == tile.TileX && row.TryGetProperty("tile_y", out var y) && y.GetInt32() == tile.TileY);
                // A control sleep does not turn an unplanted obligation into a completed crop.
                if (!present) { UnverifiedLayoutTileCount++; continue; }
                MoneyRevenueCropNativeLifecycleProofBuilder.VerifyOwnedDayGrowth(source.Snapshot, after, layout.LocationId, tile,
                    crop.SeedQualifiedItemId, crop.HarvestItemQualifiedId);
                VerifiedCropTileCount++;
            }
        }
        AfterSnapshotPath = Path.GetFullPath(transition.AfterSnapshotPath);
        FromTotalDay = preparation.TotalDay;
        ToTotalDay = MoneyRevenueCropSeedAcquisitionProofBuilder.ReadInt(after, "time", "total_days");
        rebind = new(after, source.Ledger, stepId, ValidateInputsUnchanged, "Crop overnight control");
        ValidateInputsUnchanged();
    }

    public int FromTotalDay { get; }
    public int ToTotalDay { get; }
    public int VerifiedCropTileCount { get; }
    public int UnverifiedLayoutTileCount { get; }
    public string AfterSnapshotPath { get; }
    public bool NativeCropLifecycleVerified => false;
    public bool FormalProductTrainingAuthorized => false;
    public StrategyCommitmentLedger BaseLedger => rebind.BaseLedger;
    public ReservationPortfolioCommitRequest RebindRequest => rebind.Request;
    public StrategyCommitmentLedger VerifyRebindReply(ReservationPortfolioCommitResult reply) => rebind.VerifyReply(reply);
    public IReadOnlyDictionary<string, string> InputHashes => inputs.ReadHashes();
    public void ValidateInputsUnchanged() { preparation.ValidateInputsUnchanged(); inputs.Validate(); }
}
