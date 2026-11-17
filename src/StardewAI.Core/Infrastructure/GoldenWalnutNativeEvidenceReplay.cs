using System.Text.Json.Serialization;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Infrastructure;

// This replays frozen native sources, not the selected global route, wallet or formal admission.
public sealed record GoldenWalnutNativeEvidenceReplay(
    [property: JsonPropertyName("binding")] GoldenWalnutNativeEvidenceBinding Binding,
    [property: JsonPropertyName("source_schedule")] GoldenWalnutFreshSaveSourceSchedule SourceSchedule,
    [property: JsonPropertyName("gourmand_continuation")] GoldenWalnutGourmandContinuationReceipt? Continuation)
{
    [JsonPropertyName("training_label_eligible")]
    public bool TrainingLabelEligible => false;

    [JsonPropertyName("formal_product_training_authorized")]
    public bool FormalProductTrainingAuthorized => false;
}
