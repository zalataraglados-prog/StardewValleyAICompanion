using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.Training;

namespace StardewAI.GoalConditionedBootstrap;

// The bounded observation supplies geometry, never a replacement accepted resource head.
public sealed partial class CropTeacherSiteApproachObservation
{
    private readonly CropTeacherRetainedInputs inputs;

    private CropTeacherSiteApproachObservation(string runRequestPath, string sourcePath, string ledgerPath,
        IReadOnlyCollection<string> commitmentIds, string commitmentId, string stepId, string observationPath)
    {
        var paths = new[] { runRequestPath, sourcePath, ledgerPath, observationPath }.Select(Path.GetFullPath)
            .Distinct(CropTeacherSeedTransferNativeOutcome.PathComparer).ToArray();
        using var reads = InvocationReadSession.Start(paths);
        inputs = new(paths);
        var source = CropTeacherSupportingSource.Read(runRequestPath, sourcePath, ledgerPath, commitmentIds, stepId);
        var accepted = NativeExecutionSourceSupport.ReadTyped<SnapshotEnvelope>(sourcePath, "snapshot.v1");
        var observed = NativeExecutionSourceSupport.ReadTyped<SnapshotEnvelope>(observationPath, "snapshot.v1");
        NativeExecutionSourceSupport.ValidateSnapshot(accepted);
        NativeExecutionSourceSupport.ValidateSnapshot(observed);
        Require(EqualJson(accepted, source.Snapshot) && source.Ledger.SourceStateHash == accepted.StateHash &&
                accepted.GameTick == observed.GameTick && accepted.GameTick >= 0 && accepted.GameVersion == observed.GameVersion &&
                accepted.BridgeVersion == observed.BridgeVersion &&
                NativeExecutionSourceSupport.Identity(accepted.SaveId) == NativeExecutionSourceSupport.Identity(observed.SaveId) &&
                NativeExecutionSourceSupport.Identity(accepted.PlayerId) == NativeExecutionSourceSupport.Identity(observed.PlayerId),
            "Site approach observation is not bound to the accepted frame/actor/ledger.");
        foreach (var field in new[] { "year", "season", "day", "total_days", "time" })
        {
            var before = Value(accepted, "time", field); var after = Value(observed, "time", field);
            Require((field == "season" ? before.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(before.GetString()) :
                    before.ValueKind == JsonValueKind.Number && before.TryGetInt32(out _)) && EqualJson(before, after),
                "Site approach observation changed or mistyped its accepted calendar/clock.");
        }
        foreach (var field in new[] { "location_id", "tile_x", "tile_y" })
            Require(EqualJson(Value(accepted, "player", field), Value(observed, "player", field)),
                "Site approach observation changed its accepted location/position.");
        var crop = source.Ledger.CropPlantingCommitments.SingleOrDefault(row => row.CommitmentId == commitmentId &&
            row.Status == StrategyCommitmentStatuses.Active && commitmentIds.Contains(commitmentId, StringComparer.Ordinal));
        TotalDay = NativeExecutionSourceSupport.TotalDay(accepted);
        var location = Value(accepted, "player", "location_id");
        Require(crop is not null && TotalDay <= crop.PlantingTotalDay && source.CropLocation == "Farm" &&
                location.ValueKind == JsonValueKind.String && location.GetString() == "Farm",
            "Site approach observation is outside its original active Farm/date obligation.");
        var layout = source.RunRequest.LayoutIntents.Single(row => row.CommitmentId == commitmentId);
        var preparation = layout.Tiles.Concat(layout.WateringRefillPreparationTiles ?? Array.Empty<MoneyRevenueCropPlantingTile>()).ToArray();
        Require(layout.Tiles.Length == crop!.TileCount && preparation.Length == preparation.Distinct().Count(),
            "Site approach observation changed or overlapped its original preparation layout.");
        var context = ReadContext(accepted, observed, preparation);
        Target = CropSiteApproachSelection.Select(Int(Value(accepted, "player", "tile_x")),
            Int(Value(accepted, "player", "tile_y")), preparation.Select(tile => (tile.TileX, tile.TileY)).ToArray(), context);
        if (Target is null)
        {
            var x = Int(Value(accepted, "player", "tile_x")); var y = Int(Value(accepted, "player", "tile_y"));
            Require(context.Any(tile => tile.TileX == x && tile.TileY == y && tile.MapPassable &&
                    !tile.NativeBlockedWithoutCharacters && tile.NativeApproachStandable &&
                    preparation.Any(target => Math.Abs(target.TileX - x) + Math.Abs(target.TileY - y) == 1)),
                "Site approach has no verified stand; absent target is not arrival.");
        }
        SourcePath = Path.GetFullPath(sourcePath); ObservationPath = Path.GetFullPath(observationPath);
        SourceStateHash = accepted.StateHash; ObservationStateHash = observed.StateHash;
        inputs.Validate();
    }

    public static CropTeacherSiteApproachObservation Open(string runRequestPath, string sourcePath, string ledgerPath,
        IReadOnlyCollection<string> commitmentIds, string commitmentId, string stepId, string observationPath) =>
        new(runRequestPath, sourcePath, ledgerPath, commitmentIds, commitmentId, stepId, observationPath);
    public string SourcePath { get; }
    public string ObservationPath { get; }
    public string SourceStateHash { get; }
    public string ObservationStateHash { get; }
    public int TotalDay { get; }
    public CropSiteApproachTile? Target { get; }
    public bool AlreadyAtStand => Target is null;
    public bool NativeOutcomeVerified => false;
    public bool ResourceBoundaryVerified => false;
    public bool FormalProductTrainingAuthorized => false;
    public IReadOnlyDictionary<string, string> InputHashes => inputs.ReadHashes();
    public void ValidateInputsUnchanged() => inputs.Validate();

    private static JsonElement Value(SnapshotEnvelope snapshot, string domain, string name) => NativeExecutionSourceSupport.Value(snapshot, domain, name);
    private static int Int(JsonElement value) { Require(value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out _), "Site approach integer is missing or mistyped."); return value.GetInt32(); }
}
