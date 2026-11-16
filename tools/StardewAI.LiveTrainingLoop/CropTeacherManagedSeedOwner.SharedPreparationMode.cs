using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.LiveTrainingLoop;

public sealed record CropTeacherSharedPreparationDecision(string SnapshotPath, string PlanPath, string RankingPath,
    int SelectedQueueIndex, string[] OwnedCandidateIds, CropTeacherDailyDecisionReference? DecisionReference = null)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? OriginalLedgerPath { get; init; }
    internal CropTeacherSharedSourceSelection WithOutputs(string plan, string queue, string evidence) =>
        new(SnapshotPath, PlanPath, RankingPath, plan, queue, evidence, SelectedQueueIndex, OwnedCandidateIds)
            { DecisionReference = DecisionReference };
}

public sealed partial class CropTeacherManagedSeedOwner
{
    private sealed record SharedPreparationMode(bool Cooking)
    {
        internal static readonly SharedPreparationMode Transfer = new(false);
        internal static readonly SharedPreparationMode Consumption = new(true);
        internal string AdmissionProtocol => Cooking ? "selected_cooking_consumption.v1" : "selected_work_yield.v3";
        internal string ExecutionProtocol => Cooking ? "selected_cooking_yield.v1" : "selected_work_yield.v2";
        internal string DecisionKey => Cooking ? "shared_consumption_decision" : "shared_transfer_decision";
        internal string RequestKey => Cooking ? "shared_consumption_request" : "shared_transfer_request";
        internal string ReferenceKey => Cooking ? "original_consumption_preparation" : "original_transfer_preparation";
        internal string IntentSchema => Cooking ? "crop_teacher_managed_shared_consumption_support.v1" : "crop_teacher_managed_shared_transfer_support.v1";
        internal string PreparedSchema => Cooking ? "crop_teacher_managed_shared_consumption_preparation.v1" : "crop_teacher_managed_shared_transfer_preparation.v1";
        internal string AcceptedPreparationHashKey => Cooking ? "shared_consumption_prepared_sha256" : "shared_transfer_prepared_sha256";
        internal JsonNode ReferenceNode(SharedPreparationReference reference) => Cooking
            ? JsonSerializer.SerializeToNode(reference.Consumption, JsonOptions)!
            : JsonSerializer.SerializeToNode(reference.Transfer, JsonOptions)!;
        internal SharedPreparationReference ReadReference(JsonNode? node)
        {
            if (node is null) throw new InvalidDataException("Shared preparation lacks its typed original RPC reference.");
            if (Cooking)
            {
                var value = node.Deserialize<CropTeacherSharedConsumptionPreparationReference>(JsonOptions)!;
                return new(value.RequestPath, value.ReplyPath, value.JournalPath, value.BackendUrl);
            }
            var transfer = node.Deserialize<CropTeacherSharedTransferPreparationReference>(JsonOptions)!;
            return new(transfer.RequestPath, transfer.ReplyPath, transfer.JournalPath, transfer.BackendUrl);
        }
        internal CropTeacherSharedSourceSelection Bind(CropTeacherSharedSourceSelection selection, SharedPreparationReference reference) => Cooking
            ? selection with { InventoryConsumptionPreparation = reference.Consumption }
            : selection with { MaterialTransferPreparation = reference.Transfer };
    }

    private sealed record SharedPreparationReference(string RequestPath, string ReplyPath, string JournalPath, string BackendUrl)
    {
        internal CropTeacherSharedTransferPreparationReference Transfer => new(RequestPath, ReplyPath, JournalPath, BackendUrl);
        internal CropTeacherSharedConsumptionPreparationReference Consumption => new(RequestPath, ReplyPath, JournalPath, BackendUrl);
    }

    private static bool IsSharedCookingPreparation(JsonObject record) =>
        record["shared_source_execution_protocol"]?.GetValue<string>() == SharedPreparationMode.Consumption.AdmissionProtocol;
    private static bool IsSharedPortfolioPreparation(JsonObject record) => IsSharedTransferPreparation(record) || IsSharedCookingPreparation(record);
    private static SharedPreparationMode PreparationMode(JsonObject record) => IsSharedCookingPreparation(record)
        ? SharedPreparationMode.Consumption : IsSharedTransferPreparation(record) ? SharedPreparationMode.Transfer
        : throw new InvalidDataException("Unsupported shared portfolio preparation protocol.");
}
