using System.Text.Json;
using System.Text.Json.Nodes;

namespace StardewAI.LiveTrainingLoop;

public sealed record CropTeacherSavedOwnerPrefix(string SourceDirectory, string SourceJournalDirectory,
    string SourceHeaderSha256, int Sequence, string AcceptedSha256, string ArchiveDirectory);

public sealed partial class CropTeacherManagedSeedOwner
{
    private sealed record SavedHistorySegment(CropTeacherSavedOwnerPrefix Prefix, string HeaderHash, bool IsRecovery);

    private string HistoryHeaderHash => savedHistory.Length > 0 ? savedHistory[0].HeaderHash : headerHash;

    private SavedHistorySegment? HistoricalSegment(int sequence) =>
        savedHistory.FirstOrDefault(segment => sequence <= segment.Prefix.Sequence);

    private static SavedHistorySegment[] ReadSavedHistory(CropTeacherManagedSeedOwnerOptions options, JsonObject header,
        Dictionary<string, string> hashes)
    {
        if (options.SavedPrefix is not { } requested)
            return [];
        var binding = options.NativeSaveBinding
            ?? throw new InvalidDataException("Saved-prefix recovery requires the original native save binding.");
        var history = new List<SavedHistorySegment>();
        var outputs = new List<string> { options.EvidenceDirectory, options.PortfolioJournalDirectory };
        var upperSequence = int.MaxValue;
        for (CropTeacherSavedOwnerPrefix? prefix = requested; prefix is not null;)
        {
            var source = prefix.SourceDirectory;
            var journal = prefix.SourceJournalDirectory;
            var archive = prefix.ArchiveDirectory;
            var paths = new[] { source, journal, archive };
            if (prefix.Sequence < 1 || prefix.Sequence >= upperSequence || paths.Any(path => !Path.IsPathFullyQualified(path)) ||
                outputs.Any(output => paths.Any(path => Within(output, path) || Within(path, output))))
                throw new InvalidDataException("Saved-prefix recovery requires separate source, archive and continuation outputs and earlier ancestor checkpoints.");
            RequireOutsideNativeSaveSlot(paths, binding);
            RequireDirectoryPath(source);
            RequireDirectoryPath(archive);
            var sourceHeaderPath = Path.Combine(source, "owner-intent.json");
            var sourceHeaderHash = HashFile(sourceHeaderPath);
            var retained = Read<JsonObject>(sourceHeaderPath);
            var parent = retained["saved_prefix"]?.Deserialize<CropTeacherSavedOwnerPrefix>(JsonOptions);
            var expected = (JsonObject)header.DeepClone();
            expected["portfolio_journal_directory"] = journal;
            if (parent is not null)
            {
                expected["saved_prefix"] = JsonSerializer.SerializeToNode(parent, JsonOptions);
                expected["saved_prefix_output_directory"] = source;
            }
            if (!string.Equals(sourceHeaderHash, prefix.SourceHeaderSha256, StringComparison.OrdinalIgnoreCase) ||
                !JsonNode.DeepEquals(expected, retained) || HashFile(sourceHeaderPath) != sourceHeaderHash)
                throw new InvalidDataException("Saved-prefix recovery changed the original owner identity.");
            hashes[sourceHeaderPath] = sourceHeaderHash;
            history.Add(new(prefix, sourceHeaderHash, parent is not null));
            outputs.AddRange([source, journal]);
            upperSequence = prefix.Sequence;
            prefix = parent;
        }
        header["saved_prefix"] = JsonSerializer.SerializeToNode(requested, JsonOptions);
        header["saved_prefix_output_directory"] = options.EvidenceDirectory;
        history.Reverse();
        return history.ToArray();
    }

    private void ValidateSavedPrefixHead(CropTeacherSavedOwnerPrefix prefix)
    {
        if (Pending is not null || lastAcceptedOperation?.Sequence != prefix.Sequence || !lastAcceptedOperation.IsOvernight ||
            !string.Equals(lastAcceptedHash, prefix.AcceptedSha256, StringComparison.OrdinalIgnoreCase) ||
            Read<JsonObject>(lastAcceptedPath!)["native_save_boundary_verified"]?.GetValue<bool>() != true)
            throw new InvalidDataException("Saved-prefix recovery requires a verified original overnight acceptance at its exact cutoff.");
        var boundaryPath = AcceptedNativeSaveBoundaryPath();
        var expected = Read<JsonObject>(boundaryPath)["after_save"]?.Deserialize<SaveDirectoryFingerprint>(JsonOptions)
            ?? throw new InvalidDataException("Saved-prefix recovery lacks its original save fingerprint.");
        var archive = NativeSaveBoundaryVerifier.Capture(prefix.ArchiveDirectory, options.NativeSaveBinding!.SaveSlot);
        if (archive with { SlotPath = expected.SlotPath } != expected)
            throw new InvalidDataException("Saved-prefix recovery archive differs from the accepted native save.");
        fixedHashes[lastAcceptedPath!] = lastAcceptedHash;
        fixedHashes[boundaryPath] = HashFile(boundaryPath);
    }

    private string OperationJournalDirectory(int sequence) =>
        HistoricalSegment(sequence)?.Prefix.SourceJournalDirectory ?? options.PortfolioJournalDirectory;

    private CropTeacherManagedSeedOwnerOptions OperationRecoveryOptions(CropTeacherManagedSeedOperation operation) =>
        options with { PortfolioJournalDirectory = OperationJournalDirectory(operation.Sequence) };
}
