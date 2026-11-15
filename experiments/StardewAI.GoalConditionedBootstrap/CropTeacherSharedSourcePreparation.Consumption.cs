using StardewAI.Contracts.Strategy;
using StardewAI.Contracts.Training;

namespace StardewAI.GoalConditionedBootstrap;

public sealed partial class CropTeacherSharedSourcePreparation
{
    public static CropTeacherSharedSourcePreparation OpenPreparedCookingConsumption(string runRequestPath, string sourcePath,
        string ledgerPath, IReadOnlyCollection<string> commitmentIds, string stepId, string modelPlanPath,
        MoneyRevenueCropNativeLifecycleTransitionRequest boundary, IReadOnlyDictionary<string, string> selectionHashes,
        CropTeacherSharedConsumptionPreparationReference preparation, string settlementReferencePath, bool product) =>
        new(runRequestPath, sourcePath, ledgerPath, commitmentIds, stepId, modelPlanPath, boundary, selectionHashes,
            null, settlementReferencePath, product, false, preparation ?? throw new ArgumentNullException(nameof(preparation)));

    public bool RequiresConsumptionSettlement => consumptionPreparation is not null;
    public CropTeacherSharedConsumptionPreparationReference? ConsumptionPreparation => consumptionPreparation;

    public ReservationPortfolioSupportingTransitionSettlementRequest ReadConsumptionSettlementRequest()
    {
        ValidateInputsUnchanged();
        Require(RequiresConsumptionSettlement, "Shared source yield has no prepared inventory consumption.");
        return CropTeacherSharedConsumption.CreateRequest(BaseLedger, ReadPreparedNativeTransition());
    }

    public StrategyCommitmentLedger VerifyConsumptionSettlementReply(ReservationPortfolioSupportingTransitionSettlementRequest request,
        ReservationPortfolioSupportingTransitionSettlementResult reply)
    {
        ValidateInputsUnchanged();
        Require(RequiresConsumptionSettlement, "Shared source yield has no prepared inventory consumption.");
        var result = CropTeacherSharedConsumption.VerifyReply(BaseLedger, ReadPreparedNativeTransition(), request, reply);
        ValidateInputsUnchanged();
        return result;
    }

    private CropTeacherSharedSourceReturn VerifyConsumptionTransition(MoneyRevenueCropNativeLifecycleTransitionRequest transition)
    {
        var reference = CurrentTeacherFrontierSupport.Read<CropTeacherSharedConsumptionSettlementReference>(settlementReferencePath!, "shared cooking settlement");
        Require(reference.Preparation == consumptionPreparation && reference.BackendUrl == consumptionPreparation!.BackendUrl,
            "Shared cooking changed its original preparation or backend.");
        return CropTeacherSharedSourceReturn.OpenAfterPreparedCookingConsumption(runRequestPath, SourcePath, ledgerPath,
            commitmentIds, transition, stepId, reference, settlementReferencePath!, productTransfer);
    }
}
