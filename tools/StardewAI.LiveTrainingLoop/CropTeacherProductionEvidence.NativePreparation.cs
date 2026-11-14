using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.LiveTrainingLoop;

internal static partial class CropTeacherProductionEvidence
{
    private static MoneyRevenueCropNativeLifecycleTransitionRequest BindNativePreparation(CropTeacherManagedProductionTrace trace,
        MoneyRevenueCropNativeProductionRunPlan plan, CropTeacherManagedProductionTransition row)
    {
        var transition = row.NativeTransition;
        if (row.OperationKind == "owned_seed_planting") return transition with { TransitionKind = "crop_actions" };
        if (row.OperationKind != "watering_maintenance") return transition;
        var name = "operation-" + row.OwnerSequence.ToString("D6", System.Globalization.CultureInfo.InvariantCulture) + "-intent.json";
        var records = trace.InputHashes.Keys.Where(path => Path.GetFileName(path) == name).ToArray();
        if (records is not [var recordPath]) throw new InvalidDataException("Native refill reference lacks its unique original owner intent.");
        var inputs = new CropSeedRuntimeArtifacts(trace.InputHashes);
        var record = inputs.Read<JsonObject>(recordPath);
        var control = record["maintenance_control_kind"]?.GetValue<string>();
        if (control is null) return transition;
        if (control is not ("refill_approach" or "refill_approach_resources" or "refill_approach_native") ||
            record["sequence"]?.GetValue<int>() != row.OwnerSequence ||
            record["operation_kind"]?.GetValue<string>() != row.OperationKind ||
            record["maintenance_execution_protocol"]?.GetValue<string>() != "original_loop.v1" ||
            !JsonNode.DeepEquals(record["maintenance_transition"], JsonSerializer.SerializeToNode(transition, CropTeacherManagedSeedOwner.JsonOptions)))
            throw new InvalidDataException("Native refill reference differs from its original accepted role/transition.");
        string Text(string key) => record[key]?.GetValue<string>() ?? throw new InvalidDataException("Native refill reference lacks " + key);
        string Pinned(string path)
        {
            path = Path.GetFullPath(path);
            if (!trace.InputHashes.ContainsKey(path)) throw new InvalidDataException("Native refill reference escaped its original owner inputs.");
            inputs.Pin(path);
            return path;
        }
        var commitments = record["active_commitment_ids"]?.Deserialize<string[]>(CropTeacherManagedSeedOwner.JsonOptions);
        var commitment = Text("commitment_id");
        if (commitments is not { Length: > 0 } || commitments.Distinct(StringComparer.Ordinal).Count() != commitments.Length ||
            !commitments.Contains(commitment, StringComparer.Ordinal) || string.IsNullOrWhiteSpace(Text("support_step_id")))
            throw new InvalidDataException("Native refill reference changed its covered commitments or step identity.");
        return transition with { NativeRefillPreparation = new(Pinned(plan.RequestPath), Pinned(Text("before_ledger_path")),
            commitments, commitment, Text("support_step_id"), Pinned(Text("original_model_action_path")),
            control == "refill_approach_native" ? "native_engine_observed" :
                control == "refill_approach_resources" ? "native_selected_resource_effects" : "native_resource_neutral") };
    }
}
