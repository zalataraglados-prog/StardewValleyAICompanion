using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Fact]
    public void ExistingShippingRequiresExplicitPreparationAndCannotBeAdmittedByOldEntryPoints()
    {
        using var f = new NativeVariantTerminalFixture(existingShipping: true);
        var path = Path.Combine(f.Root, "not-dispatched.json");
        Assert.Contains("explicit joint", Assert.Throws<InvalidDataException>(() =>
            CropTeacherTerminalShipping.Preparation.OpenNativeVariants(path, path, path, new[] { Id }, "not-dispatched",
                f.Target, path, path)).Message, StringComparison.Ordinal);
        Assert.Contains("explicit joint", Assert.Throws<InvalidDataException>(() =>
            CropTeacherTerminalShipping.Preparation.OpenJointNativeVariants(path, path, path, new[] { Id }, "not-dispatched",
                f.Target with { ExistingShipping = null }, path, path)).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ExistingShippingIncomeIsExcludedFromCropCreditAndLegacyModeStillRequiresAnEmptyBin()
    {
        using var f = new NativeVariantTerminalFixture(existingShipping: true);
        var target = f.Target;
        Assert.Equal(205, target.ExistingShipping!.PendingValue);
        Assert.Equal(0, target.PendingValueAt(0));
        Assert.Equal(1093, target.PendingValueAt(6));
        Assert.Equal(1298, target.PhysicalPendingValueAt(6));
        target.VerifyNativeVariantSource(f.Shared.Source.Before, 0, 1050);
        Assert.Throws<InvalidDataException>(() => target.VerifyNativeVariantSource(f.Shared.Source.Before, 0, 1100));
        Assert.Throws<InvalidDataException>(() => (target with { ExistingShipping = null }).VerifyNativeVariantSource(f.Shared.Source.Before, 0));
        Assert.Equal(target, JsonSerializer.Deserialize<CropTeacherTerminalShippingTarget>(JsonSerializer.Serialize(target, JsonOptions), JsonOptions));
    }

    [Theory]
    [InlineData("contents")]
    [InlineData("price")]
    [InlineData("truncated")]
    [InlineData("signature")]
    [InlineData("pending_incomplete")]
    [InlineData("actor")]
    [InlineData("day")]
    [InlineData("baseline_value")]
    [InlineData("baseline_signature")]
    [InlineData("baseline_path")]
    [InlineData("baseline_version")]
    public void ExistingShippingRejectsUnknownOrDriftedContentsAndUnboundBaseline(string fault)
    {
        using var f = new NativeVariantTerminalFixture(existingShipping: true);
        var source = NativeVariantTerminalFixture.Copy(f.Shared.Source.Before);
        var target = f.Target;
        ChangeState(source, "farm", node =>
        {
            var bin = node["shipping_bins"]!["value"]![0]!.AsObject();
            if (fault == "contents") { bin["contents"]![0]!["qualified_item_id"] = "(O)666"; RefreshDeclaredShippingTotals(bin); }
            if (fault == "price") bin["contents"]![0]!["unit_sell_to_store_price"] = 44;
            if (fault == "truncated") bin["contents_truncated"] = true;
            if (fault == "signature") bin["contents_signature"] = "unverified";
            if (fault == "pending_incomplete") bin["pending_settlement_value_complete"] = false;
            if (fault == "actor") bin["player_id"] = "other";
        });
        if (fault == "day") ChangeState(source, "time", node => node["total_days"]!["value"] = target.TotalDay + 1);
        if (fault == "baseline_value") target = target with { ExistingShipping = target.ExistingShipping! with { PendingValue = 204 } };
        if (fault == "baseline_signature") target = target with { ExistingShipping = target.ExistingShipping! with { ContentsSignature = "other" } };
        if (fault == "baseline_path") target = target with { ExistingShipping = target.ExistingShipping! with { SnapshotPath = target.RetentionProofPath } };
        if (fault == "baseline_version") target = target with { ExistingShipping = target.ExistingShipping! with { SchemaVersion = "unknown" } };
        Assert.Throws<InvalidDataException>(() => target.VerifyNativeVariantSource(source, 0));
    }

    [Theory]
    [InlineData("crop_teacher_native_variant_terminal_request.v1")]
    [InlineData("crop_teacher_native_variant_terminal_request.v2")]
    [InlineData("crop_teacher_native_variant_terminal_request.v3")]
    public void ExistingShippingCannotBeDowngradedToALegacySettlementWrapper(string version)
    {
        using var f = new NativeVariantTerminalFixture(existingShipping: true);
        var path = Path.Combine(f.Root, "baseline-legacy-request.json");
        Fixture.Save(path, new CropTeacherNativeVariantTerminalRequest(version,
            f.Target with { AcceptedDepositCount = 6 }, new MoneyRevenueCropTerminalSettlementProofRequest()));
        Assert.Contains("explicit joint aggregate", Assert.Throws<InvalidDataException>(() =>
            CropTeacherNativeVariantTerminalProof.Build(path)).Message, StringComparison.Ordinal);
    }

    private static int ShippingItemCount(SnapshotEnvelope source, string item) => source.State["farm"]
        .GetProperty("shipping_bins").GetProperty("value")[0].GetProperty("contents").EnumerateArray()
        .Where(row => row.GetProperty("qualified_item_id").GetString() == item).Sum(row => row.GetProperty("count").GetInt32());

    private static void SetDeclaredExistingShipping(JsonObject bin, string item, bool sameItem)
    {
        bin["contents"] = sameItem ? new JsonArray(new JsonObject { ["item_id"] = item[3..], ["qualified_item_id"] = item,
            ["quality"] = 0, ["count"] = 1, ["unit_sell_to_store_price"] = 175, ["total_sell_to_store_price"] = 175 }) :
            new JsonArray(new JsonObject { ["item_id"] = "147", ["qualified_item_id"] = "(O)147", ["quality"] = 0,
                ["count"] = 1, ["unit_sell_to_store_price"] = 45, ["total_sell_to_store_price"] = 45 },
                new JsonObject { ["item_id"] = "708", ["qualified_item_id"] = "(O)708", ["quality"] = 0,
                    ["count"] = 1, ["unit_sell_to_store_price"] = 160, ["total_sell_to_store_price"] = 160 });
        RefreshDeclaredShippingTotals(bin);
    }

    private static void MergeDeclaredExistingShipping(JsonArray contents, JsonElement initial)
    {
        foreach (var original in initial.EnumerateArray())
        {
            var prior = JsonNode.Parse(original.GetRawText())!.AsObject();
            var row = contents.OfType<JsonObject>().SingleOrDefault(row => row["qualified_item_id"]!.GetValue<string>() == prior["qualified_item_id"]!.GetValue<string>() &&
                row["quality"]!.GetValue<int>() == prior["quality"]!.GetValue<int>() && row["unit_sell_to_store_price"]!.GetValue<int>() == prior["unit_sell_to_store_price"]!.GetValue<int>());
            if (row is null) contents.Add(prior);
            else { row["count"] = row["count"]!.GetValue<int>() + prior["count"]!.GetValue<int>();
                row["total_sell_to_store_price"] = row["total_sell_to_store_price"]!.Deserialize<long>() + prior["total_sell_to_store_price"]!.Deserialize<long>(); }
        }
    }

    private static void RefreshDeclaredShippingTotals(JsonObject bin)
    {
        var rows = bin["contents"]!.AsArray().OfType<JsonObject>().OrderBy(row => row["qualified_item_id"]!.GetValue<string>(), StringComparer.Ordinal)
            .ThenBy(row => row["quality"]!.GetValue<int>()).ThenBy(row => row["unit_sell_to_store_price"]!.GetValue<int>()).ToArray();
        bin["contents"] = new JsonArray(rows.Select(row => (JsonNode)row.DeepClone()).ToArray());
        bin["contents_total_count"] = rows.Sum(row => row["count"]!.GetValue<int>());
        bin["contents_distinct_item_count"] = rows.Select(row => row["qualified_item_id"]!.GetValue<string>()).Distinct().Count();
        bin["contents_distinct_settlement_variant_count"] = rows.Length;
        bin["pending_settlement_value"] = rows.Sum(row => row["total_sell_to_store_price"]!.Deserialize<long>());
        bin["contents_signature"] = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Concat(rows.Select(row =>
            FormattableString.Invariant($"{row["qualified_item_id"]!.GetValue<string>()}|{row["quality"]!.GetValue<int>()}|{row["unit_sell_to_store_price"]!.GetValue<int>()}|{row["count"]!.GetValue<int>()}\n")))))).ToLowerInvariant();
    }
}
