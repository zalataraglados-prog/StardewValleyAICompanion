using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    // These declared certificates exercise the durable reference, not whole production verification.
    private static CropTeacherTerminalProductionReference DeclaredTerminalProductionReference(
        CropTeacherManagedSeedOwner owner, TerminalOutcomeFixture f, Action<JsonObject>? change = null)
    {
        var path = Path.Combine(f.Shared.Files.OwnerRoot, "declared-production-evidence.json");
        var manifest = new JsonObject
        {
            ["schema_version"] = "crop_teacher_managed_production_evidence.v1",
            ["native_run_plan_path"] = f.Shared.Files.Options.NativeRunPlanPath,
            ["accepted_snapshot_path"] = owner.CurrentSnapshotPath, ["accepted_ledger_path"] = owner.CurrentLedgerPath,
            ["native_crop_lifecycle_verified"] = true, ["whole_wave_output_retention_verified"] = true,
            ["terminal_settlement_verified"] = false, ["formal_product_training_authorized"] = false,
            ["output_retention_proof_paths"] = new JsonArray(f.Target.RetentionProofPath)
        };
        change?.Invoke(manifest); Fixture.Save(path, manifest);
        return new(path, SavedResourceFixture.Hash(path));
    }

    private static Task<CropTeacherManagedSeedOperation> AdmitReferencedTerminal(CropTeacherManagedSeedOwner owner,
        TerminalOutcomeFixture f, CropTeacherTerminalProductionReference reference) =>
        owner.CompileAndAdmitOriginalTerminalAsync(f.Target, () => Task.FromResult(new CropTeacherOriginalTerminalCompilation(
            f.ModelPath, f.QueuePath, f.Shared.ReceiptPath, f.Shared.AfterPath, f.Shared.Files.Native)), reference);

    [Fact]
    public async Task TerminalProductionReferenceSurvivesOriginalIntentAcceptanceTraceAndReopen()
    {
        var f = new TerminalOutcomeFixture(approach: true);
        using var host = f.Shared.Runtime;
        await using var owner = await f.Shared.Files.Open();
        var reference = DeclaredTerminalProductionReference(owner, f);
        var operation = await AdmitReferencedTerminal(owner, f, reference);
        await owner.ExecuteOriginalTerminalAsync(operation, host.Client, () => Task.FromResult(EmitTerminal(f)));
        await RebindTerminal(owner, operation, f);
        var accepted = OwnedControlAccepted(f.Shared, operation);
        Assert.Equal(reference.ManifestSha256, accepted["terminal_production_manifest_sha256"]!.GetValue<string>());
        var intent = JsonNode.Parse(File.ReadAllText(Path.Combine(operation.RuntimeDirectory, "terminal-intent.json")))!;
        Assert.Equal(reference.ManifestPath, intent["terminal_production_manifest_path"]!.GetValue<string>());
        Assert.Equal(reference.ManifestSha256, intent["terminal_production_manifest_sha256"]!.GetValue<string>());
        var trace = await owner.ReadProductionTraceAsync();
        Assert.Equal(reference.ManifestSha256, trace.InputHashes[reference.ManifestPath]);
        await owner.DisposeAsync();
        await using var reopened = await f.Shared.Files.Open();
        await reopened.RestoreHeadAsync(host.Client, f.Shared.Files.Recovery, f.Shared.Files.Post);
        var replayed = await reopened.ReadProductionTraceAsync();
        Assert.Equal(reference.ManifestSha256, replayed.InputHashes[reference.ManifestPath]);
        Assert.Equal(1, host.Handler.Commits); Assert.Equal(0, reopened.AcceptedTerminalDepositCount);
        await reopened.DisposeAsync();
        File.AppendAllText(reference.ManifestPath, " ");
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => f.Shared.Files.Open());
        Assert.Contains("reference/hash changed", error.Message);
    }

    [Theory]
    [InlineData("source")]
    [InlineData("ledger")]
    [InlineData("formal")]
    [InlineData("hash")]
    public async Task TerminalProductionReferenceRejectsForeignOrChangedMetadataBeforeAdmission(string fault)
    {
        var f = new TerminalOutcomeFixture(approach: true);
        using var host = f.Shared.Runtime;
        await using var owner = await f.Shared.Files.Open();
        var reference = DeclaredTerminalProductionReference(owner, f, manifest =>
        {
            if (fault == "source") manifest["accepted_snapshot_path"] = f.AfterPath;
            if (fault == "ledger") manifest["accepted_ledger_path"] = f.AfterPath;
            if (fault == "formal") manifest["formal_product_training_authorized"] = true;
        });
        if (fault == "hash") reference = reference with { ManifestSha256 = new string('0', 64) };
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => AdmitReferencedTerminal(owner, f, reference));
        Assert.Contains(fault == "hash" ? "reference/hash changed" : "original selected production head", error.Message);
        Assert.Null(owner.Pending); Assert.Equal(0, host.Handler.Commits);
    }

    [Fact]
    public async Task TerminalProductionReferenceCannotBeDroppedAfterAcceptedApproach()
    {
        var f = new TerminalOutcomeFixture(approach: true);
        using var host = f.Shared.Runtime;
        await using var owner = await f.Shared.Files.Open();
        var operation = await AdmitReferencedTerminal(owner, f, DeclaredTerminalProductionReference(owner, f));
        await owner.ExecuteOriginalTerminalAsync(operation, host.Client, () => Task.FromResult(EmitTerminal(f)));
        await RebindTerminal(owner, operation, f);
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => owner.CompileAndAdmitOriginalTerminalAsync(f.Target,
            () => Task.FromResult(new CropTeacherOriginalTerminalCompilation(f.ModelPath, f.QueuePath,
                f.Shared.ReceiptPath + ".next", f.Shared.AfterPath + ".next", f.Shared.Files.Native + "-next"))));
        Assert.Contains("cannot disappear", error.Message); Assert.Null(owner.Pending); Assert.Equal(1, host.Handler.Commits);
    }
}
