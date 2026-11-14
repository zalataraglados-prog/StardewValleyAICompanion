using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Core.Infrastructure;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed class CropTeacherTerminalShippingSelectionTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly Type Selection = typeof(CropTeacherManagedSeedOwner).Assembly.GetType("StardewAI.LiveTrainingLoop.CropTeacherTerminalShippingSelection")!;

    [Fact]
    public async Task TerminalShippingSelectionBindsOriginalWaveAndEmitsOnlyItsOriginalApproachAndSingleDepositModels()
    {
        var f = new CropTeacherProductionEvidenceTests.Fixture(); var evidence = await f.Build();
        var selection = Open(f, evidence); var source = Read(f.SourcePath);
        var approach = Build(selection, source, 0)!;
        Assert.Equal("authoritative_crop_terminal_continuation.v1", approach.SourceModel);
        Assert.Equal(source.StateHash, approach.StateHash); Assert.Equal("training_farmer.main", approach.Actor.ActorId);
        Assert.Equal("executor.move_to_tile", Assert.Single(approach.Actions).OptionId);
        Assert.Contains(approach.Actions[0].Parameters, row => row.Name == "target_tile_x" && row.Value == "70");
        Assert.Contains(approach.Actions[0].Parameters, row => row.Name == "continuation.qualified_item_id" && row.Value == "(O)400");
        Move(source, 70, 14);
        var deposit = Build(selection, source, 0)!;
        var action = Assert.Single(deposit.Actions);
        Assert.Equal("executor.ship_inventory_item_to_bin", action.OptionId);
        Assert.Contains(action.Parameters, row => row.Name == "quantity" && row.Value == "1");
        Assert.Contains(action.Parameters, row => row.Name == "expected_unit_price" && row.Value == "120");
        Assert.Contains(action.Parameters, row => row.Name == "precondition" && row.Value.EndsWith(":deposit:1", StringComparison.Ordinal));
        Assert.Equal(evidence.ManifestSha256, Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(evidence.ManifestPath))).ToLowerInvariant());
    }

    [Theory]
    [InlineData("manifest_hash")]
    [InlineData("head")]
    [InlineData("ledger")]
    [InlineData("retention")]
    public async Task TerminalShippingSelectionRejectsUnboundEvidenceBeforeAnyNativeWork(string fault)
    {
        var f = new CropTeacherProductionEvidenceTests.Fixture(); var evidence = await f.Build();
        if (fault == "manifest_hash") evidence = evidence with { ManifestSha256 = new string('0', 64) };
        if (fault == "retention") evidence = evidence with { OutputRetentionProofPaths = Array.Empty<string>() };
        Assert.Throws<InvalidDataException>(() => Open(f, evidence, fault == "head" ? f.LedgerPath : null,
            fault == "ledger" ? f.SourcePath : null));
    }

    [Theory]
    [InlineData("cursor")]
    [InlineData("slot")]
    [InlineData("pending")]
    [InlineData("actor")]
    [InlineData("price")]
    [InlineData("shippable")]
    [InlineData("bin_actor")]
    public async Task TerminalShippingSelectionCannotInventAnAcceptedDepositOrSwitchItsVariant(string fault)
    {
        var f = new CropTeacherProductionEvidenceTests.Fixture(); var selection = Open(f, await f.Build());
        var source = Read(f.SourcePath);
        if (fault == "actor") source.PlayerId.Value += ":other";
        if (fault == "slot") Change(source, "player", node => node["inventory"]!["value"]![0]!["quality"] = 4);
        if (fault == "price") Change(source, "player", node => node["inventory"]!["value"]![0]!["sell_to_store_price"] = 999);
        if (fault == "shippable") Change(source, "player", node => node["inventory"]!["value"]![0]!["can_be_shipped"] = false);
        if (fault == "bin_actor") Change(source, "farm", node => node["shipping_bins"]!["value"]![0]!["player_id"] = "other");
        if (fault == "pending") Change(source, "farm", node => node["shipping_bins"]!["value"]![0]!["pending_settlement_value"] = 120);
        Assert.Throws<InvalidDataException>(() => Build(selection, source, fault == "cursor" ? 1 : 0));
    }

    private static object Open(CropTeacherProductionEvidenceTests.Fixture f, CropTeacherManagedProductionEvidence evidence,
        string? head = null, string? ledger = null) => Unwrap(() => Selection.GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance).Single(row => row.GetParameters().Length == 4)
            .Invoke(new object[] { evidence, f.NativePlanPath, head ?? f.SourcePath, ledger ?? f.LedgerPath }))!;
    private static SmallModelActionEnvelope? Build(object selection, SnapshotEnvelope source, int cursor) =>
        (SmallModelActionEnvelope?)Unwrap(() => Selection.GetMethod("BuildOriginalAction", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(selection, new object[] { source, cursor }));
    private static object? Unwrap(Func<object?> invoke)
    {
        try { return invoke(); }
        catch (TargetInvocationException error) when (error.InnerException is not null)
        { ExceptionDispatchInfo.Capture(error.InnerException).Throw(); throw; }
    }
    private static SnapshotEnvelope Read(string path) => JsonSerializer.Deserialize<SnapshotEnvelope>(File.ReadAllText(path), JsonOptions)!;
    private static void Move(SnapshotEnvelope source, int x, int y) => Change(source, "player", node =>
    { node["tile_x"]!["value"] = x; node["tile_y"]!["value"] = y; });
    private static void Change(SnapshotEnvelope source, string domain, Action<JsonObject> change)
    {
        var node = JsonNode.Parse(source.State[domain].GetRawText())!.AsObject(); change(node);
        source.State[domain] = JsonSerializer.SerializeToElement(node); source.StateHash = SnapshotHash.ComputeStateHash(source.State);
    }
}
