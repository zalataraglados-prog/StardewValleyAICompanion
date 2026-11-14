using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.Testing;

namespace StardewAI.Backend.Tests;

public sealed class MoneyCropSupplementalEvidenceSummaryTests
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    [Fact]
    public void SummarizesAcceptedRecordsWithoutReplayWritesOrAutomaticTrainingClaims()
    {
        var f = new Case();
        var before = f.Files();
        var summary = MoneyRevenueCropSupplementalEvidenceSummary.Build(f.RequestPath);
        Assert.Equal("manual_native_supplement", summary["evidence_mode"]!.GetValue<string>());
        Assert.Equal(1050, summary["gross_revenue"]!.GetValue<int>());
        Assert.Equal(1070, summary["final_money"]!.GetValue<int>());
        Assert.True(summary["accepted_production_evidence_reused"]!.GetValue<bool>());
        foreach (var field in new[] { "production_replayed", "native_dispatch_performed", "owner_mutation_performed",
                     "automatic_owner_resume_verified", "complete_managed_runtime_verified", "teacher_comparison_verified",
                     "teacher_preference_label_eligible", "formal_product_training_authorized" })
            Assert.False(summary[field]!.GetValue<bool>());
        Assert.Equal(before, f.Files());
    }

    [Theory]
    [InlineData("production")]
    [InlineData("manual_closeout")]
    [InlineData("revenue_recurrence")]
    public void RejectsChangedAcceptedCertificateRatherThanReplayingOrRepairingIt(string role)
    {
        var f = new Case();
        File.AppendAllText(f.Binding(role).Path, " ");
        var before = f.Files();
        var error = Assert.Throws<InvalidDataException>(() => MoneyRevenueCropSupplementalEvidenceSummary.Build(f.RequestPath));
        Assert.Contains("hash changed", error.Message);
        Assert.Equal(before, f.Files());
    }

    [Theory]
    [InlineData("automatic_owner_resume_verified")]
    [InlineData("complete_managed_runtime_verified")]
    [InlineData("teacher_preference_label_eligible")]
    [InlineData("formal_product_training_authorized")]
    [InlineData("original_owner_modified")]
    public void RejectsManualRecordPromotedIntoAutomaticOwnerOrTrainingEvidence(string field)
    {
        var f = new Case();
        f.Edit("manual_closeout", node => node[field] = true);
        var error = Assert.Throws<InvalidDataException>(() => MoneyRevenueCropSupplementalEvidenceSummary.Build(f.RequestPath));
        Assert.Contains("promotes", error.Message);
    }

    [Theory]
    [InlineData("plan")]
    [InlineData("sequence")]
    [InlineData("tail")]
    [InlineData("save_hash")]
    [InlineData("run")]
    [InlineData("actor")]
    [InlineData("revenue")]
    [InlineData("sleep")]
    public void RejectsCrossedPlanningSaveTailOrRevenueBindingsEvenWithFreshCertificateHashes(string fault)
    {
        var f = new Case();
        if (fault == "plan") f.Edit("production", node => node["native_run_plan_path"] = f.RequestPath);
        if (fault == "sequence") f.Edit("manual_closeout", node => node["source_accepted_save_sequence"] = 86);
        if (fault == "tail") f.Edit("manual_closeout", node => node["old_unsaved_tail_not_resumed"] = new JsonArray());
        if (fault == "save_hash") f.Edit("manual_closeout", node => node["source_save_sha256"] = new string('f', 64));
        if (fault == "run") f.Edit("manual_closeout", node => node["source_run_id"] = "foreign");
        if (fault == "actor") f.Edit("revenue_recurrence", node => node["prefix"]!["player_id"] = "foreign");
        if (fault == "revenue") f.Edit("manual_closeout", node => node["gross_revenue"] = 1093);
        if (fault == "sleep") f.Edit("revenue_recurrence", node => node["transitions"]![0]!["artifacts"]![0]!["path"] = f.RequestPath);
        var before = f.Files();
        Assert.Throws<InvalidDataException>(() => MoneyRevenueCropSupplementalEvidenceSummary.Build(f.RequestPath));
        Assert.Equal(before, f.Files());
    }

    private sealed class Case
    {
        private readonly string root;
        private readonly MoneyRevenueCropSupplementalEvidenceRequest request;
        internal string RequestPath { get; }

        internal Case()
        {
            var teacher = new MoneyCropTeacherFixture();
            root = Path.GetDirectoryName(teacher.SnapshotPath)!;
            var capture = MoneyCropTeacherCaptureFixture.Save(teacher, Path.Combine(root, "capture.json"));
            var dispatch = MoneyRevenueCropTeacherDispatchBuilder.Build(capture, Path.Combine(root, "dispatch"),
                "accepted.supplement", "2026-11-15T00:00:00Z");
            RequestPath = Path.Combine(root, "supplement-request.json");
            var boundaryPath = Save("boundary", new
            {
                schema_version = "crop_teacher_owned_overnight_save_boundary.v1", run_id = "accepted-original",
                save_id = teacher.SourceSnapshot.SaveId.Value, player_id = teacher.SourceSnapshot.PlayerId.Value,
                after_snapshot_path = teacher.SnapshotPath, to_total_day = 12,
                after_save = new { sha256 = new string('a', 64) },
                native_save_boundary_verified = true, formal_product_training_authorized = false
            });
            var production = Save("production", new
            {
                schema_version = "crop_teacher_managed_production_evidence.v1", native_run_plan_path = dispatch.NativeRunPlanPath,
                native_crop_lifecycle_verified = true, whole_wave_output_retention_verified = true,
                terminal_settlement_verified = false, formal_product_training_authorized = false,
                input_artifact_hashes = new Dictionary<string, string> { [boundaryPath] = Hash(boundaryPath) },
                owner_transitions = new[]
                {
                    new { ownerSequence = 85, operationKind = "overnight_control", nativeTransition = new
                        { after_snapshot_path = teacher.SnapshotPath, run_id = "accepted-original" } },
                    new { ownerSequence = 86, operationKind = "supporting_route", nativeTransition = new
                        { after_snapshot_path = teacher.SnapshotPath, run_id = "accepted-original" } }
                }
            });
            // These are synthetic accepted-record fixtures; the summary does not claim to revalidate their native execution.
            var artifacts = new[] { "queue", "before_snapshot", "execution_receipt", "after_snapshot" }
                .Select(role => new MoneyRevenueArtifactBinding(role, Path.Combine(root, role + ".json"), new string('b', 64))).ToArray();
            var manual = Save("manual", new
            {
                schema_version = "stardewai.manual_native_closeout.v1",
                verification_source = "controller_manual_supplement_using_original_native_reader",
                source_run_id = "accepted-original", run_id = "accepted-manual", source_boundary_path = boundaryPath,
                source_boundary_sha256 = Hash(boundaryPath), source_save_sha256 = new string('a', 64), source_accepted_save_sequence = 85,
                old_unsaved_tail_not_resumed = new[] { 86 }, manual_native_business_closeout_verified = true,
                original_native_queue_reader_verified = true, native_save_verified = true, fresh_process_reload_verified = true,
                source_save_unchanged = true, original_owner_modified = false, automatic_owner_resume_verified = false,
                complete_managed_runtime_verified = false, teacher_preference_label_eligible = false, formal_product_training_authorized = false,
                initial_money = 20, final_money = 1070, gross_revenue = 1050, harvested_units = 6, shipped_units = 6, unit_price = 175,
                manual_action_queues = new[] { new { queue_path = artifacts[0].Path, before_path = artifacts[1].Path,
                    receipt_path = artifacts[2].Path, after_path = artifacts[3].Path } },
                input_artifact_hashes = artifacts.ToDictionary(row => row.Path, row => row.Sha256)
            });
            var prefix = new MoneyRevenueRecurrencePrefixEvidence(teacher.SourceSnapshot.SaveId.Value!, teacher.SourceSnapshot.PlayerId.Value!,
                "before", "after", 12, 13, 20, 1070, 0, 1050, 1050, 0, 1, []);
            var revenue = Save("revenue", new MoneyRevenueRecurrenceProofReceipt
            {
                Status = "verified_revenue_recurrence_proof", ProofVerified = true, Prefix = prefix,
                Transitions = [new MoneyRevenueRecurrenceTransitionProofEvidence(0, "accepted-manual-sleep",
                    MoneyRevenueRecurrenceProofKinds.NativeShippingSettlement, null!, artifacts)]
            });
            request = new()
            {
                TeacherDispatchManifestPath = Path.Combine(root, "dispatch", "teacher-dispatch-manifest.json"),
                AcceptedProductionEvidence = new("production", production, Hash(production)),
                AcceptedManualCloseout = new("manual_closeout", manual, Hash(manual)),
                AcceptedRevenueRecurrence = new("revenue_recurrence", revenue, Hash(revenue))
            };
            WriteRequest();
        }

        internal MoneyRevenueArtifactBinding Binding(string role) => role switch
        {
            "production" => request.AcceptedProductionEvidence, "manual_closeout" => request.AcceptedManualCloseout,
            _ => request.AcceptedRevenueRecurrence
        };
        internal void Edit(string role, Action<JsonObject> change)
        {
            var binding = Binding(role);
            var node = JsonNode.Parse(File.ReadAllText(binding.Path))!.AsObject();
            change(node);
            File.WriteAllText(binding.Path, node.ToJsonString(Options));
            binding = binding with { Sha256 = Hash(binding.Path) };
            if (role == "production") request.AcceptedProductionEvidence = binding;
            else if (role == "manual_closeout") request.AcceptedManualCloseout = binding;
            else request.AcceptedRevenueRecurrence = binding;
            WriteRequest();
        }
        internal string[] Files() => Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal).Select(path => path + ":" + Hash(path)).ToArray();
        private void WriteRequest() => File.WriteAllText(RequestPath, JsonSerializer.Serialize(request, Options));
        private string Save(string name, object value)
        {
            var path = Path.Combine(root, name + ".json");
            File.WriteAllText(path, JsonSerializer.Serialize(value, Options));
            return path;
        }
    }

    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
}
