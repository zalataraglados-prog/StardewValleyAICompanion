using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.Testing;

namespace StardewAI.Backend.Tests;

public sealed class MoneyCropTeacherDispatchVerificationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReadsOriginalDispatchWithoutWritingOrPromotingEvidence(bool baseLedger)
    {
        var f = Create(baseLedger);
        var before = Files(f.Root);
        var manifest = MoneyRevenueCropTeacherDispatchBuilder.ReadVerified(f.Path);
        Assert.Equal(JsonSerializer.Serialize(f.Manifest, JsonOptions), JsonSerializer.Serialize(manifest, JsonOptions));
        Assert.False(manifest.FormalProductTrainingAuthorized);
        Assert.Equal(before, Files(f.Root));
    }

    [Theory]
    [InlineData("action_count")]
    [InlineData("source_state_hash")]
    [InlineData("decision_id")]
    [InlineData("formal_product_training_authorized")]
    [InlineData("missing")]
    [InlineData("unknown")]
    public void RejectsChangedOrIncompleteManifestWithoutWriting(string field)
    {
        var f = Create();
        var node = JsonNode.Parse(File.ReadAllText(f.Path))!.AsObject();
        if (field == "missing") node.Remove("formal_product_training_authorized");
        else node[field] = field switch
        {
            "action_count" => JsonValue.Create(f.Manifest.ActionCount + 1),
            "formal_product_training_authorized" or "unknown" => JsonValue.Create(true),
            _ => JsonValue.Create("foreign")
        };
        File.WriteAllText(f.Path, node.ToJsonString(JsonOptions));
        var before = Files(f.Root);
        Assert.Throws<InvalidDataException>(() => MoneyRevenueCropTeacherDispatchBuilder.ReadVerified(f.Path));
        Assert.Equal(before, Files(f.Root));
    }

    [Theory]
    [InlineData("decision")]
    [InlineData("ledger")]
    [InlineData("capture")]
    [InlineData("run_request")]
    public void RejectsRehashedArtifactDriftRatherThanTrustingTheManifestHash(string field)
    {
        var f = Create();
        var path = field switch
        {
            "decision" => f.Manifest.TeacherDecisionPath,
            "ledger" => f.Manifest.StrategyLedgerPath,
            "capture" => f.Manifest.CaptureManifestPath,
            _ => f.Manifest.NativeRunPlanRequestPath
        };
        var node = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        if (field == "capture") node["layout_snapshots"]![0]!["tile_count"] = 999;
        else if (field == "run_request") node["max_queue_item_count"] = 1;
        else node[field == "decision" ? "decision_id" : "source_state_hash"] = "foreign";
        File.WriteAllText(path, node.ToJsonString(JsonOptions));
        var manifest = JsonNode.Parse(File.ReadAllText(f.Path))!.AsObject();
        manifest[field switch
        {
            "decision" => "teacher_decision_sha256", "ledger" => "strategy_ledger_sha256",
            "capture" => "capture_manifest_sha256", _ => "native_run_plan_request_sha256"
        }] = Hash(path);
        File.WriteAllText(f.Path, manifest.ToJsonString(JsonOptions));
        var before = Files(f.Root);
        Assert.Throws<InvalidDataException>(() => MoneyRevenueCropTeacherDispatchBuilder.ReadVerified(f.Path));
        Assert.Equal(before, Files(f.Root));
    }

    private static (string Root, string Path, MoneyRevenueCropTeacherDispatchManifest Manifest) Create(bool baseLedger = false)
    {
        var fixture = new MoneyCropTeacherFixture();
        var root = Path.GetDirectoryName(fixture.SnapshotPath)!;
        if (baseLedger) fixture.SetLedger(fixture.EmptyLedger());
        var capture = MoneyCropTeacherCaptureFixture.Save(fixture, Path.Combine(root, "capture.json"));
        var manifest = MoneyRevenueCropTeacherDispatchBuilder.Build(capture, Path.Combine(root, "dispatch"),
            "dispatch.readonly", "2026-11-15T00:00:00Z", baseStrategyLedgerPath: baseLedger ? fixture.LedgerPath : null);
        return (root, Path.Combine(root, "dispatch", "teacher-dispatch-manifest.json"), manifest);
    }

    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
    private static string[] Files(string root) => Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
        .Order(StringComparer.Ordinal).Select(path => path + ":" + Hash(path)).ToArray();
}
