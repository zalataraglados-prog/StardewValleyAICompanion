using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Execution;
using StardewAI.Core.Strategy;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedTransferTests
{
    [Theory]
    [InlineData("missing-control")]
    [InlineData("unknown-control")]
    [InlineData("duplicate-control")]
    [InlineData("case-alias-control")]
    [InlineData("malformed-ids")]
    [InlineData("wrong-owner")]
    [InlineData("wrong-crop")]
    [InlineData("inactive-crop")]
    [InlineData("wrong-quantity")]
    [InlineData("mixed-machine")]
    [InlineData("mixed-custody")]
    [InlineData("missing-binding")]
    [InlineData("missing-preparation")]
    [InlineData("stale-state")]
    [InlineData("wrong-native-payload")]
    [InlineData("wrong-option")]
    [InlineData("missing-namespace")]
    public void CropDispatchRejectsDriftWithoutChangingAnyLedgerRows(string mutation)
    {
        using var fixture = new Fixture();
        var scope = fixture.Open();
        var marker = scope.VerifyPreparationReply(fixture.Prepare(scope));
        var ledger = marker.Ledger;
        var queue = fixture.Compile(marker);
        var item = Assert.Single(queue.Items);
        void Set(string name, string value) => item.NormalizedCommand.Parameters.Single(row => row.Name == name).Value = value;
        void Add(string name, string value) => item.NormalizedCommand.Parameters = item.NormalizedCommand.Parameters.Append(
            new SmallModelActionParameter { Name = name, Value = value }).ToArray();
        switch (mutation)
        {
            case "missing-control": item.NormalizedCommand.Parameters = item.NormalizedCommand.Parameters.Where(row => row.Name != "crop_seed_transfer_commitment_id").ToArray(); break;
            case "unknown-control": Add("crop_seed_transfer_guess", "true"); break;
            case "duplicate-control": Add("crop_seed_transfer_version", "1"); break;
            case "case-alias-control": Add("CROP_SEED_TRANSFER_VERSION", "1"); break;
            case "malformed-ids": Set("crop_seed_transfer_reservation_ids_json", "["); break;
            case "wrong-owner": Set("crop_seed_transfer_source_decision_id", "other-root"); break;
            case "wrong-crop": Set("crop_seed_transfer_commitment_id", "other-crop"); break;
            case "inactive-crop": ledger.CropPlantingCommitments.Single().Status = StrategyCommitmentStatuses.Completed; break;
            case "wrong-quantity": Set("quantity", "1"); break;
            case "mixed-machine": Add("acquisition_support_transition_kind", "machine_input_material_transfer"); break;
            case "mixed-custody": Add("museum_output_custody_transfer_version", "1"); break;
            case "missing-binding": item.NormalizedCommand.Parameters = item.NormalizedCommand.Parameters.Where(row => row.Name != PreparedMaterialTransferContract.BindingParameter).ToArray(); break;
            case "missing-preparation": ledger.PreparedMaterialTransfer = null; break;
            case "stale-state": item.NormalizedCommand.StateHash = "stale"; break;
            case "wrong-native-payload": Set("material_transfer_intent_json", "null"); break;
            case "wrong-option": item.NormalizedCommand.OptionId = "executor.wait_ticks"; break;
            case "missing-namespace": item.NormalizedCommand.Parameters = item.NormalizedCommand.Parameters.Where(row => !CropSeedMaterialTransferBinding.IsControlParameter(row.Name)).ToArray(); break;
        }
        var original = JsonSerializer.Serialize(ledger);
        var result = new ActionQueueDispatchReadinessService().Evaluate(queue, item, ledger, fixture.Before.StateHash);
        Assert.False(result.Ready);
        Assert.NotEmpty(result.BlockingReasons);
        Assert.Equal(original, JsonSerializer.Serialize(ledger));
    }

    [Fact]
    public void ALeftoverCropControlCannotDowngradeToUnreservedLegacyDispatch()
    {
        using var fixture = new Fixture();
        var scope = fixture.Open();
        var marker = scope.VerifyPreparationReply(fixture.Prepare(scope));
        var queue = fixture.Compile(marker);
        var item = Assert.Single(queue.Items);
        item.NormalizedCommand.GoalId = null;
        item.NormalizedCommand.Parameters = item.NormalizedCommand.Parameters.Where(row =>
            !CropSeedMaterialTransferBinding.IsControlParameter(row.Name) && row.Name != PreparedMaterialTransferContract.BindingParameter)
            .Append(new SmallModelActionParameter { Name = "crop_seed_transfer_version", Value = "1" }).ToArray();
        var result = new ActionQueueDispatchReadinessService().Evaluate(queue, item,
            new StrategyCommitmentLedger(), fixture.Before.StateHash);
        Assert.False(result.Ready);
        Assert.Contains("dispatch_crop_seed_transfer_binding_drifted", result.BlockingReasons);
    }

    [Fact]
    public void OriginalDispatchCanRebindOrdinaryMovementWithoutRepreparingResources()
    {
        using var fixture = new Fixture();
        var scope = fixture.Open();
        var marker = scope.VerifyPreparationReply(fixture.Prepare(scope));
        var queue = fixture.Compile(marker);
        var item = Assert.Single(queue.Items);
        var ledger = marker.Ledger;
        var original = JsonSerializer.Serialize(ledger);
        var hash = new string('c', 64);
        item.NormalizedCommand.StateHash = hash;
        var result = new ActionQueueDispatchReadinessService().Evaluate(queue, item, ledger, hash);
        Assert.True(result.Ready, string.Join(",", result.BlockingReasons));
        Assert.Equal(original, JsonSerializer.Serialize(ledger));
    }
}
