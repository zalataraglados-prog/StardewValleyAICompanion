using System.Text.Json.Nodes;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OwnerPreparationDistinguishesUnclaimedFirstCallFromUncertainClaim(bool acknowledgementLost)
    {
        using var f = new Fixture(otherSeeds: 39);
        using var runtime = new RuntimeFixture(f);
        using var inputs = new OwnerInputs(f);
        using var files = new OwnerFiles(f, runtime, inputs);
        var transition = f.Transition();
        await using var owner = await files.Open(writer: async (path, json) =>
        {
            var start = Path.GetFileName(path) == "operation-000001-started.json";
            if (start && !acknowledgementLost) throw new IOException("Claim was not written.");
            await InvokeLive<Task>("WritePortfolioJournalFileAsync", path, json);
            if (start) throw new IOException("Claim durable; acknowledgement lost.");
        });
        var step = CropTeacherRunPlanner.Evaluate(inputs.Plan, inputs.Request, f.Before, resourceLedger: f.Ledger);
        var operation = await owner.AdmitAsync(step, f.SourcePath, f.LedgerPath, files.Native);
        var calls = 0;
        Task<CropTeacherSeedPurchaseRuntime> Prepare(CropTeacherManagedSeedOperation item)
        {
            calls++;
            return runtime.Prepare(transition, selectedScope: item.Scopes.Purchase, runtimeDirectory: item.RuntimeDirectory);
        }
        await Assert.ThrowsAsync<IOException>(() => owner.PrepareSeedAsync(operation, () => Prepare(operation)));
        Assert.Equal(0, calls);
        await owner.DisposeAsync();
        await using var resumed = await files.Open();
        if (acknowledgementLost)
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => resumed.PrepareSeedAsync(resumed.Pending!, () => Prepare(resumed.Pending!)));
            Assert.Equal(0, calls);
            Assert.Equal(0, runtime.Handler.Commits);
        }
        else
        {
            await resumed.PrepareSeedAsync(resumed.Pending!, () => Prepare(resumed.Pending!));
            Assert.Equal(1, calls);
            Assert.Equal(1, runtime.Handler.Commits);
        }
    }

    [Fact]
    public async Task OwnerPreparationPreservesLegacyRecordsWithoutPretendingTheyHadAPreMutationPin()
    {
        using var f = new Fixture(otherSeeds: 39);
        using var runtime = new RuntimeFixture(f);
        using var inputs = new OwnerInputs(f);
        using var files = new OwnerFiles(f, runtime, inputs);
        var transition = f.Transition();
        await using (var owner = await files.Open())
        {
            var step = CropTeacherRunPlanner.Evaluate(inputs.Plan, inputs.Request, f.Before, resourceLedger: f.Ledger);
            await owner.AdmitAsync(step, f.SourcePath, f.LedgerPath, files.Native);
        }
        var path = Path.Combine(files.OwnerRoot, "operation-000001-intent.json");
        var record = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        record.Remove("original_intent_sha256");
        File.WriteAllText(path, record.ToJsonString());
        await using var legacy = await files.Open();
        var pending = legacy.Pending!;
        var calls = 0;
        await Assert.ThrowsAsync<InvalidOperationException>(() => legacy.PrepareSeedAsync(pending, () => { calls++; return Task.FromResult<ICropTeacherSeedRuntime>(null!); }));
        Assert.Equal(0, calls);
        var prepared = await runtime.Prepare(transition, selectedScope: pending.Scopes.Purchase, runtimeDirectory: pending.RuntimeDirectory);
        await legacy.PinPreparedAsync(pending, prepared);
        runtime.ObserveAfter();
        await prepared.CompleteAsync(transition);
        await legacy.AcceptLiveAsync(pending, prepared);
        Assert.Null(legacy.Pending);
        Assert.Equal(2, runtime.Handler.Commits);
        Assert.Equal(1, runtime.Handler.Settlements);
    }

    [Fact]
    public async Task OwnerPreparationRejectsAnActualJournalWrittenOutsideItsPinnedRoot()
    {
        using var f = new Fixture(otherSeeds: 39);
        using var runtime = new RuntimeFixture(f);
        using var inputs = new OwnerInputs(f);
        using var files = new OwnerFiles(f, runtime, inputs);
        var wrong = Path.Combine(Path.GetDirectoryName(files.OwnerRoot)!, "wrong-seed-journals");
        Directory.CreateDirectory(wrong);
        await using var owner = await files.Open(files.Options with { PortfolioJournalDirectory = wrong });
        var transition = f.Transition();
        var step = CropTeacherRunPlanner.Evaluate(inputs.Plan, inputs.Request, f.Before, resourceLedger: f.Ledger);
        var operation = await owner.AdmitAsync(step, f.SourcePath, f.LedgerPath, files.Native);
        await Assert.ThrowsAsync<FileNotFoundException>(() => owner.PrepareSeedAsync(operation, () => runtime.Prepare(transition,
            selectedScope: operation.Scopes.Purchase, runtimeDirectory: operation.RuntimeDirectory)));
        Assert.Same(operation, owner.Pending);
        Assert.False(File.Exists(Path.Combine(files.OwnerRoot, "operation-000001-prepared.json")));
        Assert.Equal(1, runtime.Handler.Commits);
        Assert.Equal(0, runtime.Handler.Settlements);
    }
}
