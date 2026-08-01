namespace StardewAI.Core.Tests;

public sealed class LiveSnapshotSchemaWorkflowSourceGuardTests
{
    [Fact]
    public void ValidatorChecksEveryRegisteredRequiredStateFactor()
    {
        var source = RepositoryFile(
            "tools",
            "StardewAI.KnowledgeCompiler",
            "Program.cs");
        var joiner = RepositoryFile(
            "tools",
            "StardewAI.KnowledgeCompiler",
            "SnapshotSchemaJoiner.cs");

        Assert.Contains("validate-snapshot-schema-only", source, StringComparison.Ordinal);
        Assert.Contains("SelectMany(row => row.RequiredStateFactors)", source, StringComparison.Ordinal);
        Assert.Contains("SnapshotCoverageBlocksTraining", source, StringComparison.Ordinal);
        Assert.Contains("snapshot-schema-validation.json", source, StringComparison.Ordinal);
        Assert.Contains("update-current-snapshot-lock", source, StringComparison.Ordinal);
        Assert.Contains("JsonNode.Parse", source, StringComparison.Ordinal);
        Assert.Contains("encoderShouldEmitUTF8Identifier: false", source, StringComparison.Ordinal);
        Assert.Contains("payload[0] == 0xEF", joiner, StringComparison.Ordinal);
        Assert.Contains("payload.AsMemory(3)", joiner, StringComparison.Ordinal);
    }

    [Fact]
    public void InstallerValidatesBeforeAtomicallyUpdatingExternalPointer()
    {
        var installer = RepositoryFile("scripts", "Install-LiveSnapshotSchema.ps1");
        var reconciliation = RepositoryFile("scripts", "Update-ActionReconciliation.ps1");

        Assert.Contains("--validate-snapshot-schema-only", installer, StringComparison.Ordinal);
        Assert.Contains("blocking_count", installer, StringComparison.Ordinal);
        Assert.Contains("Get-FileHash", installer, StringComparison.Ordinal);
        Assert.Contains(".incoming", installer, StringComparison.Ordinal);
        Assert.Contains("Move-Item -LiteralPath $incoming -Destination $target -Force", installer, StringComparison.Ordinal);
        Assert.Contains("current_snapshot", installer, StringComparison.Ordinal);
        Assert.Contains("metadata_sha256", installer, StringComparison.Ordinal);
        Assert.Contains("knowledge-artifacts.lock.json", installer, StringComparison.Ordinal);
        Assert.Contains("Every replacement candidate is validated", installer, StringComparison.Ordinal);
        Assert.Contains("installedLock.current_snapshot.sha256", installer, StringComparison.Ordinal);
        Assert.Contains("snapshot-schema-validation-candidate", installer, StringComparison.Ordinal);
        Assert.Contains("Move-Item -LiteralPath $validationIncoming -Destination $validationTarget -Force", installer, StringComparison.Ordinal);
        Assert.True(
            installer.IndexOf("Knowledge artifact lock file is missing", StringComparison.Ordinal) <
            installer.IndexOf("Move-Item -LiteralPath $incoming -Destination $target -Force", StringComparison.Ordinal));
        Assert.Contains("current-live-full-snapshot.json", reconciliation, StringComparison.Ordinal);
        Assert.Contains("[string]$SnapshotPath", reconciliation, StringComparison.Ordinal);
        Assert.Contains("Get-FileHash -LiteralPath $snapshot", reconciliation, StringComparison.Ordinal);
        Assert.Contains("lock.current_snapshot.sha256", reconciliation, StringComparison.Ordinal);
        Assert.DoesNotContain("live-full-snapshot-20260719.json", reconciliation, StringComparison.Ordinal);
    }

    private static string RepositoryFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null &&
            !File.Exists(Path.Combine(directory.FullName, "StardewValleyAICompanion.sln")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return File.ReadAllText(
            Path.Combine(new[] { directory!.FullName }.Concat(parts).ToArray()));
    }
}
