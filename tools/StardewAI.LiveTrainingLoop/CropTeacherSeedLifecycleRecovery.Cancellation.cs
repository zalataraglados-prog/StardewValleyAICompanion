using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Strategy;
using StardewAI.GoalConditionedBootstrap;
using Artifacts = StardewAI.LiveTrainingLoop.CropSeedRuntimeArtifacts;

namespace StardewAI.LiveTrainingLoop;

public static partial class CropTeacherSeedLifecycleRecovery
{
    private static async Task<CropTeacherSeedRelease> ReadCancellationAsync(CropTeacherSeedLifecycleRuntimeOptions options,
        CropTeacherSeedLifecycleSupport scope, CropTeacherSeedCancellation cancellation, StrategyCommitmentLedger closed, Artifacts artifacts)
    {
        var raw = JsonSerializer.Serialize(cancellation.Request, JsonOptions);
        var hash = PortfolioMutationRequestJournal.Hash(Encoding.UTF8.GetBytes(raw));
        var requestPath = Path.Combine(options.EvidenceDirectory, "crop-cancel-request.json");
        var journalPath = Path.Combine(options.EvidenceDirectory, "crop-cancel-journal.json");
        var endpoint = options.BackendUrl + "/api/v1/strategy/commitments/crops/" + Uri.EscapeDataString(scope.CommitmentId) + "/cancel";
        var journal = CropTeacherSeedLifecycleRuntime.CreateCancellationJournal(endpoint, requestPath, hash, scope.CommitmentId, options.RunId);
        artifacts.Pin(requestPath);
        artifacts.Pin(journalPath);
        _ = await PortfolioMutationRequestJournal.ReadAsync(journalPath, requestPath, journal, hash);
        var replyPath = Path.Combine(options.EvidenceDirectory, "crop-cancel-reply.json");
        var release = File.Exists(replyPath)
            ? cancellation.VerifyReply(artifacts.Read<JsonObject>(replyPath).Deserialize<StrategyCommitmentMutationResult>(JsonOptions)
                ?? throw new InvalidDataException("Recovered crop cancellation reply is empty."))
            : cancellation.ResumeFromReadback(closed);
        CropTeacherSeedRuntimeTransaction.RequireLedger(release.ClosedLedger, closed, "original cancellation closure");
        return release;
    }
}
