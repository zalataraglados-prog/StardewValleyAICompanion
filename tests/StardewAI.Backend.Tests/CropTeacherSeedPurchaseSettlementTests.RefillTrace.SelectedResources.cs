using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RefillSelectedResourceNeutralFramesRetainOriginalInputsAndEvidence(bool observed)
    {
        using var f = new RefillTraceFixture(repair: false, completeSnapshotInventory: true,
            resourceNeutral: true, observeNativeOutputs: observed);
        await f.Build();
        var compilation = f.Approach.Open();
        var runId = f.Approach.Watering.Shared.Runtime.LiveOptions.RunId;
        var old = CropTeacherRefillApproachNativeTrace.OpenResourceNeutral(compilation, f.ReceiptPath, f.AfterPath, runId);
        var trace = OpenSelectedRefillResources(f);
        Assert.False(old.SelectedResourceEffectsVerified);
        Assert.Empty(old.ResourceEffects);
        Assert.True(trace.SelectedResourceEffectsVerified);
        Assert.False(trace.ResourceNeutralBoundaryVerified);
        Assert.False(trace.SnapshotInventoryContinuityVerified);
        Assert.False(trace.PickupInventoryCaptureVerified);
        Assert.Equal(old.InputHashes.OrderBy(row => row.Key), trace.InputHashes.OrderBy(row => row.Key));
        Assert.Equal(f.Approach.Queue.Items.Select(row => row.QueueItemId), trace.ResourceEffects.Select(row => row.QueueItemId));
        Assert.Equal(f.Approach.Queue.Items.Select(row => row.OptionId), trace.ResourceEffects.Select(row => row.OptionId));
        Assert.Equal(observed ? trace.ResourceEffects.Count : 0, trace.Steps.Count);
        Assert.Equal(trace.Steps.Count, trace.ResourceEvidence.Count);
        Assert.Empty(trace.PickupInventory);
        for (var index = 0; index < trace.ResourceEffects.Count; index++)
        {
            var effects = trace.ResourceEffects[index].Effects;
            Assert.True(effects.ResourceNeutralBoundaryVerified);
            Assert.Null(effects.WildTree);
            Assert.Null(effects.Pickup);
            AssertSelectedRefillEffectsHaveNoCredit(effects);
            if (!observed) Assert.Null(effects.Evidence);
            else
            {
                Assert.NotNull(effects.Evidence);
                Assert.Same(effects.Evidence, trace.ResourceEvidence[index].Evidence);
                Assert.Same(effects.Evidence.Observation, trace.Steps[index].Observation);
                Assert.Equal(trace.ResourceEffects[index].QueueItemId, trace.Steps[index].QueueItemId);
                Assert.Equal(0L, effects.Evidence.ObservedPickupUnits);
                Assert.Equal(0L, effects.Evidence.CurrentActionProductionCreditUnits);
            }
        }
        Assert.False(trace.ResourceCreditAuthorized);
        Assert.False(trace.NativeOutcomeVerified);
        Assert.False(trace.FormalProductTrainingAuthorized);
        if (observed)
        {
            foreach (var legacy in new[] { f.Open(), f.OpenWithSnapshotInventoryContinuity(), f.OpenWithResourceEvidence() })
            {
                Assert.False(legacy.SelectedResourceEffectsVerified);
                Assert.Empty(legacy.ResourceEffects);
            }
        }
        trace.ValidateInputsUnchanged();
        File.AppendAllText(f.Journals[0] + ".gz", "changed");
        Assert.Throws<InvalidDataException>(trace.ValidateInputsUnchanged);
    }

    [Theory]
    [InlineData(false, "energy")]
    [InlineData(true, "energy")]
    [InlineData(false, "unknown")]
    [InlineData(true, "unknown")]
    public async Task RefillSelectedResourceNeutralMoveRefusesPersistentDrift(bool observed, string change)
    {
        using var f = new RefillTraceFixture(repair: false, completeSnapshotInventory: true,
            resourceNeutral: true, observeNativeOutputs: observed);
        await f.Build((after, _) =>
        {
            if (change == "energy") ChangeState(after, "player", node => node["energy"]!["value"] = 1);
            else after.State["unknown_persistent"] = JsonSerializer.SerializeToElement(new { status = "available", value = "changed" });
        });
        var error = Assert.Throws<InvalidDataException>(() => OpenSelectedRefillResources(f));
        Assert.Contains("persistent-resource boundary", error.Message);
    }

    [Fact]
    public async Task RefillSelectedResourceRequestedObservationCannotBeOmitted()
    {
        using var f = new RefillTraceFixture(repair: false, completeSnapshotInventory: true, resourceNeutral: true);
        await f.Build(emitObservations: false);
        var error = Assert.Throws<InvalidDataException>(() => OpenSelectedRefillResources(f));
        Assert.Contains("requested original move observation is unavailable", error.Message);
    }

    [Theory]
    [InlineData("endpoint")]
    [InlineData("identity")]
    [InlineData("unrequested")]
    public async Task RefillSelectedResourceKeepsOriginalEndpointAndIdentityGuards(string change)
    {
        using var f = new RefillTraceFixture(repair: false, completeSnapshotInventory: true,
            resourceNeutral: true, observeNativeOutputs: change != "unrequested");
        await f.Build(emitObservations: true);
        if (change != "unrequested")
        {
            var path = f.Results[^1];
            var result = JsonNode.Parse(File.ReadAllText(path))!;
            if (change == "endpoint") result["action_origin_observation"]!["after_game_tick"] = 999999;
            else result["action_origin_observation"]!["queue_item_id"] = "foreign";
            Fixture.Save(path, result);
        }
        Assert.Throws<InvalidDataException>(() => OpenSelectedRefillResources(f));
    }

    [Fact]
    public async Task RefillSelectedResourceGrassRepairCannotMasqueradeAsWildTreeProof()
    {
        using var f = new RefillTraceFixture(repair: true, completeSnapshotInventory: true, resourceNeutral: true);
        // Each synthetic repair frame must have a distinct transition, even without
        // the fixture's default persistent trace marker. Keep the final target intact.
        await f.Build((after, index) =>
        {
            if (index < f.Approach.Queue.Items.Length - 1)
                ChangeState(after, "player", node => node["tile_x"]!["value"] =
                    node["tile_x"]!["value"]!.GetValue<int>() + index + 1);
        });
        Assert.NotEmpty(f.Open().Steps.Where(row => row.OptionId == "executor.clear_obstacle"));
        Assert.Throws<InvalidDataException>(() => OpenSelectedRefillResources(f));
    }

    private static CropTeacherRefillApproachNativeTrace OpenSelectedRefillResources(RefillTraceFixture f) =>
        CropTeacherRefillApproachNativeTrace.OpenWithSelectedResourceEffects(f.Approach.Open(), f.ReceiptPath,
            f.AfterPath, f.Approach.Watering.Shared.Runtime.LiveOptions.RunId);

    private static void AssertSelectedRefillEffectsHaveNoCredit(NativeRefillFrameResourceEffects effects)
    {
        Assert.True(effects.SpecificPersistentResourceBoundaryVerified);
        Assert.False(effects.ProducerProvenanceVerified);
        Assert.False(effects.ResourceCreditAuthorized);
        Assert.False(effects.NativeOutcomeVerified);
        Assert.False(effects.FormalProductTrainingAuthorized);
    }
}
