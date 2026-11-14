using StardewAI.LiveTrainingLoop;
using static StardewAI.Backend.Tests.CropTeacherSeedPurchaseSettlementTests;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPlantingRuntimeTests
{
    [Fact]
    public async Task ProductionTraceBindsOriginalPlantingProofAndRetainsItsActualNativeKind()
    {
        using var f = new RuntimeFixture();
        using var inputs = new OwnerInputs(f.Files.SourcePath, f.Files.LedgerPath, f.Files.RequestPath);
        using var files = new OwnerFiles(f.Options, f.Client, f.Files.SourcePath, f.Files.LedgerPath, inputs);
        await using var owner = await files.Open();
        var step = CropTeacherRunPlanner.Evaluate(inputs.Plan, inputs.Request, f.Files.Before,
            preparedCommitmentIds: new HashSet<string>(StringComparer.Ordinal) { f.Files.Id }, resourceLedger: f.Files.Ledger);
        var operation = await owner.AdmitAsync(step, f.Files.SourcePath, f.Files.LedgerPath, files.Native);
        var runtime = await f.Prepare(consumed: 1, selectedScope: operation.Scopes.Planting,
            runtimeDirectory: operation.RuntimeDirectory, selectedStep: operation.ReadStep());
        await owner.PinPreparedAsync(operation, runtime);
        await f.Observe(runtime); await runtime.CompleteAsync(f.Transition);
        await owner.AcceptLiveAsync(operation, runtime);
        var trace = await owner.ReadProductionTraceAsync();
        var first = Assert.Single(trace.Transitions);
        Assert.Equal("owned_seed_planting", first.OperationKind);
        Assert.Equal(f.Transition, first.NativeTransition);
        Assert.Equal(1, f.Handler.Commits); Assert.Equal(1, f.Handler.Settlements);
        trace.ValidateInputsUnchanged();
    }
}
