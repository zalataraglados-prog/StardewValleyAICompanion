using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using StardewAI.Contracts.State;

namespace StardewAI.GoalConditionedBootstrap;

public sealed class MoneyRevenueCropSupplementalEvidenceRequest
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } = "money_revenue_crop_supplemental_evidence_request.v1";
    [JsonPropertyName("teacher_dispatch_manifest_path")]
    public string TeacherDispatchManifestPath { get; set; } = string.Empty;
    [JsonPropertyName("accepted_production_evidence")]
    public MoneyRevenueArtifactBinding AcceptedProductionEvidence { get; set; } = new("production", "", "");
    [JsonPropertyName("accepted_manual_closeout")]
    public MoneyRevenueArtifactBinding AcceptedManualCloseout { get; set; } = new("manual_closeout", "", "");
    [JsonPropertyName("accepted_revenue_recurrence")]
    public MoneyRevenueArtifactBinding AcceptedRevenueRecurrence { get; set; } = new("revenue_recurrence", "", "");
}

public static class MoneyRevenueCropSupplementalEvidenceSummary
{
    // This joins owner-accepted certificates, not a replay or a new automatic-owner acceptance.
    public static JsonObject Build(string requestPath)
    {
        var pins = new Dictionary<string, string>(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        string Pin(string path, string? expected = null)
        {
            Require(Path.IsPathFullyQualified(path) && File.Exists(path), "Supplemental evidence path must name an existing absolute file.");
            var full = Path.GetFullPath(path);
            var hash = CurrentTeacherFrontierSupport.HashFile(full);
            Require(expected is null || IsLowerSha256(expected) && hash == expected, "Supplemental evidence hash changed: " + full);
            Require(!pins.TryGetValue(full, out var prior) || prior == hash, "Supplemental evidence has conflicting bindings.");
            pins[full] = hash;
            return full;
        }
        T Read<T>(string path) => CurrentTeacherFrontierSupport.Read<T>(path, "crop supplemental evidence");
        var fullRequest = Pin(Path.GetFullPath(requestPath));
        var request = Read<MoneyRevenueCropSupplementalEvidenceRequest>(fullRequest);
        Require(request.SchemaVersion == "money_revenue_crop_supplemental_evidence_request.v1" &&
            EqualJson(Read<JsonElement>(fullRequest), request), "Supplemental evidence request shape differs.");
        JsonObject Accepted(MoneyRevenueArtifactBinding binding, string role)
        {
            Require(binding is not null && binding.Role == role, "Supplemental accepted evidence role differs.");
            return Read<JsonObject>(Pin(binding!.Path, binding.Sha256));
        }
        var dispatchPath = Pin(request.TeacherDispatchManifestPath);
        var dispatch = MoneyRevenueCropTeacherDispatchBuilder.ReadVerified(dispatchPath);
        var production = Accepted(request.AcceptedProductionEvidence, "production");
        var manual = Accepted(request.AcceptedManualCloseout, "manual_closeout");
        Accepted(request.AcceptedRevenueRecurrence, "revenue_recurrence");
        var revenue = Read<MoneyRevenueRecurrenceProofReceipt>(request.AcceptedRevenueRecurrence.Path);
        Require(Text(production, "schema_version") == "crop_teacher_managed_production_evidence.v1" &&
            Flag(production, "native_crop_lifecycle_verified") && Flag(production, "whole_wave_output_retention_verified") &&
            !Flag(production, "terminal_settlement_verified") && !Flag(production, "formal_product_training_authorized") &&
            Same(Text(production, "native_run_plan_path"), dispatch.NativeRunPlanPath), "Accepted production does not bind the current Teacher dispatch.");
        Require(Text(manual, "schema_version") == "stardewai.manual_native_closeout.v1" &&
            Text(manual, "verification_source") == "controller_manual_supplement_using_original_native_reader" &&
            Flag(manual, "manual_native_business_closeout_verified") && Flag(manual, "original_native_queue_reader_verified") &&
            Flag(manual, "native_save_verified") && Flag(manual, "fresh_process_reload_verified") &&
            Flag(manual, "source_save_unchanged") && !Flag(manual, "original_owner_modified") &&
            !Flag(manual, "automatic_owner_resume_verified") && !Flag(manual, "complete_managed_runtime_verified") &&
            !Flag(manual, "teacher_preference_label_eligible") && !Flag(manual, "formal_product_training_authorized"),
            "Manual supplement is incomplete or promotes automatic/Teacher/training acceptance.");
        var boundaryPath = Pin(Text(manual, "source_boundary_path"), Text(manual, "source_boundary_sha256"));
        var boundary = Read<JsonObject>(boundaryPath);
        var productionInputs = production["input_artifact_hashes"]!.AsObject();
        Require(Text(boundary, "schema_version") == "crop_teacher_owned_overnight_save_boundary.v1" &&
            Flag(boundary, "native_save_boundary_verified") && !Flag(boundary, "formal_product_training_authorized") &&
            Text(boundary, "run_id") == Text(manual, "source_run_id") &&
            Text(boundary["after_save"]!.AsObject(), "sha256") == Text(manual, "source_save_sha256") &&
            productionInputs[boundaryPath]?.GetValue<string>() == pins[boundaryPath], "Supplement source save boundary is not the accepted production boundary.");
        var sequence = manual["source_accepted_save_sequence"]!.GetValue<long>();
        var transitions = production["owner_transitions"]!.AsArray().Select(row => row!.AsObject()).ToArray();
        var saved = transitions.Where(row => row["ownerSequence"]!.GetValue<long>() == sequence).ToArray();
        Require(saved.Length == 1 && Text(saved[0], "operationKind") == "overnight_control" &&
            Same(Text(saved[0]["nativeTransition"]!.AsObject(), "after_snapshot_path"), Text(boundary, "after_snapshot_path")) &&
            Text(saved[0]["nativeTransition"]!.AsObject(), "run_id") == Text(manual, "source_run_id") &&
            manual["old_unsaved_tail_not_resumed"]!.AsArray().Select(row => row!.GetValue<long>()).SequenceEqual(
                transitions.Where(row => row["ownerSequence"]!.GetValue<long>() > sequence).Select(row => row["ownerSequence"]!.GetValue<long>())),
            "Supplement source sequence or retained unsaved tail differs.");
        var prefix = revenue.Prefix;
        Require(revenue.SchemaVersion == "money_revenue_recurrence_proof_receipt.v1" && revenue.ProofVerified &&
            revenue.Status == "verified_revenue_recurrence_proof" && !revenue.FormalProductTrainingAuthorized && prefix is not null &&
            revenue.Transitions.Length == 1 && revenue.Transitions[0].ProofKind == MoneyRevenueRecurrenceProofKinds.NativeShippingSettlement,
            "Accepted supplemental revenue is not one verified native shipping settlement.");
        var source = Read<SnapshotEnvelope>(Pin(Read<PlanningHandoffCaptureManifest>(dispatch.CaptureManifestPath).SourceSnapshotPath));
        Require(prefix!.SaveId == source.SaveId.Value && prefix.PlayerId == source.PlayerId.Value &&
            prefix.SaveId == Text(boundary, "save_id") && prefix.PlayerId == Text(boundary, "player_id") &&
            prefix.InitialTotalDay == boundary["to_total_day"]!.GetValue<int>() && prefix.FinalTotalDay == prefix.InitialTotalDay + 1 &&
            prefix.InitialMoney == manual["initial_money"]!.GetValue<int>() && prefix.FinalMoney == manual["final_money"]!.GetValue<int>() &&
            prefix.TotalRevenue == manual["gross_revenue"]!.GetValue<int>() && prefix.TotalRevenue > 0 &&
            prefix.TotalRevenue == checked(manual["shipped_units"]!.GetValue<int>() * manual["unit_price"]!.GetValue<int>()) &&
            manual["harvested_units"]!.GetValue<int>() == manual["shipped_units"]!.GetValue<int>(), "Supplement actor/day/revenue differs from accepted native outcome.");
        var sleep = manual["manual_action_queues"]!.AsArray()[^1]!.AsObject();
        var manualInputs = manual["input_artifact_hashes"]!.AsObject();
        foreach (var (role, field) in new[] { ("queue", "queue_path"), ("before_snapshot", "before_path"),
                     ("execution_receipt", "receipt_path"), ("after_snapshot", "after_path") })
        {
            var artifact = revenue.Transitions[0].Artifacts.Where(row => row.Role == role).ToArray();
            Require(artifact.Length == 1 && Same(artifact[0].Path, Text(sleep, field)) &&
                manualInputs[artifact[0].Path]?.GetValue<string>() == artifact[0].Sha256,
                "Revenue certificate is not bound to the accepted manual sleep: " + role);
        }
        foreach (var (path, hash) in pins) Require(CurrentTeacherFrontierSupport.HashFile(path) == hash, "Supplemental evidence changed during read.");
        return new JsonObject
        {
            ["schema_version"] = "money_revenue_crop_supplemental_evidence_summary.v1",
            ["status"] = "accepted_crop_evidence_summarized", ["evidence_mode"] = "manual_native_supplement",
            ["request_path"] = fullRequest, ["decision_id"] = dispatch.DecisionId,
            ["teacher_dispatch_action_count"] = dispatch.ActionCount, ["source_save_sequence"] = sequence,
            ["source_save_sha256"] = Text(manual, "source_save_sha256"),
            ["production_run_id"] = Text(manual, "source_run_id"), ["manual_run_id"] = Text(manual, "run_id"),
            ["gross_revenue"] = prefix.TotalRevenue, ["final_money"] = prefix.FinalMoney,
            ["accepted_production_evidence_reused"] = true, ["accepted_manual_closeout_reused"] = true,
            ["accepted_revenue_certificate_reused"] = true, ["production_replayed"] = false,
            ["native_dispatch_performed"] = false, ["owner_mutation_performed"] = false,
            ["automatic_owner_resume_verified"] = false, ["complete_managed_runtime_verified"] = false,
            ["teacher_comparison_verified"] = false, ["teacher_preference_label_eligible"] = false,
            ["formal_product_training_authorized"] = false,
            ["input_artifact_hashes"] = JsonSerializer.SerializeToNode(pins)
        };
    }

    private static string Text(JsonObject node, string field) => node[field]?.GetValue<string>()
        ?? throw new InvalidDataException("Missing supplemental evidence field: " + field);
    private static bool Flag(JsonObject node, string field) => node[field]?.GetValue<bool>()
        ?? throw new InvalidDataException("Missing supplemental evidence flag: " + field);
    private static bool Same(string left, string right) => string.Equals(Path.GetFullPath(left), Path.GetFullPath(right),
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
}
