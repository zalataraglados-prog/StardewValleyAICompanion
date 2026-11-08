using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Core.Infrastructure;

namespace StardewAI.LiveTrainingLoop;

public sealed partial class CropTeacherManagedSeedOwner
{
    private static StringComparer PathComparer => OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
    private static bool SamePath(string first, string second) => PathComparer.Equals(Path.GetFullPath(first), Path.GetFullPath(second));
    private static bool Within(string root, string path) => SamePath(root, path) || Path.GetFullPath(path).StartsWith(
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)) + Path.DirectorySeparatorChar,
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    private static void Regular(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("Managed crop owner artifacts must not be reparse points.");
    }
    private static void RequireDirectoryPath(string path)
    {
        for (string? current = path; current is not null; current = Path.GetDirectoryName(current))
        {
            if (File.Exists(current)) throw new IOException("Managed crop output has a file in its directory path.");
            if (Directory.Exists(current)) Regular(current);
        }
    }
    private bool ReservedRuntimePath(string path)
    {
        var first = Path.GetRelativePath(options.EvidenceDirectory, path).Split(Path.DirectorySeparatorChar)[0];
        return first.StartsWith("operation-", OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) ||
            PathComparer.Equals(first, "owner-intent.json") || PathComparer.Equals(first, "owner.lock");
    }
    private static string HashFile(string path) { Regular(path); return PortfolioMutationRequestJournal.HashFile(path); }
    private static T Read<T>(string path)
    {
        Regular(path);
        return JsonSerializer.Deserialize<T>(ContentAddressedJsonArtifactReader.ReadAllText(path), JsonOptions)
            ?? throw new InvalidDataException("Managed crop owner artifact is empty: " + path);
    }
    private string RecordPath(int sequence, string kind) => Path.Combine(options.EvidenceDirectory, $"operation-{sequence:D6}-{kind}.json");
    private string StepId(int sequence) => options.RunId + ":owned-seed:" + sequence.ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
    private void ValidateState()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (faulted) throw new InvalidOperationException("Managed crop owner write is incomplete; reopen retained state before further work.");
        foreach (var (path, hash) in fixedHashes.Concat(frontierHashes))
            if (HashFile(path) != hash) throw new InvalidDataException("Managed crop owner retained artifact changed: " + path);
        liveHeadGuard?.Invoke();
    }
    private async Task SaveAsync(string path, JsonObject value)
    {
        try { await write(path, value.ToJsonString(JsonOptions)).ConfigureAwait(false); }
        catch { faulted = true; throw; }
    }
    private void TrackFrontier(string path) => frontierHashes[Path.GetFullPath(path)] = HashFile(path);
    private static string Text(JsonObject node, string name) => node[name]?.GetValue<string>()
        ?? throw new InvalidDataException("Managed crop owner artifact lacks " + name + ".");
    private static string StoredPath(JsonObject node, string name)
    {
        var path = Text(node, name);
        if (!Path.IsPathFullyQualified(path))
            throw new InvalidDataException("Managed crop owner retained path is not absolute: " + name);
        return path;
    }
    private static int Sequence(JsonObject node)
    {
        if (node["sequence"] is not JsonValue value || !value.TryGetValue<int>(out var sequence) || sequence < 1)
            throw new InvalidDataException("Managed crop owner phase sequence is not a positive integer.");
        return sequence;
    }
    private void RequireOperation(CropTeacherManagedSeedOperation operation)
    {
        ValidateState();
        if (!ReferenceEquals(operation.Owner, this) || !ReferenceEquals(Pending, operation))
            throw new InvalidOperationException("Managed crop operation is not this owner's unique pending operation.");
        operation.RuntimeArtifactGuard?.Invoke();
        if (operation.IsSupportingRoute) ValidateSupportingRoute(operation);
        else if (operation.IsLifecycle) ValidateLifecycle(operation);
        else
        {
            operation.Scopes.ValidateStep(operation.ReadStep());
            ValidateSeedIntent(operation);
        }
    }
    private CropTeacherManagedSeedScopes OpenScopes(JsonObject record)
    {
        var prior = record["prior_verified_snapshot_path"] is null ? null : StoredPath(record, "prior_verified_snapshot_path");
        if (!SamePath(StoredPath(record, "before_ledger_path"), headLedgerPath) || prior is not null && !SamePath(prior, headSnapshotPath))
            throw new InvalidDataException("Managed crop operation does not continue its accepted source boundary.");
        var step = record["step"]?.Deserialize<CropTeacherRunStep>(JsonOptions)
            ?? throw new InvalidDataException("Managed seed admission lacks its original step.");
        if (!plan.CoveredCommitmentIds.Contains(step.CommitmentId, StringComparer.Ordinal))
            throw new InvalidDataException("Managed crop operation commitment is outside its retained plan.");
        return CropTeacherManagedSeedScopes.Open(plan.RequestPath, StoredPath(record, "before_snapshot_path"), headLedgerPath,
            step, Text(record, "support_step_id"), prior);
    }
    private static IReadOnlyDictionary<string, string> Inputs(CropTeacherManagedSeedScopes scopes) =>
        scopes.Purchase?.InputHashes ?? scopes.Transfer?.InputHashes ?? scopes.Planting!.InputHashes;
    private static IReadOnlyDictionary<string, string> OperationInputs(CropTeacherManagedSeedOperation operation) =>
        operation.RouteStart is not null ? operation.RouteStart.InputHashes : operation.IsSupportingRoute ? operation.SupportingRoute!.InputHashes :
        operation.IsLifecycle ? operation.Lifecycle!.InputHashes : Inputs(operation.Scopes);
}
