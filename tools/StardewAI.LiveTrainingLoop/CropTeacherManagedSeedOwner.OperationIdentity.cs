using System.Text.Json.Nodes;
using StardewAI.Contracts.Strategy;

namespace StardewAI.LiveTrainingLoop;

public sealed partial class CropTeacherManagedSeedOwner
{
    private static string SupportStepPrefix(CropTeacherManagedSeedOwnerOptions options,
        StrategyCommitmentLedger initialLedger, string originalPlanHash)
    {
        var legacy = options.RunId + ":owned-seed:";
        var scoped = legacy + "plan:" + originalPlanHash + ":";
        var hasEarlierOwner = initialLedger.History.Any(row =>
            row.CommitmentId.StartsWith(legacy, StringComparison.Ordinal) ||
            row.SourceDecisionId.StartsWith(legacy, StringComparison.Ordinal));
        var first = Path.Combine(options.EvidenceDirectory, "operation-000001-intent.json");
        if (File.Exists(first))
        {
            // Existing records define their retained format; all original index,
            // source freshness, intent and receipt checks still replay below.
            var id = Text(Read<JsonObject>(first), "support_step_id");
            if (id == legacy + "000001") return legacy;
            if (!hasEarlierOwner || id != scoped + "000001")
                throw new InvalidDataException("Managed crop operation identity differs from its original run/plan.");
        }
        // Both the initial history and full plan SHA are already immutable owner
        // inputs. Reopening an empty or scoped owner therefore selects the same ID.
        return hasEarlierOwner ? scoped : legacy;
    }
}
