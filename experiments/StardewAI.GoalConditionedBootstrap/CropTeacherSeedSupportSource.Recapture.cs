using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

internal sealed partial class CropTeacherSeedSupportSource
{
    private StrategyCommitmentLedger ReadSupplyLedger(string? priorVerifiedSnapshotPath)
    {
        if (ledger.SourceStateHash == before.StateHash) return ledger;
        Require(priorVerifiedSnapshotPath is not null,
            "Crop seed support requires its prior verified snapshot for runtime recapture.");
        var prior = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(priorVerifiedSnapshotPath!, "prior crop seed source");
        MoneyRevenueCropSeedAcquisitionProofBuilder.ValidateSnapshot(prior, "prior support source");
        Require(prior.StateHash == ledger.SourceStateHash &&
                SameIdleSource(prior, before),
            "Crop seed support recapture is not the prior verified runtime boundary.");
        CropTeacherOwnedSeedSupply.Read(prior, ledger, new[] { CommitmentId });
        // Preview the original marker on a clone; only its real HTTP reply can establish the new source.
        var preview = new ReservationPortfolioLedgerService().Commit(ledger, before, marker, ledger.UpdatedAt);
        Require(preview.Accepted && preview.Ledger is not null,
            "Crop seed support recapture marker preflight was rejected: " + string.Join(",", preview.Errors));
        return preview.Ledger!;
    }

    private static bool SameIdleSource(SnapshotEnvelope prior, SnapshotEnvelope current)
    {
        if (prior.SaveId.Value != current.SaveId.Value || prior.PlayerId.Value != current.PlayerId.Value ||
            prior.GameVersion != current.GameVersion)
            return false;
        var first = RuntimeSnapshotBoundaryNormalization.TryCreateRuntimeIdentityBoundary(prior);
        var next = RuntimeSnapshotBoundaryNormalization.TryCreateRuntimeIdentityBoundary(current);
        if (first is null || next is null) return false;
        int start, end;
        try
        {
            start = MoneyRevenueCropSeedAcquisitionProofBuilder.ReadInt(prior, "time", "time");
            end = MoneyRevenueCropSeedAcquisitionProofBuilder.ReadInt(current, "time", "time");
        }
        catch (InvalidDataException) { return JsonNode.DeepEquals(first, next); }
        if (start != end)
        {
            if (end < start || current.GameTick < prior.GameTick ||
                !FieldEnvelopeValidator.IsReadableStatus(prior.InGameTime.Status) ||
                !FieldEnvelopeValidator.IsReadableStatus(current.InGameTime.Status) ||
                prior.InGameTime.Value != start || current.InGameTime.Value != end)
                return false;
            // Comparison views only; date and every other state field still match. No snapshot/hash is rewritten.
            first["time"]!["time"]!["value"] = "__same_day_idle_clock__";
            next["time"]!["time"]!["value"] = "__same_day_idle_clock__";
        }
        return JsonNode.DeepEquals(first, next);
    }
}
