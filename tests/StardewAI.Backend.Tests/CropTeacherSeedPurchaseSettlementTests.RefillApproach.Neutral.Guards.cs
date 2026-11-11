using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Theory]
    [InlineData("energy")]
    [InlineData("water")]
    [InlineData("money")]
    [InlineData("stock")]
    [InlineData("slot-quality")]
    [InlineData("slot-maximum-stack")]
    [InlineData("slot-runtime-type")]
    [InlineData("crop-growth")]
    [InlineData("crop-watered")]
    [InlineData("unknown-crop")]
    [InlineData("unknown-persistent")]
    public async Task RefillApproachNeutralRejectsPhysicalDriftAtOriginalResourceGuard(string change)
    {
        using var f = new RefillTraceFixture(repair: false, completeSnapshotInventory: true, resourceNeutral: true);
        await f.Build(changeFrame: (after, _) => MutateNeutralResource(after, change));
        // These identities are deliberately coherent: the neutral projector, not an
        // earlier stale-state/result/receipt check, must reject the physical change.
        var final = JsonSerializer.Deserialize<SnapshotEnvelope>(File.ReadAllText(f.AfterPath), JsonOptions)!;
        Assert.Equal(SnapshotHash.ComputeStateHash(final.State), final.StateHash);
        Assert.Equal(final.StateHash, JsonNode.Parse(File.ReadAllText(f.ReceiptPath))!["after_state_hash"]!.GetValue<string>());
        var error = Assert.Throws<InvalidDataException>(() => VerifyNeutralApproach(f));
        Assert.Contains("resource-neutral approach changed persistent resources/state", error.Message);
    }

    [Fact]
    public async Task RefillApproachNeutralRequestedObservationCannotInventItsRawInventoryDenominator()
    {
        using var f = new RefillTraceFixture(repair: false, completeSnapshotInventory: true, resourceNeutral: true);
        await f.Build();
        var result = JsonNode.Parse(File.ReadAllText(f.Results[0]))!;
        result["action_origin_observation"]!["inventory_before"]![1]!["quantity"] = 6;
        result["action_origin_observation"]!["inventory_after"]![1]!["quantity"] = 6;
        Fixture.Save(f.Results[0], result);
        var error = Assert.Throws<InvalidDataException>(() => VerifyNeutralApproach(f));
        Assert.Contains("outside the captured native interval", error.Message);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RefillApproachNeutralRejectsRequestedMissingOrUnrequestedAttachedObservation(bool requested)
    {
        using var f = new RefillTraceFixture(repair: false, completeSnapshotInventory: true,
            resourceNeutral: true, observeNativeOutputs: requested);
        await f.Build(emitObservations: !requested);
        var error = Assert.Throws<InvalidDataException>(() => VerifyNeutralApproach(f));
        Assert.Contains("observation", error.Message.ToLowerInvariant());
    }

    [Theory]
    [InlineData("nonce")]
    [InlineData("raw-wire")]
    [InlineData("target")]
    [InlineData("source")]
    public async Task RefillApproachNeutralPreservesOriginalWireSourceNonceAndTargetGuards(string change)
    {
        using var f = new RefillTraceFixture(repair: false, completeSnapshotInventory: true, resourceNeutral: true);
        await f.Build(changeFrame: change == "target" ? (after, _) =>
            ChangeState(after, "player", node => node["tile_x"]!["value"] = 1) : null);
        var preparation = PrepareNeutralApproach(f);
        if (change == "nonce")
        {
            var result = JsonNode.Parse(File.ReadAllText(f.Results[0]))!;
            result["execution_request_nonce"] = Guid.NewGuid().ToString("N");
            Fixture.Save(f.Results[0], result);
        }
        if (change == "raw-wire")
        {
            string raw = await ReadNeutralRequestBlob(f.Journals[0]);
            // A valid gzip and unchanged JSON semantics still cannot change original
            // raw bytes while keeping the journal's raw hash/byte denominator.
            using var file = File.Create(f.Journals[0] + ".gz");
            using var gzip = new GZipStream(file, CompressionLevel.Fastest);
            byte[] bytes = Encoding.UTF8.GetBytes(raw + " ");
            gzip.Write(bytes);
        }
        if (change == "source")
        {
            var before = f.Approach.Watering.Before;
            ChangeState(before, "player", node => node["energy"]!["value"] = 99);
            before.StateHash = SnapshotHash.ComputeStateHash(before.State);
            Fixture.Save(f.Approach.Watering.Shared.Source.SourcePath, before);
        }
        var error = Assert.Throws<InvalidDataException>(() => preparation.VerifyTransition(NeutralTransition(f, preparation)));
        if (change == "target") Assert.Contains("original approach tile", error.Message);
    }

    [Theory]
    [InlineData("source")]
    [InlineData("result")]
    [InlineData("journal")]
    [InlineData("gzip")]
    [InlineData("receipt")]
    [InlineData("frame")]
    [InlineData("final")]
    public async Task RefillApproachNeutralWithoutObservationsStillPinsEveryOriginalEvidenceArtifact(string artifact)
    {
        using var f = new RefillTraceFixture(repair: false, completeSnapshotInventory: true,
            resourceNeutral: true, observeNativeOutputs: false);
        await f.Build();
        var outcome = VerifyNeutralApproach(f);
        string path = artifact switch
        {
            "source" => f.Approach.Watering.Shared.Source.SourcePath,
            "result" => f.Results[0], "journal" => f.Journals[0], "gzip" => f.Journals[0] + ".gz",
            "receipt" => f.ReceiptPath, "frame" => f.AfterFrames[0], "final" => f.AfterPath,
            _ => throw new ArgumentOutOfRangeException(nameof(artifact))
        };
        File.AppendAllText(path, " ");
        Assert.Throws<InvalidDataException>(outcome.ValidateInputsUnchanged);
        Assert.Throws<InvalidDataException>(() => outcome.RebindRequest);
    }

    private static void MutateNeutralResource(SnapshotEnvelope after, string change)
    {
        if (change is "energy" or "water" or "money" or "stock" or "slot-quality")
            ChangeState(after, "player", node =>
            {
                if (change == "energy") node["energy"]!["value"] = node["energy"]!["value"]!.GetValue<float>() + 0.0001f;
                if (change == "water") node["watering_can_state"]!["value"]!["water_left"] = 1;
                if (change == "money")
                {
                    int money = node["money"]!["value"]!.GetValue<int>() + 1;
                    node["money"]!["value"] = money;
                    var currency = node["shop_currency_balances"]!["value"]!["rows"]!.AsArray()
                        .Single(row => row!["currency_id"]!.GetValue<int>() == 0)!;
                    currency["balance"] = money;
                }
                if (change == "stock") node["inventory"]!["value"]![1]!["stack"] = 6;
                if (change == "slot-quality") node["inventory"]!["value"]![1]!["quality"] = 1;
            });
        if (change is "stock" or "slot-quality" or "slot-maximum-stack" or "slot-runtime-type")
            ChangeState(after, "farm", node =>
            {
                var slot = node["material_inventory_graph"]!["value"]!["inventory_nodes"]![0]!["slots"]![1]!;
                if (change == "stock") slot["stack"] = 6;
                if (change == "slot-quality") slot["quality"] = 1;
                if (change == "slot-maximum-stack") slot["maximum_stack_size"] = 998;
                if (change == "slot-runtime-type") slot["runtime_type"] = "StardewValley.Objects.ColoredObject";
            });
        if (change is "crop-growth" or "crop-watered" or "unknown-crop")
        {
            ChangeState(after, "farm", node =>
            {
                var crop = node["crops"]!["value"]![0]!;
                if (change == "crop-growth") crop["day_of_current_phase"] = 1;
                if (change == "crop-watered") { crop["watered"] = true; crop["needs_watering"] = false; }
                if (change == "unknown-crop") crop["unknown_native_crop_detail"] = "changed";
            });
            ChangeState(after, "current_location", node => node["crops"] =
                JsonNode.Parse(after.State["farm"].GetRawText())!["crops"]!.DeepClone());
        }
        if (change == "unknown-persistent") ChangeState(after, "world_progress", node =>
            node["unrelated_observed_native_progress"] = new JsonObject { ["status"] = "available", ["value"] = 1 });
    }
}
