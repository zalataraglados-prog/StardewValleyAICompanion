using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.LiveTrainingLoop;

public sealed class CropTeacherManagedSeedScopes
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly string stepJson;
    private readonly Action validateInputs;

    private CropTeacherManagedSeedScopes(CropTeacherRunStep step, Action validateInputs,
        CropTeacherSeedPurchaseSupport? purchase = null, CropTeacherSeedTransferSupport? transfer = null,
        CropTeacherSeedPlantingSupport? planting = null)
    {
        stepJson = JsonSerializer.Serialize(step, JsonOptions);
        this.validateInputs = validateInputs;
        Purchase = purchase;
        Transfer = transfer;
        Planting = planting;
    }

    public CropTeacherSeedPurchaseSupport? Purchase { get; }
    public CropTeacherSeedTransferSupport? Transfer { get; }
    public CropTeacherSeedPlantingSupport? Planting { get; }

    public static CropTeacherManagedSeedScopes Open(string runRequestPath, string snapshotPath, string ledgerPath,
        CropTeacherRunStep step, string supportStepId, string? priorVerifiedSnapshotPath = null)
    {
        if (step.Stage == CropTeacherRunStageKinds.AcquireSeed)
        {
            var scope = CropTeacherSeedPurchaseSupport.Open(runRequestPath, snapshotPath, ledgerPath,
                step.CommitmentId, supportStepId, priorVerifiedSnapshotPath);
            var intent = scope.AcquisitionIntent;
            if (step.TotalDay != scope.TotalDay || step.SeedQualifiedItemId != intent.SeedQualifiedItemId ||
                step.ShopId != intent.ShopId || step.MaxUnitPrice != intent.MaxUnitPrice || step.RemainingQuantity <= 0 ||
                step.RemainingQuantity > scope.MaximumPurchaseQuantity ||
                step.ActionBatch is not null || step.SeedTransferIntent is not null || step.SeedReservationId is not null)
                throw new InvalidDataException("Managed purchase step differs from its original acquisition intent.");
            scope.ValidateInputsUnchanged();
            return new(step, scope.ValidateInputsUnchanged, purchase: scope);
        }
        if (step.Stage == CropTeacherRunStageKinds.TransferSeedSupply)
        {
            if (string.IsNullOrWhiteSpace(step.SeedReservationId) || step.SeedTransferIntent is null || step.ActionBatch is not null)
                throw new InvalidDataException("Managed transfer step lacks its original selected chest claim.");
            var scope = CropTeacherSeedTransferSupport.Open(runRequestPath, snapshotPath, ledgerPath,
                step.CommitmentId, supportStepId, step.SeedReservationId, priorVerifiedSnapshotPath);
            var intent = scope.PreparationRequest.MaterialTransferPreparation!.Intent;
            if (step.TotalDay != scope.TotalDay || step.SeedQualifiedItemId != intent.QualifiedItemId ||
                intent.Quantity > step.RemainingQuantity || !Equal(step.SeedTransferIntent, intent))
                throw new InvalidDataException("Managed transfer step differs from its original owned seed projection.");
            return new(step, scope.ValidateInputsUnchanged, transfer: scope);
        }
        if (step.Stage == CropTeacherRunStageKinds.ExecuteCropBatch && step.ActionBatch?.OptionId == "executor.plant_seed")
        {
            if (priorVerifiedSnapshotPath is not null || step.SeedTransferIntent is not null)
                throw new InvalidDataException("Managed planting requires its settled current source, not an idle recapture.");
            var scope = CropTeacherSeedPlantingSupport.Open(runRequestPath, snapshotPath, ledgerPath, step.CommitmentId, supportStepId);
            if (step.TotalDay != scope.TotalDay || step.ActionBatch.CommitmentId != step.CommitmentId ||
                step.ActionBatch.SeedQualifiedItemId != step.SeedQualifiedItemId)
                throw new InvalidDataException("Managed planting step differs from its original crop batch.");
            return new(step, scope.ValidateInputsUnchanged, planting: scope);
        }
        throw new InvalidDataException("Managed seed scope requires an original purchase, transfer or planting step.");
    }

    public void ValidateStep(CropTeacherRunStep step)
    {
        validateInputs();
        if (!JsonNode.DeepEquals(JsonNode.Parse(stepJson), JsonSerializer.SerializeToNode(step, JsonOptions)))
            throw new InvalidDataException("Managed seed scope cannot be reused for a different or changed control step.");
    }

    private static bool Equal<T>(T left, T right) => JsonNode.DeepEquals(
        JsonSerializer.SerializeToNode(left, JsonOptions), JsonSerializer.SerializeToNode(right, JsonOptions));
}
