using System.Text.Json.Nodes;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RefillResourceEvidenceReusesOriginalCompiledFramesAndOneObservationRead(bool repair)
    {
        using var f = new RefillTraceFixture(repair, completeSnapshotInventory: true);
        await f.Build();
        var old = f.OpenWithSnapshotInventoryContinuity();
        var current = f.OpenWithResourceEvidence();
        Assert.Empty(old.ResourceEvidence);
        Assert.True(current.SnapshotInventoryContinuityVerified);
        Assert.Equal(f.Approach.Queue.Items.Length, current.ResourceEvidence.Count);
        Assert.Equal(old.InputHashes.OrderBy(pair => pair.Key), current.InputHashes.OrderBy(pair => pair.Key));
        for (int index = 0; index < current.Steps.Count; index++)
        {
            var row = current.ResourceEvidence[index];
            Assert.Equal(current.Steps[index].QueueItemId, row.QueueItemId);
            Assert.Equal(current.Steps[index].OptionId, row.OptionId);
            Assert.Same(current.Steps[index].Observation, row.Evidence.Observation);
            Assert.Empty(row.Evidence.Admissions);
            Assert.Equal(0L, row.Evidence.CurrentActionProductionCreditUnits);
            Assert.False(row.Evidence.ProducerProvenanceVerified);
        }
        AssertNoRefillSnapshotCredit(current);
        current.ValidateInputsUnchanged();
        File.AppendAllText(f.Journals[0] + ".gz", "changed");
        Assert.Throws<InvalidDataException>(current.ValidateInputsUnchanged);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RefillResourceEvidenceCannotReclassifyAnUnobservedSnapshotGain(bool repair)
    {
        using var f = new RefillTraceFixture(repair, completeSnapshotInventory: true);
        await f.Build();
        _ = f.OpenWithResourceEvidence();
        var path = f.Results[^1]; var result = JsonNode.Parse(File.ReadAllText(path))!;
        result["action_origin_observation"]!["inventory_before"]![1]!["quantity"] = 6;
        result["action_origin_observation"]!["inventory_after"]![1]!["quantity"] = 6;
        Fixture.Save(path, result);
        Assert.NotEmpty(f.Open().Steps);
        var error = Assert.Throws<InvalidDataException>(() => f.OpenWithResourceEvidence());
        Assert.Contains("outside the captured native interval", error.Message);
    }
}
