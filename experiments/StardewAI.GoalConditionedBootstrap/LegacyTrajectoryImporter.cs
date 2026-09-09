using System.Security.Cryptography;
using System.Text.Json;
using StardewAI.Contracts.Training;

namespace StardewAI.GoalConditionedBootstrap;

public static class LegacyTrajectoryImporter
{
    public static GoalConditionedDemonstration[] Import(
        string path,
        GoalKnowledge knowledge)
    {
        var fullPath = Path.GetFullPath(path);
        var sourceHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(fullPath))).ToLowerInvariant();
        var rows = File.ReadLines(fullPath)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => JsonSerializer.Deserialize<PolicyDecisionTrajectoryEnvelope>(line, JsonOptions)
                ?? throw new InvalidDataException("Legacy policy trajectory row is null."))
            .ToArray();

        return rows.Select(row => new GoalConditionedDemonstration
        {
            DemonstrationId = "legacy:" + row.TrajectoryId,
            Source = new DemonstrationSource
            {
                Kind = DemonstrationSourceKinds.LegacyAiRollout,
                SessionId = row.RunId,
                SourcePaths = new[] { fullPath },
                SourceSha256 = new[] { sourceHash }
            },
            Goal = new DemonstrationGoal
            {
                GoalId = knowledge.GoalId,
                TargetScore = knowledge.TargetScore
            },
            Context = new DemonstrationContext
            {
                SaveId = row.Context.SaveId,
                Year = row.Context.Year,
                Season = row.Context.Season,
                Day = row.Context.Day,
                StartTime = row.Context.Time,
                EndTime = row.Context.Time,
                StartStateHash = row.SourceStateHash,
                EndStateHash = row.Outcome.StateHashChanged ? "legacy_after_hash_not_recorded_here" : row.SourceStateHash
            },
            StateFeatures = row.StateFeatures,
            Segments = new[]
            {
                new DemonstrationSegment
                {
                    Sequence = 1,
                    BundleId = "legacy-single-selection",
                    MethodId = "legacy.untrusted_selection",
                    OptionId = row.Selection.OptionId,
                    CandidateId = row.Selection.CandidateId,
                    CandidateKind = row.Candidates.FirstOrDefault(candidate => candidate.Selected)?.Kind ?? string.Empty,
                    StartStateHash = row.SourceStateHash,
                    EndStateHash = "legacy_after_hash_not_recorded_here",
                    Verified = row.Outcome.Success && row.Outcome.AfterSnapshotFresh,
                    SemanticConfidence = 1,
                    ObservedEffects = JsonDocument.Parse("{}").RootElement.Clone()
                }
            },
            Outcome = new DemonstrationOutcome
            {
                Status = row.Outcome.Success ? "legacy_applied" : "legacy_failed",
                AllSegmentsVerified = row.Outcome.Success && row.Outcome.AfterSnapshotFresh,
                DayBoundaryObserved = false,
                TerminalGoalComplete = row.Returns.Grandpa21 == 1
            },
            Audit = new DemonstrationAudit
            {
                ExpertAdmitted = false,
                AdmissionReasons = new[] { "legacy_ai_rollout_must_not_supervise_bootstrap_policy" }
            }
        }).ToArray();
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}
