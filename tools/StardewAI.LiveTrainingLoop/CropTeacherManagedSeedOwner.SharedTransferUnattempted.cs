using System.Text;
using System.Text.Json.Nodes;

namespace StardewAI.LiveTrainingLoop;

public sealed partial class CropTeacherManagedSeedOwner
{
    private Func<string, string, Task> UnattemptedSharedPreparationWriter(CropTeacherManagedSeedOperation operation, string root)
    {
        RequireDirectoryPath(root);
        if (new[] { StoredPath(operation.Record, "native_receipt_path"), StoredPath(operation.Record, "native_after_snapshot_path") }
            .Any(path => File.Exists(path) || Directory.Exists(path)))
            throw new InvalidOperationException("Unattempted preparation cannot continue after an unclaimed native output.");
        var intentPath = Path.Combine(root, "support-intent.json");
        var summaryPath = Path.Combine(root, "preparation-summary.json");
        if (Directory.Exists(root))
            foreach (var path in Directory.EnumerateFileSystemEntries(root))
            {
                var name = Path.GetFileName(path);
                if (name == "teacher-ledgers" && Directory.Exists(path)) { RequireDirectoryPath(path); continue; }
                if (SamePath(path, intentPath))
                {
                    if (HashFile(path) != Text(operation.Record, "original_intent_sha256"))
                        throw new InvalidDataException("Unattempted preparation lost its original admitted intent.");
                    continue;
                }
                const string prefix = "preparation-before-commit-";
                var retrySummary = name.StartsWith(prefix, StringComparison.Ordinal) && name.EndsWith(".json", StringComparison.Ordinal) &&
                    Guid.TryParseExact(name[prefix.Length..^5], "N", out _);
                if (!SamePath(path, summaryPath) && !retrySummary)
                    throw new IOException("Unattempted preparation has an unexpected original artifact; retain it for explicit recovery.");
                var summary = Read<JsonObject>(path);
                if (CreateSharedTransferPreparationIntent(operation.Record).Any(field => !JsonNode.DeepEquals(field.Value, summary[field.Key])))
                    throw new InvalidDataException("Unattempted preparation summary belongs to another admission.");
            }
        return (path, json) =>
        {
            if (SamePath(path, intentPath) && File.Exists(path))
            {
                if (HashFile(path) != PortfolioMutationRequestJournal.Hash(Encoding.UTF8.GetBytes(json)))
                    throw new InvalidDataException("Unattempted preparation changed its retained intent.");
                return Task.CompletedTask;
            }
            // Keep the earlier failed summary; this continues the first RPC, never overwrites an old attempt.
            var target = SamePath(path, summaryPath) && File.Exists(path)
                ? Path.Combine(root, "preparation-before-commit-" + Guid.NewGuid().ToString("N") + ".json") : path;
            return write(target, json);
        };
    }
}
