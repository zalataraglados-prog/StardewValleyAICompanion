using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Fact]
    public async Task JointLifecycleConsumesSharedNativeMovementInsideTheOriginalWholeCropTrajectory()
    {
        var f = new LifecycleControlsFixture(combinedRefill: true); await f.Build();
        var rows = f.Rows.ToArray();
        var index = Array.FindIndex(rows, row => row.NativeRefillPreparation is not null);
        var original = rows[index];
        var binding = original.NativeRefillPreparation!;
        // Reuse declared synthetic Product movement as separately attributed shared work; no game claim.
        var shared = original with { TransitionKind = "shared_source_return", NativeRefillPreparation = null,
            ExecutorVersion = PolicyTrajectoryVersionPins.RuntimeTestHarnessExecutor };
        const string step = "declared-joint-source-movement";
        var scope = CropTeacherSharedSourceReturn.Open(binding.RunRequestPath, shared.BeforeSnapshotPath,
            binding.LedgerPath, binding.CoveredCommitmentIds, shared, step);
        rows[index] = shared with { NativeSharedSource = new(binding.RunRequestPath, binding.LedgerPath,
            binding.CoveredCommitmentIds, step, scope.InputHashes) };
        Assert.Empty((Array)f.ValidateJoint(rows));
        var loaded = rows.Select(LoadJointLifecycleLeaf).ToArray();
        var values = Array.CreateInstance(loaded[0].GetType(), loaded.Length);
        for (var item = 0; item < loaded.Length; item++) values.SetValue(loaded[item], item);
        Assert.Equal(6, LifecycleInvoke("CountOption", values, "executor.plant_seed"));
        Assert.Equal(6, LifecycleInvoke("CountOption", values, "executor.harvest_crop"));
        Assert.Equal(0, LifecycleInvoke("CountOption", values, "executor.move_to_tile"));
        foreach (var bad in new[] { rows.Where((_, position) => position != index).ToArray(),
            rows.Take(index).Concat(new[] { rows[index], rows[index] }).Concat(rows.Skip(index + 1)).ToArray() })
            Assert.IsType<InvalidDataException>(Assert.Throws<TargetInvocationException>(() => f.ValidateJoint(bad)).InnerException);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task JointLifecycleLeafReplaysTheOriginalForeignNativeSourceWithoutCropCredit(bool product)
    {
        var f = new SiteTillNativeFixture(productTransport: product);
        using var host = f.Files.F.Runtime;
        var transition = await SharedLifecycleTransition(f);
        var verified = MoneyRevenueCropNativeSharedSourceVerifier.Verify(transition);
        Assert.Equal(1, verified.VerifiedNativeStepCount);
        var loaded = LoadJointLifecycleLeaf(transition);
        Assert.True((bool)loaded.GetType().GetProperty("NativeSharedSource")!.GetValue(loaded)!);
        Assert.False((bool)loaded.GetType().GetProperty("NativeNonProduction")!.GetValue(loaded)!);
        var rows = Array.CreateInstance(loaded.GetType(), 1); rows.SetValue(loaded, 0);
        var count = typeof(MoneyRevenueCropNativeLifecycleProofBuilder).GetMethod("CountOption", BindingFlags.NonPublic | BindingFlags.Static)!;
        Assert.Equal(0, (int)count.Invoke(null, new object[] { rows, "executor.till_soil" })!);
        Assert.False(verified.CropProductionCreditAuthorized);
        Assert.False(verified.FormalProductTrainingAuthorized);
        var guard = (Action)loaded.GetType().GetProperty("SharedInputGuard")!.GetValue(loaded)!;
        guard();
        File.AppendAllText(f.JournalPath, "\n");
        Assert.Throws<InvalidDataException>(guard);
    }

    [Theory]
    [InlineData("missing_input")]
    [InlineData("wrong_hash")]
    [InlineData("wrong_run")]
    [InlineData("wrong_kind")]
    public async Task JointLifecycleLeafRejectsUnsealedOrMisboundForeignEvidence(string fault)
    {
        var f = new SiteTillNativeFixture();
        using var host = f.Files.F.Runtime;
        var transition = await SharedLifecycleTransition(f);
        var reference = transition.NativeSharedSource!;
        var hashes = reference.InputArtifactHashes.ToDictionary(pair => pair.Key, pair => pair.Value);
        if (fault == "missing_input") hashes.Remove(f.ResultPath);
        if (fault == "wrong_hash") hashes[f.ResultPath] = new string('a', 64);
        transition = transition with { NativeSharedSource = reference with { InputArtifactHashes = hashes } };
        if (fault == "wrong_run") transition = transition with { RunId = "different-native-run" };
        if (fault == "wrong_kind") transition = transition with { TransitionKind = "crop_actions" };
        Assert.Throws<InvalidDataException>(() => MoneyRevenueCropNativeSharedSourceVerifier.Verify(transition));
    }

    [Fact]
    public async Task LegacyLifecycleVersionsCannotSilentlyAdmitTheNewForeignSourceReference()
    {
        var f = new SiteTillNativeFixture();
        using var host = f.Files.F.Runtime;
        var transition = await SharedLifecycleTransition(f);
        foreach (var version in new[] { "v4", "v5" })
        {
            var path = Path.Combine(f.Files.F.Source.RuntimePath, "joint-lifecycle-old-" + version + ".json");
            Fixture.Save(path, new MoneyRevenueCropNativeLifecycleProofRequest
            {
                SchemaVersion = "money_revenue_crop_native_lifecycle_proof_request." + version,
                CommitmentId = Id, Transitions = [transition]
            });
            var error = Assert.Throws<InvalidDataException>(() => MoneyRevenueCropNativeLifecycleProofBuilder.Build(path));
            Assert.Contains("explicit lifecycle v6", error.Message);
        }
        var unbound = transition with { NativeSharedSource = null };
        var error2 = Assert.Throws<TargetInvocationException>(() => LoadJointLifecycleLeaf(unbound));
        Assert.IsType<InvalidDataException>(error2.InnerException);
    }

    [Fact]
    public async Task JointLifecycleForeignWorkCannotChangeAnOwnedCropProjection()
    {
        var f = new SiteTillNativeFixture(prepareSource: source => ChangeState(source.Before, "farm", farm =>
            farm["crops"] = new JsonObject { ["status"] = "available", ["value"] = new JsonArray() }));
        using var host = f.Files.F.Runtime;
        var loaded = LoadJointLifecycleLeaf(await SharedLifecycleTransition(f));
        var builder = typeof(MoneyRevenueCropNativeLifecycleProofBuilder);
        var contextType = builder.GetNestedType("LifecycleCommitmentContext", BindingFlags.NonPublic)!;
        var context = contextType.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .Single(ctor => ctor.GetParameters().Length == 6).Invoke(new object?[]
            { "declared-layout", new MoneyRevenueCropPlantingLayoutProof { LocationId = "Farm" }, null, null,
                new HashSet<(int, int)> { (0, 0) }, 0 });
        var contexts = Array.CreateInstance(contextType, 1); contexts.SetValue(context, 0);
        var check = builder.GetMethod("ValidateSharedCropBoundary", BindingFlags.NonPublic | BindingFlags.Static)!;
        check.Invoke(null, new[] { loaded, contexts });
        // An in-memory ownership-boundary negative, not fabricated native evidence or a game execution.
        var after = (SnapshotEnvelope)loaded.GetType().GetProperty("After")!.GetValue(loaded)!;
        ChangeState(after, "farm", farm => farm["crops"]!["value"]!.AsArray().Add(new JsonObject
        {
            ["location_id"] = "Farm", ["tile_x"] = 0, ["tile_y"] = 0,
            ["harvest_source_seed_id"] = "(O)745", ["harvest_item_qualified_id"] = "(O)400",
            ["phase_days"] = new JsonArray(1, 1), ["current_phase"] = 0, ["phase_count"] = 2,
            ["day_of_current_phase"] = 0, ["dead"] = false, ["fully_grown"] = false,
            ["ready_for_harvest"] = false, ["watered"] = false, ["days_until_next_harvest_if_watered"] = 2,
            ["regrow_days"] = null
        }));
        var error = Assert.Throws<TargetInvocationException>(() => check.Invoke(null, new[] { loaded, contexts }));
        Assert.IsType<InvalidDataException>(error.InnerException);
        Assert.Contains("owned crop tile", error.InnerException!.Message);
    }

    private static async Task<MoneyRevenueCropNativeLifecycleTransitionRequest> SharedLifecycleTransition(SiteTillNativeFixture f)
    {
        await f.Build();
        const string step = "declared-joint-lifecycle-source";
        var request = f.Transition with { TransitionKind = "shared_source_return" };
        var scope = CropTeacherSharedSourceReturn.Open(f.Files.F.Inputs.Plan.RequestPath, f.Files.F.Source.SourcePath,
            f.Files.F.Source.LedgerPath, f.Files.F.Inputs.Plan.CoveredCommitmentIds, request, step);
        return request with { NativeSharedSource = new(f.Files.F.Inputs.Plan.RequestPath, f.Files.F.Source.LedgerPath,
            f.Files.F.Inputs.Plan.CoveredCommitmentIds, step, scope.InputHashes) };
    }

    private static object LoadJointLifecycleLeaf(MoneyRevenueCropNativeLifecycleTransitionRequest transition) =>
        typeof(MoneyRevenueCropNativeLifecycleProofBuilder).GetMethod("LoadJointObservedTransition", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, new object[] { transition })!;
}
