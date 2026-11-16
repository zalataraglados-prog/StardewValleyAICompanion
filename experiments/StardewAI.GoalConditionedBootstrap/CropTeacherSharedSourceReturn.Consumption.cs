using System.Text.Json.Serialization;

namespace StardewAI.GoalConditionedBootstrap;

public sealed partial class CropTeacherSharedSourceReturn
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public CropTeacherSharedConsumptionSettlementReference? ConsumptionSettlement { get; }

    public static CropTeacherSharedSourceReturn OpenAfterPreparedCookingConsumption(string runRequestPath, string sourcePath,
        string ledgerPath, IReadOnlyCollection<string> commitmentIds, MoneyRevenueCropNativeLifecycleTransitionRequest transition,
        string stepId, CropTeacherSharedConsumptionSettlementReference reference, string retainedReferencePath, bool product) =>
        new(runRequestPath, sourcePath, ledgerPath, commitmentIds, transition, stepId, null, true, retainedReferencePath,
            product, false, false, reference ?? throw new ArgumentNullException(nameof(reference)));
}
