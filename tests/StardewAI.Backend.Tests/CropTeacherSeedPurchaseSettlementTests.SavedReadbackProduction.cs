using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AcceptedSavedReadbackConnectsTheNextOriginalRouteWithoutAddingAnAction(bool product)
    {
        using var f = new SavedReadbackOwnerFixture(productExecutor: product);
        await using var owner = await f.Night.Open();
        await f.CompleteNight(owner, SavedReadbackOwnerFixture.RefreshDeclaredLoadedFieldTicks);
        await f.Run(owner, await f.Admit(owner));
        var loadedTrace = await owner.ReadProductionTraceAsync();
        var readback = Assert.Single(loadedTrace.SavedContentReadbacks);
        Assert.DoesNotContain(loadedTrace.Transitions, row => row.OperationKind == "saved_content_readback");
        await f.Night.Farm(owner);
        var trace = await owner.ReadProductionTraceAsync();
        Assert.Equal(loadedTrace.Transitions.Count + 1, trace.Transitions.Count);
        var resumes = ProductionReadbackResumes(trace);
        var resume = Assert.Single(resumes);
        Assert.Equal(trace.Transitions.Count - 1, resume.TransitionIndex);
        Assert.True(JsonNode.DeepEquals(JsonSerializer.SerializeToNode(readback.Reference, JsonOptions),
            JsonSerializer.SerializeToNode(Assert.Single(resume.AcceptedSavedReadbacks!), JsonOptions)));
        var previous = trace.Transitions[resume.TransitionIndex - 1].NativeTransition;
        var next = trace.Transitions[resume.TransitionIndex].NativeTransition;
        var boundary = CropTeacherAcceptedSavedReadbackVerifier.Verify(previous.AfterSnapshotPath, next.BeforeSnapshotPath,
            previous.RunId, resume.AcceptedSavedReadbacks!);
        Assert.Equal(64, boundary.PersistentStateSha256.Length);
        Assert.Single(boundary.AcceptanceSha256s);
        var proof = Assert.IsType<MoneyRevenueCropNativeLifecycleCheckpointResumeProof>(LifecycleInvoke(
            "ValidateAcceptedSavedReadbackResume", resume.TransitionIndex, LifecycleInvoke("LoadTransition", previous),
            LifecycleInvoke("LoadTransition", next), resume.AcceptedSavedReadbacks!));
        Assert.Equal("accepted_native_saved_content_readback", proof.BoundaryKind);
        Assert.Equal(boundary.AcceptanceSha256s, proof.SavedReadbackAcceptanceSha256s);
        var old = Assert.Throws<TargetInvocationException>(() => LifecycleInvoke("ValidateCheckpointResume", resume.TransitionIndex,
            LifecycleInvoke("LoadTransition", previous), LifecycleInvoke("LoadTransition", next)));
        Assert.Contains("normalized save state", Assert.IsType<InvalidDataException>(old.InnerException).Message);
        var requestPath = Path.Combine(f.Witness.Root, "old-lifecycle-with-new-readback.json");
        foreach (var version in Enumerable.Range(4, 6))
        {
            Fixture.Save(requestPath, new MoneyRevenueCropNativeLifecycleProofRequest
            {
                SchemaVersion = "money_revenue_crop_native_lifecycle_proof_request.v" + version, CommitmentId = Id,
                Transitions = trace.Transitions.Select(row => row.NativeTransition).ToArray(), CheckpointResumes = resumes
            });
            Assert.Contains("explicit lifecycle v10", Assert.Throws<InvalidDataException>(() =>
                MoneyRevenueCropNativeLifecycleProofBuilder.Build(requestPath)).Message);
        }
        Assert.Equal(1, f.Night.NativeNightCalls);
        Assert.Equal(5, f.Night.Shared.Runtime.Handler.Commits);
        trace.ValidateInputsUnchanged();
    }

    [Theory]
    [InlineData("missing_input")]
    [InlineData("hash")]
    [InlineData("unaccepted")]
    [InlineData("run")]
    [InlineData("next_source")]
    public async Task AcceptedSavedReadbackRejectsChangedAuthorityOrWrongNativeContinuation(string fault)
    {
        using var f = new SavedReadbackOwnerFixture(productExecutor: true);
        await using var owner = await f.Night.Open();
        await f.CompleteNight(owner);
        await f.Run(owner, await f.Admit(owner));
        var trace = await owner.ReadProductionTraceAsync();
        var reference = Assert.Single(trace.SavedContentReadbacks).Reference;
        var previous = trace.Transitions[^1].NativeTransition;
        var hashes = reference.InputArtifactHashes.ToDictionary(pair => pair.Key, pair => pair.Value);
        if (fault == "missing_input") hashes.Remove(reference.ObservationPath);
        if (fault == "hash") hashes[reference.ObservationPath] = new string('0', 64);
        if (fault == "unaccepted")
        {
            var path = Path.Combine(f.Witness.Root, "not-an-accepted-readback.json");
            var accepted = JsonNode.Parse(File.ReadAllText(reference.AcceptancePath))!;
            accepted["native_reload_verified"] = false; Fixture.Save(path, accepted);
            hashes[path] = SiteTillNativeFixture.FileHash(path);
            reference = reference with { AcceptancePath = path };
        }
        reference = reference with { InputArtifactHashes = hashes };
        Assert.Throws<InvalidDataException>(() => CropTeacherAcceptedSavedReadbackVerifier.Verify(previous.AfterSnapshotPath,
            fault == "next_source" ? previous.AfterSnapshotPath : f.LoadedPath,
            fault == "run" ? "another-owner-run" : previous.RunId, [reference]));
        Assert.Equal(4, f.Night.Shared.Runtime.Handler.Commits);
        Assert.Equal(1, f.Night.NativeNightCalls);
    }

    private static MoneyRevenueCropNativeLifecycleCheckpointResumeRequest[] ProductionReadbackResumes(CropTeacherManagedProductionTrace trace) =>
        (MoneyRevenueCropNativeLifecycleCheckpointResumeRequest[])typeof(CropTeacherManagedSeedOwner).Assembly
            .GetType("StardewAI.LiveTrainingLoop.CropTeacherProductionEvidence")!
            .GetMethod("SavedReadbackResumes", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [trace, trace.Transitions.ToArray()])!;
}
