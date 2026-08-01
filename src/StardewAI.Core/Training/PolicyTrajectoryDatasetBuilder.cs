using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training;

public sealed class PolicyTrajectoryDatasetBuilder
{
    private readonly PolicyTrajectoryDatasetValidator validator = new();
    private readonly PolicyTrajectoryReturnBackfiller returnBackfiller = new();

    public PolicyDatasetBuildResult Build(
        string inputPath,
        string outputRoot,
        string? horizonObservationPath = null,
        string? expectedKnowledgeDictionary = null)
    {
        var fullInputPath = Path.GetFullPath(inputPath);
        if (!File.Exists(fullInputPath))
            throw new FileNotFoundException("Policy trajectory input does not exist.", fullInputPath);

        var fullOutputRoot = Path.GetFullPath(outputRoot);
        Directory.CreateDirectory(fullOutputRoot);
        var inputLines = File.ReadAllLines(fullInputPath);
        var rejections = new List<PolicyDatasetRejection>();
        var parsed = ParseTrajectories(inputLines, rejections);
        var observations = ReadObservations(horizonObservationPath);
        EnsureUniqueGrandpaTerminalObservations(observations);
        var accepted = ResolveDuplicates(parsed, rejections, out var duplicateRows, out var conflictingRows)
            .OrderBy(row => row.Trajectory.Context.SaveId, StringComparer.Ordinal)
            .ThenBy(row => PolicyTrajectoryDatasetValidator.DateOrdinal(row.Trajectory.Context))
            .ThenBy(row => row.Trajectory.Context.Time)
            .ThenBy(row => row.Trajectory.TrajectoryId, StringComparer.Ordinal)
            .Select(row => row.Trajectory)
            .ToArray();
        if (accepted.Length == 0)
            throw new InvalidOperationException("Policy dataset contains no valid non-conflicting trajectories.");
        if (accepted.Select(VersionKey).Distinct(StringComparer.Ordinal).Count() != 1)
            throw new InvalidOperationException("Policy dataset mixes multiple version sets; build each immutable version separately.");
        if (!string.IsNullOrWhiteSpace(expectedKnowledgeDictionary) &&
            accepted.Any(row => !string.Equals(
                row.Versions.KnowledgeDictionary,
                expectedKnowledgeDictionary,
                StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                "Policy dataset knowledge dictionary does not match the expected immutable version '" +
                expectedKnowledgeDictionary + "'.");
        }

        returnBackfiller.Backfill(accepted, observations);
        foreach (var row in accepted)
            row.Context.DatasetPartition = PartitionFor(row.Context.SplitKey);

        var cleanedPath = Path.Combine(fullOutputRoot, "policy-trajectories.cleaned.jsonl");
        var trainPath = Path.Combine(fullOutputRoot, "policy-trajectories.train.jsonl");
        var validationPath = Path.Combine(fullOutputRoot, "policy-trajectories.validation.jsonl");
        var testPath = Path.Combine(fullOutputRoot, "policy-trajectories.test.jsonl");
        var rejectionPath = Path.Combine(fullOutputRoot, "policy-trajectories.rejections.jsonl");
        var manifestPath = Path.Combine(fullOutputRoot, "policy-dataset-manifest.json");

        WriteJsonl(cleanedPath, accepted);
        var partitionRows = new Dictionary<string, PolicyDecisionTrajectoryEnvelope[]>(StringComparer.Ordinal)
        {
            [PolicyDatasetPartitions.Train] = accepted.Where(row => row.Context.DatasetPartition == PolicyDatasetPartitions.Train).ToArray(),
            [PolicyDatasetPartitions.Validation] = accepted.Where(row => row.Context.DatasetPartition == PolicyDatasetPartitions.Validation).ToArray(),
            [PolicyDatasetPartitions.Test] = accepted.Where(row => row.Context.DatasetPartition == PolicyDatasetPartitions.Test).ToArray()
        };
        WriteJsonl(trainPath, partitionRows[PolicyDatasetPartitions.Train]);
        WriteJsonl(validationPath, partitionRows[PolicyDatasetPartitions.Validation]);
        WriteJsonl(testPath, partitionRows[PolicyDatasetPartitions.Test]);
        WriteJsonl(
            rejectionPath,
            rejections
                .OrderBy(rejection => rejection.LineNumber)
                .ThenBy(rejection => rejection.Reason, StringComparer.Ordinal)
                .ToArray());

        var manifest = new PolicyDatasetManifest
        {
            Input = Digest(fullInputPath, inputLines.Count(line => !string.IsNullOrWhiteSpace(line))),
            HorizonObservations = string.IsNullOrWhiteSpace(horizonObservationPath)
                ? null
                : Digest(Path.GetFullPath(horizonObservationPath), observations.Count),
            Cleaned = Digest(cleanedPath, accepted.Length),
            Counts = new PolicyDatasetCounts
            {
                InputLines = inputLines.Length,
                AcceptedRows = accepted.Length,
                RejectedRows = rejections.Count,
                DuplicateRows = duplicateRows,
                ConflictingDuplicateRows = conflictingRows
            },
            Partitions = new[]
            {
                PartitionDigest(PolicyDatasetPartitions.Train, trainPath, partitionRows[PolicyDatasetPartitions.Train]),
                PartitionDigest(PolicyDatasetPartitions.Validation, validationPath, partitionRows[PolicyDatasetPartitions.Validation]),
                PartitionDigest(PolicyDatasetPartitions.Test, testPath, partitionRows[PolicyDatasetPartitions.Test])
            },
            Rejections = rejections
                .GroupBy(rejection => rejection.Reason, StringComparer.Ordinal)
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => new PolicyDatasetRejectionCount { Reason = group.Key, Count = group.Count() })
                .ToArray(),
            VersionSets = accepted
                .GroupBy(VersionKey, StringComparer.Ordinal)
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => VersionSet(group.First().Versions, group.Count()))
                .ToArray(),
            Returns = ReturnCoverage(accepted)
        };
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, ManifestJsonOptions), Encoding.UTF8);
        return new PolicyDatasetBuildResult
        {
            ManifestPath = manifestPath,
            RejectionsPath = rejectionPath,
            Manifest = manifest
        };
    }

    public static string PartitionFor(string splitKey)
    {
        if (string.IsNullOrWhiteSpace(splitKey))
            throw new ArgumentException("Split key is required.", nameof(splitKey));
        var bytes = Encoding.UTF8.GetBytes(splitKey);
        byte[] hash;
        using (var sha = SHA256.Create())
            hash = sha.ComputeHash(bytes);
        var bucket = (int)((((uint)hash[0] << 24) |
            ((uint)hash[1] << 16) |
            ((uint)hash[2] << 8) |
            hash[3]) % 100);
        return bucket < 80
            ? PolicyDatasetPartitions.Train
            : bucket < 90
                ? PolicyDatasetPartitions.Validation
                : PolicyDatasetPartitions.Test;
    }

    private List<ParsedTrajectory> ParseTrajectories(
        IReadOnlyList<string> lines,
        ICollection<PolicyDatasetRejection> rejections)
    {
        var parsed = new List<ParsedTrajectory>();
        for (var index = 0; index < lines.Count; index++)
        {
            var lineNumber = index + 1;
            var line = lines[index];
            if (string.IsNullOrWhiteSpace(line))
            {
                Reject(rejections, lineNumber, string.Empty, "blank_line", string.Empty);
                continue;
            }

            try
            {
                var row = JsonSerializer.Deserialize<PolicyDecisionTrajectoryEnvelope>(line, JsonOptions);
                if (row is null)
                {
                    Reject(rejections, lineNumber, string.Empty, "json_null", string.Empty);
                    continue;
                }

                var reason = validator.Validate(row);
                if (reason is not null)
                {
                    Reject(rejections, lineNumber, row.TrajectoryId, reason, string.Empty);
                    continue;
                }

                parsed.Add(new ParsedTrajectory(
                    lineNumber,
                    row,
                    DecisionKey(row),
                    DecisionFingerprint(row)));
            }
            catch (JsonException ex)
            {
                Reject(rejections, lineNumber, string.Empty, "invalid_json", ex.GetType().Name);
            }
        }
        return parsed;
    }

    private static IReadOnlyList<ParsedTrajectory> ResolveDuplicates(
        IReadOnlyList<ParsedTrajectory> rows,
        ICollection<PolicyDatasetRejection> rejections,
        out int duplicateRows,
        out int conflictingRows)
    {
        duplicateRows = 0;
        conflictingRows = 0;
        var accepted = new List<ParsedTrajectory>();
        foreach (var group in rows.GroupBy(row => row.DecisionKey, StringComparer.Ordinal))
        {
            var members = group
                .OrderBy(row => row.Trajectory.TrajectoryId, StringComparer.Ordinal)
                .ThenBy(row => row.LineNumber)
                .ToArray();
            if (members.Length == 1)
            {
                accepted.Add(members[0]);
                continue;
            }

            if (members.Select(row => row.DecisionFingerprint).Distinct(StringComparer.Ordinal).Count() != 1)
            {
                conflictingRows += members.Length;
                foreach (var member in members)
                    Reject(rejections, member.LineNumber, member.Trajectory.TrajectoryId, "conflicting_duplicate_decision", group.Key);
                continue;
            }

            accepted.Add(members[0]);
            duplicateRows += members.Length - 1;
            foreach (var duplicate in members.Skip(1))
                Reject(rejections, duplicate.LineNumber, duplicate.Trajectory.TrajectoryId, "duplicate_decision", group.Key);
        }
        return accepted;
    }

    private static IReadOnlyList<PolicyHorizonObservationEnvelope> ReadObservations(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return Array.Empty<PolicyHorizonObservationEnvelope>();
        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException("Horizon observation input does not exist.", fullPath);

        var result = new List<PolicyHorizonObservationEnvelope>();
        var observationIds = new HashSet<string>(StringComparer.Ordinal);
        var lines = File.ReadAllLines(fullPath);
        for (var index = 0; index < lines.Length; index++)
        {
            if (string.IsNullOrWhiteSpace(lines[index]))
                throw new InvalidOperationException("Horizon observation contains a blank line at " + (index + 1) + ".");
            PolicyHorizonObservationEnvelope observation;
            try
            {
                observation = JsonSerializer.Deserialize<PolicyHorizonObservationEnvelope>(lines[index], JsonOptions)
                    ?? throw new InvalidOperationException("Horizon observation is null at " + (index + 1) + ".");
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException("Horizon observation JSON is invalid at " + (index + 1) + ".", ex);
            }
            ValidateObservation(observation, index + 1);
            if (!observationIds.Add(observation.ObservationId))
                throw new InvalidOperationException("Horizon observation ID is duplicated at line " + (index + 1) + ".");
            result.Add(observation);
        }
        return result;
    }

    private static void ValidateObservation(PolicyHorizonObservationEnvelope observation, int lineNumber)
    {
        var validHorizon = observation.Horizon is
            PolicyHorizonKinds.Day or
            PolicyHorizonKinds.Season or
            PolicyHorizonKinds.Year or
            PolicyHorizonKinds.Grandpa21;
        if (!string.Equals(observation.SchemaVersion, "policy_horizon_observation.v1", StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(observation.ObservationId) ||
            string.IsNullOrWhiteSpace(observation.SaveId) ||
            observation.Year < 1 ||
            PolicyTrajectoryDatasetValidator.SeasonOrdinal(observation.Season) < 0 ||
            observation.Day is < 1 or > 28 ||
            !ValidGameTime(observation.Time) ||
            !validHorizon ||
            !observation.Closed ||
            string.IsNullOrWhiteSpace(observation.SourceStateHash) ||
            string.IsNullOrWhiteSpace(observation.EvidenceKind) ||
            string.IsNullOrWhiteSpace(observation.EvidencePath))
            throw new InvalidOperationException("Horizon observation is incomplete at line " + lineNumber + ".");
        if (string.Equals(observation.Horizon, PolicyHorizonKinds.Grandpa21, StringComparison.Ordinal) &&
            (observation.Year < 3 ||
             !observation.GrandpaScore.HasValue ||
             observation.GrandpaScore.Value is < 0 or > 21))
            throw new InvalidOperationException("Grandpa horizon observation has an invalid score at line " + lineNumber + ".");
        if (!string.Equals(observation.Horizon, PolicyHorizonKinds.Grandpa21, StringComparison.Ordinal) &&
            observation.GrandpaScore.HasValue)
            throw new InvalidOperationException("Non-Grandpa horizon observation carries a Grandpa score at line " + lineNumber + ".");
    }

    private static void EnsureUniqueGrandpaTerminalObservations(
        IReadOnlyList<PolicyHorizonObservationEnvelope> observations)
    {
        var duplicate = observations
            .Where(observation => string.Equals(
                observation.Horizon,
                PolicyHorizonKinds.Grandpa21,
                StringComparison.Ordinal))
            .GroupBy(observation => observation.SaveId, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new InvalidOperationException(
                "Policy dataset has multiple Grandpa terminal observations for save '" +
                duplicate.Key + "'.");
        }
    }

    private static string DecisionKey(PolicyDecisionTrajectoryEnvelope row) =>
        row.Context.SaveId + "\n" + row.SourceStateHash + "\n" + row.Selection.CandidateId;

    private static string DecisionFingerprint(PolicyDecisionTrajectoryEnvelope row) => HashText(
        JsonSerializer.Serialize(new
        {
            row.SourceStateHash,
            row.Context,
            row.Versions,
            row.Candidates,
            row.Selection,
            outcome = new
            {
                row.Outcome.QueueId,
                row.Outcome.PrimitiveOptionId,
                row.Outcome.Status,
                row.Outcome.Success,
                row.Outcome.ActualTicks,
                row.Outcome.StateHashChanged,
                row.Outcome.AfterSnapshotFresh,
                row.Outcome.FailureAttribution,
                row.Outcome.BlockReasons,
                row.Outcome.ChangedFacts
            },
            immediate_return = row.Returns.Immediate
        }, JsonOptions));

    private static string VersionKey(PolicyDecisionTrajectoryEnvelope row) =>
        row.Versions.FeatureSchema + "\n" +
        row.Versions.CandidateVocabulary + "\n" +
        row.Versions.CapabilityRegistry + "\n" +
        row.Versions.KnowledgeDictionary + "\n" +
        row.Versions.Compiler + "\n" +
        row.Versions.Executor;

    private static bool ValidGameTime(int time)
    {
        var hour = time / 100;
        var minute = time % 100;
        return hour is >= 6 and <= 26 && minute is >= 0 and < 60;
    }

    private static PolicyDatasetVersionSet VersionSet(PolicyTrajectoryVersions versions, int count) => new()
    {
        FeatureSchema = versions.FeatureSchema,
        CandidateVocabulary = versions.CandidateVocabulary,
        CapabilityRegistry = versions.CapabilityRegistry,
        KnowledgeDictionary = versions.KnowledgeDictionary,
        Compiler = versions.Compiler,
        Executor = versions.Executor,
        RowCount = count
    };

    private static PolicyDatasetPartitionDigest PartitionDigest(
        string partition,
        string path,
        IReadOnlyList<PolicyDecisionTrajectoryEnvelope> rows)
    {
        var digest = Digest(path, rows.Count);
        return new PolicyDatasetPartitionDigest
        {
            Partition = partition,
            Path = digest.Path,
            Sha256 = digest.Sha256,
            Bytes = digest.Bytes,
            Rows = digest.Rows,
            SplitKeyCount = rows.Select(row => row.Context.SplitKey).Distinct(StringComparer.Ordinal).Count()
        };
    }

    private static PolicyDatasetReturnCoverage ReturnCoverage(IReadOnlyList<PolicyDecisionTrajectoryEnvelope> rows) => new()
    {
        DayComplete = rows.Count(row => row.Returns.Day.HasValue),
        SeasonComplete = rows.Count(row => row.Returns.Season.HasValue),
        YearComplete = rows.Count(row => row.Returns.Year.HasValue),
        Grandpa21Complete = rows.Count(row => row.Returns.Grandpa21.HasValue),
        FullyComplete = rows.Count(row => string.Equals(row.Returns.LongHorizonStatus, "complete", StringComparison.Ordinal)),
        PartialObserved = rows.Count(row => string.Equals(row.Returns.LongHorizonStatus, "partial_observed", StringComparison.Ordinal)),
        Pending = rows.Count(row => string.Equals(row.Returns.LongHorizonStatus, "pending", StringComparison.Ordinal))
    };

    private static PolicyDatasetFileDigest Digest(string path, int rows)
    {
        var fullPath = Path.GetFullPath(path);
        var info = new FileInfo(fullPath);
        return new PolicyDatasetFileDigest
        {
            Path = fullPath,
            Sha256 = HashFile(fullPath),
            Bytes = info.Length,
            Rows = rows
        };
    }

    private static string HashFile(string path)
    {
        using var stream = File.OpenRead(path);
        using var sha = SHA256.Create();
        return ToHex(sha.ComputeHash(stream));
    }

    private static string HashText(string value)
    {
        using var sha = SHA256.Create();
        return ToHex(sha.ComputeHash(Encoding.UTF8.GetBytes(value)));
    }

    private static string ToHex(byte[] bytes) => string.Concat(bytes.Select(value => value.ToString("x2")));

    private static void WriteJsonl<T>(string path, IReadOnlyList<T> rows)
    {
        var payload = rows.Count == 0
            ? string.Empty
            : string.Join("\n", rows.Select(row => JsonSerializer.Serialize(row, JsonOptions))) + "\n";
        File.WriteAllText(path, payload, new UTF8Encoding(false));
    }

    private static void Reject(
        ICollection<PolicyDatasetRejection> rejections,
        int lineNumber,
        string trajectoryId,
        string reason,
        string detail) => rejections.Add(new PolicyDatasetRejection
    {
        LineNumber = lineNumber,
        TrajectoryId = trajectoryId,
        Reason = reason,
        Detail = detail
    });

    private sealed record ParsedTrajectory(
        int LineNumber,
        PolicyDecisionTrajectoryEnvelope Trajectory,
        string DecisionKey,
        string DecisionFingerprint);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly JsonSerializerOptions ManifestJsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };
}
