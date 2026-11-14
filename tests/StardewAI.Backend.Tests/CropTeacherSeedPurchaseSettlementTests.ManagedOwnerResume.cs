using System.Text.Json.Nodes;
using StardewAI.Core.Infrastructure;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Fact]
    public async Task ManagedOwnerResumeReopensOriginalAcceptedHeadAndLedgerWithoutWritesOrReplay()
    {
        using var f = new ManagedResumeFixture();
        var transition = f.Source.Transition();
        string snapshotPath, ledgerPath;
        await using (var owner = await f.Install())
        {
            var step = CropTeacherRunPlanner.Evaluate(f.Inputs.Plan, f.Inputs.Request, f.Source.Before, resourceLedger: f.Source.Ledger);
            var operation = await owner.AdmitAsync(step, f.Source.SourcePath, f.Source.LedgerPath, f.Files.Native);
            var prepared = await f.Runtime.Prepare(transition, selectedScope: operation.Scopes.Purchase, runtimeDirectory: operation.RuntimeDirectory);
            await owner.PinPreparedAsync(operation, prepared);
            f.Runtime.ObserveAfter();
            await prepared.CompleteAsync(transition);
            await owner.AcceptLiveAsync(operation, prepared);
            snapshotPath = owner.CurrentSnapshotPath;
            ledgerPath = owner.CurrentLedgerPath;
        }
        Assert.NotEqual(f.Options.InitialLedgerPath, ledgerPath);
        var before = f.RetainedBytes();
        var commits = f.Runtime.Handler.Commits;
        var settlements = f.Runtime.Handler.Settlements;
        await using var reopened = await f.Resume(backend: "http://TEST/");
        Assert.Equal(snapshotPath, reopened.CurrentSnapshotPath);
        Assert.Equal(ledgerPath, reopened.CurrentLedgerPath);
        Assert.Null(reopened.Pending);
        Assert.True(reopened.HeadRecoveryRequired);
        Assert.Equal(commits, f.Runtime.Handler.Commits);
        Assert.Equal(settlements, f.Runtime.Handler.Settlements);
        f.AssertRetainedBytes(before);
        Assert.Equal(0, f.ResumeWrites);
    }

    [Fact]
    public async Task ManagedOwnerResumeMissingDirectoryOrHeaderRefusesWithoutCreation()
    {
        using var f = new ManagedResumeFixture();
        await Assert.ThrowsAsync<InvalidDataException>(() => f.Resume());
        Assert.False(Directory.Exists(f.Files.OwnerRoot));
        Directory.CreateDirectory(f.Files.OwnerRoot);
        await Assert.ThrowsAsync<InvalidDataException>(() => f.Resume());
        Assert.Empty(Directory.EnumerateFileSystemEntries(f.Files.OwnerRoot));
        Assert.Equal(0, f.ResumeWrites);
    }

    [Fact]
    public async Task ManagedOwnerResumeBindsExpectedRunExecutorBackendJournalAndSave()
    {
        using var f = new ManagedResumeFixture();
        await using (await f.Install()) { }
        var before = f.RetainedBytes();
        var mismatches = new[]
        {
            f.Options with { RunId = "foreign-run" },
            f.Options with { ExecutorVersion = "foreign-executor" },
            f.Options with { BackendUrl = "http://foreign" },
            f.Options with { PortfolioJournalDirectory = f.Options.PortfolioJournalDirectory + "-foreign" },
            f.Options with { NativeSaveBinding = f.Options.NativeSaveBinding! with { SaveSlot = "foreign-slot" } },
            f.Options with { NativeSaveBinding = f.Options.NativeSaveBinding! with { SaveIsolationPath = f.Options.NativeSaveBinding!.SaveIsolationPath + "-foreign" } }
        };
        foreach (var options in mismatches)
            await Assert.ThrowsAsync<InvalidDataException>(() => f.Resume(options));
        f.AssertRetainedBytes(before);
        Assert.Equal(0, f.ResumeWrites);
    }

    [Fact]
    public async Task ManagedOwnerResumeRejectsChangedOriginalInputsEvenWithWarmReadCache()
    {
        using var f = new ManagedResumeFixture();
        await using (await f.Install()) { }
        foreach (var path in new[] { f.Options.NativeRunPlanPath, f.Options.InitialSnapshotPath, f.Options.InitialLedgerPath })
        {
            var original = File.ReadAllText(path);
            using var warm = InvocationReadSession.Start(new[] { path }, enabled: true);
            _ = ContentAddressedJsonArtifactReader.ReadAllText(path);
            try
            {
                // Keep valid JSON and semantic content; the original physical input hash must still bind it.
                File.WriteAllText(path, original + " ");
                await Assert.ThrowsAsync<InvalidDataException>(() => f.Resume());
            }
            finally { File.WriteAllText(path, original); }
        }
        await using var reopened = await f.Resume();
        Assert.Equal(f.Options.InitialLedgerPath, reopened.CurrentLedgerPath);
        Assert.Equal(0, f.ResumeWrites);
    }

    [Fact]
    public async Task ManagedOwnerResumeRejectsHeaderSchemaFlagsAndUnrecognizedChanges()
    {
        using var f = new ManagedResumeFixture();
        await using (await f.Install()) { }
        foreach (var (name, value) in new (string, JsonNode?)[]
        {
            ("schema_version", JsonValue.Create("foreign.v1")),
            ("selected_refill_resource_effects", JsonValue.Create(false)),
            ("selected_refill_resource_effects", null),
            ("execution_authorized", JsonValue.Create(true)),
            ("execution_authorized", null),
            ("formal_product_training_authorized", JsonValue.Create(true)),
            ("formal_product_training_authorized", null),
            ("unknown_header_field", JsonValue.Create(true))
        })
            await f.RejectHeader(header => header[name] = value);
        await using var reopened = await f.Resume();
        Assert.Equal(0, f.ResumeWrites);
    }

    [Fact]
    public async Task ManagedOwnerResumeRequiresAbsoluteExistingRegularOriginalInputPaths()
    {
        using var f = new ManagedResumeFixture();
        await using (await f.Install()) { }
        foreach (var name in new[] { "native_run_plan_path", "initial_snapshot_path", "initial_ledger_path" })
        {
            await f.RejectHeader(header => header[name] = "relative.json");
            await f.RejectHeader(header => header[name] = Path.Combine(f.Files.OwnerRoot, "missing.json"));
            await f.RejectHeader(header => header[name] = f.Files.OwnerRoot);
        }
        Assert.Equal(0, f.ResumeWrites);
    }

    [Fact]
    public async Task ManagedOwnerResumeUsesOriginalExclusiveLeaseAndReleasesItAfterDispose()
    {
        using var f = new ManagedResumeFixture();
        await using (var active = await f.Install())
            await Assert.ThrowsAsync<IOException>(() => f.Resume());
        await using (var active = await f.Resume())
            await Assert.ThrowsAsync<IOException>(() => f.Resume());
        await using var reopened = await f.Resume();
        Assert.Equal(f.Options.InitialSnapshotPath, reopened.CurrentSnapshotPath);
        Assert.Equal(0, f.ResumeWrites);
    }

    [Fact]
    public async Task ManagedOwnerResumeDelegatesIndexValidationAndReleasesLeaseOnFailure()
    {
        using var f = new ManagedResumeFixture();
        await using (await f.Install()) { }
        var orphan = Path.Combine(f.Files.OwnerRoot, "operation-000001-accepted.json");
        File.WriteAllText(orphan, "{}");
        await Assert.ThrowsAsync<InvalidDataException>(() => f.Resume());
        // Acquiring the same original lock proves that the failed original open did not leak its lease.
        using var lease = new FileStream(Path.Combine(f.Files.OwnerRoot, "owner.lock"), FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Assert.Equal(0, f.ResumeWrites);
    }

    private sealed class ManagedResumeFixture : IDisposable
    {
        internal Fixture Source { get; } = new(otherSeeds: 39);
        internal RuntimeFixture Runtime { get; }
        internal OwnerInputs Inputs { get; }
        internal OwnerFiles Files { get; }
        internal CropTeacherManagedSeedOwnerOptions Options { get; }
        internal int ResumeWrites { get; private set; }
        private string HeaderPath => Path.Combine(Files.OwnerRoot, "owner-intent.json");

        internal ManagedResumeFixture()
        {
            Runtime = new(Source);
            Runtime.LiveOptions.RunId = Source.Transition().RunId;
            Inputs = new(Source);
            Files = new(Source, Runtime, Inputs);
            Options = Files.Options with
            {
                UseSelectedRefillResourceEffects = true,
                NativeSaveBinding = new(Path.Combine(Path.GetTempPath(), "managed-resume-save-" + Guid.NewGuid().ToString("N")), "fixture-slot")
            };
        }

        internal Task<CropTeacherManagedSeedOwner> Install() => Files.Open(Options);

        internal Task<CropTeacherManagedSeedOwner> Resume(CropTeacherManagedSeedOwnerOptions? expected = null, string? backend = null)
        {
            var value = expected ?? Options;
            return CropTeacherManagedOwnerResume.OpenAsync(value.EvidenceDirectory, value.RunId, value.ExecutorVersion,
                backend ?? value.BackendUrl, value.PortfolioJournalDirectory, value.NativeSaveBinding!, (path, json) =>
                {
                    ResumeWrites++;
                    throw new InvalidOperationException("Retained resume must not write or reinstall evidence.");
                });
        }

        internal async Task RejectHeader(Action<JsonObject> change)
        {
            var original = File.ReadAllText(HeaderPath);
            try
            {
                var header = JsonNode.Parse(original)!.AsObject();
                change(header);
                File.WriteAllText(HeaderPath, header.ToJsonString(JsonOptions));
                await Assert.ThrowsAsync<InvalidDataException>(() => Resume());
            }
            finally { File.WriteAllText(HeaderPath, original); }
        }

        internal Dictionary<string, byte[]> RetainedBytes() => Directory.EnumerateFiles(Files.OwnerRoot, "*", SearchOption.AllDirectories)
            .Where(path => Path.GetFileName(path) != "owner.lock")
            .Concat(new[] { Options.NativeRunPlanPath, Options.InitialSnapshotPath, Options.InitialLedgerPath })
            .ToDictionary(path => path, File.ReadAllBytes);

        internal void AssertRetainedBytes(Dictionary<string, byte[]> before)
        {
            var after = RetainedBytes();
            Assert.Equal(before.Keys.OrderBy(path => path), after.Keys.OrderBy(path => path));
            foreach (var (path, bytes) in before) Assert.Equal(bytes, after[path]);
        }

        // Keep declared test artifacts for controller evidence; do not invoke fixture cleanup.
        public void Dispose() => Runtime.Dispose();
    }
}
