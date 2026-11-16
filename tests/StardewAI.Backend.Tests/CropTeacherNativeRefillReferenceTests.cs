using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

// Metadata binding fixtures only; original native compilation/trace remains a separate verifier.
public sealed class CropTeacherNativeRefillReferenceTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData("refill_approach_resources", "native_selected_resource_effects")]
    [InlineData("refill_approach", "native_resource_neutral")]
    [InlineData("refill_approach_native", "native_engine_observed")]
    public void NativeRefillReferenceUsesOnlyPinnedOriginalOwnerFieldsWithoutMutatingReceipt(string control, string expectedMode)
    {
        var f = new Case(control);
        var trace = f.Trace(); var before = trace.InputHashes.ToDictionary(row => row.Key, row => row.Value);
        var result = Bind(trace, f.Plan, f.Row);
        var reference = Assert.IsType<MoneyRevenueCropNativeRefillPreparationReference>(result.NativeRefillPreparation);
        Assert.Equal(expectedMode, reference.ResourceMode); Assert.Equal("crop.original", reference.CommitmentId);
        Assert.Equal(f.Plan.RequestPath, reference.RunRequestPath);
        Assert.Equal(f.Native.QueuePath, result.QueuePath); Assert.Equal(f.Native.ExecutionReceiptPath, result.ExecutionReceiptPath);
        Assert.Null(f.Native.NativeRefillPreparation);
        foreach (var row in before) Assert.Equal(row.Value, Hash(row.Key));
    }

    [Theory]
    [InlineData("sequence")]
    [InlineData("transition")]
    [InlineData("coverage")]
    [InlineData("mode")]
    [InlineData("ledger_unpinned")]
    [InlineData("model_unpinned")]
    [InlineData("request_unpinned")]
    public void NativeRefillReferenceRejectsUnboundOrMismatchedMetadata(string fault)
    {
        var f = new Case("refill_approach_resources");
        if (fault == "sequence") f.Record["sequence"] = 67;
        if (fault == "transition") f.Record["maintenance_transition"]!["selected_candidate_id"] = "foreign";
        if (fault == "coverage") f.Record["active_commitment_ids"] = new JsonArray("foreign");
        if (fault == "mode") f.Record["maintenance_control_kind"] = "invented_refill";
        var trace = f.Trace(fault);
        var error = Assert.Throws<TargetInvocationException>(() => Invoke(trace, f.Plan, f.Row));
        Assert.IsType<InvalidDataException>(error.InnerException);
    }

    [Fact]
    public void NativeRefillReferenceDoesNotConvertOrdinaryWateringIntoPreparation()
    {
        var f = new Case(null);
        Assert.Same(f.Native, Bind(f.Trace(), f.Plan, f.Row));
    }

    private static object? Invoke(CropTeacherManagedProductionTrace trace, MoneyRevenueCropNativeProductionRunPlan plan,
        CropTeacherManagedProductionTransition row) => typeof(CropTeacherManagedSeedOwner).Assembly
        .GetType("StardewAI.LiveTrainingLoop.CropTeacherProductionEvidence")!
        .GetMethod("BindNativePreparation", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, new object[] { trace, plan, row });
    private static MoneyRevenueCropNativeLifecycleTransitionRequest Bind(CropTeacherManagedProductionTrace trace,
        MoneyRevenueCropNativeProductionRunPlan plan, CropTeacherManagedProductionTransition row) =>
        (MoneyRevenueCropNativeLifecycleTransitionRequest)Invoke(trace, plan, row)!;
    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();

    private sealed class Case
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "native-refill-reference-" + Guid.NewGuid().ToString("N"));
        private readonly string[] paths;
        private readonly string recordPath;
        internal JsonObject Record { get; }
        internal MoneyRevenueCropNativeProductionRunPlan Plan { get; }
        internal MoneyRevenueCropNativeLifecycleTransitionRequest Native { get; }
        internal CropTeacherManagedProductionTransition Row => new(68, "watering_maintenance", Native);

        internal Case(string? control)
        {
            Directory.CreateDirectory(root);
            paths = new[] { "request", "ledger", "model", "queue", "before", "receipt", "after", "native-plan" }
                .Select(name => Path.Combine(root, name + ".json")).ToArray();
            foreach (var path in paths) File.WriteAllText(path, "{}");
            Plan = new() { RequestPath = paths[0] };
            Native = new("crop_actions", 8, paths[3], paths[4], paths[5], paths[6], "run.original", "executor.original", "candidate.original");
            recordPath = Path.Combine(root, "operation-000068-intent.json");
            Record = new JsonObject
            {
                ["sequence"] = 68, ["operation_kind"] = "watering_maintenance", ["maintenance_execution_protocol"] = "original_loop.v1",
                ["maintenance_control_kind"] = control, ["maintenance_transition"] = JsonSerializer.SerializeToNode(Native, JsonOptions),
                ["active_commitment_ids"] = new JsonArray("crop.original"), ["commitment_id"] = "crop.original",
                ["support_step_id"] = "original.step.68", ["before_ledger_path"] = paths[1], ["original_model_action_path"] = paths[2]
            };
        }

        internal CropTeacherManagedProductionTrace Trace(string? fault = null)
        {
            File.WriteAllText(recordPath, Record.ToJsonString(JsonOptions));
            var hashes = paths.Append(recordPath).ToDictionary(path => path, Hash);
            if (fault == "request_unpinned") hashes.Remove(paths[0]);
            if (fault == "ledger_unpinned") hashes.Remove(paths[1]);
            if (fault == "model_unpinned") hashes.Remove(paths[2]);
            return (CropTeacherManagedProductionTrace)Activator.CreateInstance(typeof(CropTeacherManagedProductionTrace),
                BindingFlags.NonPublic | BindingFlags.Instance, null,
                new object[] { paths[7], paths[6], paths[1], new[] { Row }, hashes }, null)!;
        }
    }
}
