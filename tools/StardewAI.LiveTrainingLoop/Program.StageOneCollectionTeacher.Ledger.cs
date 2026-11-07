using StardewAI.Contracts.Strategy;
using StardewAI.Contracts.Training;
using StardewAI.LiveTrainingLoop;

static partial class Program
{
    private static async Task<(string? Path, StrategyCommitmentLedger? Ledger)> ReadStageOneTeacherLedgerAsync(
        HttpClient http, LiveTrainingOptions options, string stateHash, AvailabilityAwarePolicyPredictionEnvelope ranking)
    {
        if (!ranking.RankedEventCandidates.Any(candidate => candidate.OptionId == "museum.donate_items"))
            return (null, null);
        var evidence = await StrategyLedgerEvidenceStore.ReadAsync(http, options.BackendUrl,
            options.SnapshotDir, stateHash, JsonOptions, "Stage-one donation");
        return (evidence.Path, evidence.Ledger);
    }
}
