using System.Text.Json;
using StardewAI.Core.Infrastructure;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.LiveTrainingLoop;

public sealed record CropTeacherManagedProductionEvidence(string ManifestPath, string[] PlantingLayoutProofPaths,
    string[] DailyCapacityProofPaths, string[] NativeLifecycleProofPaths, string[] OutputRetentionProofPaths,
    string ManifestSha256 = "");

internal static partial class CropTeacherProductionEvidence
{
    internal static async Task<CropTeacherManagedProductionEvidence> BuildAsync(CropTeacherManagedProductionTrace trace,
        string output, Func<string, string, Task> write) =>
        await BuildCoreAsync(trace, Array.Empty<CropTeacherManagedSharedSourceReturn>(), output, write).ConfigureAwait(false);

    internal static Task<CropTeacherManagedProductionEvidence> BuildJointAsync(CropTeacherManagedJointProductionTrace trace,
        string output, Func<string, string, Task> write) => BuildCoreAsync(trace.CropTrace, trace.SharedSourceReturns, output, write);

    internal static Task<CropTeacherManagedProductionEvidence> BuildAcceptedJointAsync(CropTeacherManagedJointProductionTrace trace,
        string output, Func<string, string, Task> write, bool retainSaleStock = false)
    {
        var references = trace.SharedSourceReturns.Select(row => row.SourceReference).ToArray();
        var nativeWork = references.Any(row => row?.AppliedNativeWorkQueue == true);
        var consumption = references.Any(row => row?.SettledInventoryConsumption is not null);
        var transferred = references.Any(row => row?.SettledMaterialTransfer is not null || row?.AcceptedMarker is not null);
        return nativeWork ? BuildJointNativeWorkAsync(trace, output, write, retainSaleStock || consumption || transferred) :
            consumption ? BuildJointRetainingSharedInventoryEffectsAsync(trace, output, write) :
            transferred ? BuildJointRetainingTransferredSaleStockAsync(trace, output, write) :
            retainSaleStock ? BuildJointRetainingSaleStockAsync(trace, output, write) : BuildJointAsync(trace, output, write);
    }

    internal static Task<CropTeacherManagedProductionEvidence> BuildJointRetainingSaleStockAsync(CropTeacherManagedJointProductionTrace trace,
        string output, Func<string, string, Task> write) => BuildCoreAsync(trace.CropTrace, trace.SharedSourceReturns, output, write, retainSaleStock: true);

    internal static Task<CropTeacherManagedProductionEvidence> BuildJointRetainingTransferredSaleStockAsync(CropTeacherManagedJointProductionTrace trace,
        string output, Func<string, string, Task> write) => BuildCoreAsync(trace.CropTrace, trace.SharedSourceReturns, output, write,
            retainSaleStock: true, transferredStock: true);

    internal static Task<CropTeacherManagedProductionEvidence> BuildJointRetainingSharedInventoryEffectsAsync(CropTeacherManagedJointProductionTrace trace,
        string output, Func<string, string, Task> write) => BuildCoreAsync(trace.CropTrace, trace.SharedSourceReturns, output, write,
            retainSaleStock: true, transferredStock: true, inventoryEffects: true);

    internal static Task<CropTeacherManagedProductionEvidence> BuildJointNativeWorkAsync(CropTeacherManagedJointProductionTrace trace,
        string output, Func<string, string, Task> write, bool retainSaleStock = false) =>
        BuildCoreAsync(trace.CropTrace, trace.SharedSourceReturns, output, write,
            retainSaleStock, transferredStock: retainSaleStock, inventoryEffects: retainSaleStock, appliedNativeWork: true);

    private static async Task<CropTeacherManagedProductionEvidence> BuildCoreAsync(CropTeacherManagedProductionTrace trace,
        IReadOnlyList<CropTeacherManagedSharedSourceReturn> shared, string output, Func<string, string, Task> write,
        bool retainSaleStock = false, bool transferredStock = false, bool inventoryEffects = false, bool appliedNativeWork = false)
    {
        trace.ValidateInputsUnchanged();
        var (request, plan) = CropTeacherResumeLoader.ReadVerifiedPlan(trace.NativeRunPlanPath);
        var production = Read<MoneyRevenueCropProductionObligationPlan>(plan.CropProductionPlanPath);
        var ordered = MergeSharedSourcesCore(trace, shared, production.SnapshotPath, plan.RequestPath,
            transferredStock, inventoryEffects, appliedNativeWork);
        var firstPlanting = Array.FindIndex(ordered, row => row.OperationKind == "owned_seed_planting");
        if (firstPlanting < 0) throw new InvalidDataException("Managed crop production has no accepted original planting boundary.");
        // Acquisition/site preparation precede the original lifecycle's planting denominator.
        // Nothing inside that lifecycle is filtered out or relabelled as resource-neutral.
        var transitions = ordered.Skip(firstPlanting).Select(row => BindNativePreparation(trace, plan, row)).ToArray();
        var checkpointResumes = SavedReadbackResumes(trace, ordered.Skip(firstPlanting).ToArray());
        if (!SamePath(transitions[^1].AfterSnapshotPath, trace.AcceptedSnapshotPath))
            throw new InvalidDataException("Managed crop production does not reach its accepted terminal source.");
        var dayStarts = transitions.GroupBy(row => row.TotalDay).ToDictionary(group => group.Key, group =>
            (group.FirstOrDefault(row => row.TransitionKind == "crop_actions") ??
             group.FirstOrDefault(row => row.TransitionKind == "supporting_route_to_sleep") ?? group.First()).BeforeSnapshotPath);
        var requiredDays = production.Commitments.SelectMany(crop => crop.RequiredWateringTotalDays
            .Append(crop.PlantingTotalDay).Concat(crop.HarvestWaves.Select(wave => wave.HarvestTotalDay)))
            .Distinct().Order().ToArray();
        if (requiredDays.Any(day => !dayStarts.ContainsKey(day)))
            throw new InvalidDataException("Managed crop production is missing an original required day boundary.");
        if (Directory.Exists(output) || File.Exists(output))
            throw new IOException("Managed crop production evidence already exists; retain and inspect it instead of overwriting.");
        Directory.CreateDirectory(output);
        var artifacts = new CropSeedRuntimeArtifacts(trace.InputHashes);
        async Task<string> Emit<T>(string name, T value)
        {
            var path = Path.Combine(output, name + ".json");
            await write(path, JsonSerializer.Serialize(value, CropTeacherManagedSeedOwner.JsonOptions)).ConfigureAwait(false);
            if (!System.Text.Json.Nodes.JsonNode.DeepEquals(JsonSerializer.SerializeToNode(value, CropTeacherManagedSeedOwner.JsonOptions),
                    artifacts.Read<System.Text.Json.Nodes.JsonNode>(path)))
                throw new InvalidDataException("Managed crop production output differs from its original builder result.");
            return path;
        }
        var layouts = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 0; index < production.Commitments.Length; index++)
        {
            var crop = production.Commitments[index];
            var intent = request.LayoutIntents.Single(row => row.CommitmentId == crop.CommitmentId);
            var path = await Emit("layout-request-" + index, new MoneyRevenueCropPlantingLayoutProofRequest
            {
                CropProductionPlanPath = plan.CropProductionPlanPath, CommitmentId = crop.CommitmentId,
                PlantingSnapshotPath = dayStarts[crop.PlantingTotalDay], LocationId = intent.LocationId, Tiles = intent.Tiles
            }).ConfigureAwait(false);
            layouts[crop.CommitmentId] = await Emit("layout-proof-" + index,
                MoneyRevenueCropPlantingLayoutProofBuilder.Build(path)).ConfigureAwait(false);
        }
        var capacities = new List<string>(); var lifecycles = new List<string>(); var retentions = new List<string>();
        for (var index = 0; index < production.Commitments.Length; index++)
        {
            var crop = production.Commitments[index];
            var capacityRequest = await Emit("capacity-request-" + index, new MoneyRevenueCropDailyCapacityProofRequest
            {
                SchemaVersion = "money_revenue_crop_daily_capacity_proof_request.v2",
                CropProductionPlanPath = plan.CropProductionPlanPath, CommitmentId = crop.CommitmentId,
                PlantingLayoutProofPaths = layouts.Values.ToArray(), DailySnapshotPaths = requiredDays.Select(day => dayStarts[day]).ToArray(),
                NativeDayTransitions = transitions
            }).ConfigureAwait(false);
            var capacity = await Emit("capacity-proof-" + index,
                MoneyRevenueCropDailyCapacityProofBuilder.BuildNativeObserved(capacityRequest)).ConfigureAwait(false);
            capacities.Add(capacity);
            var lifecycleRequest = await Emit("lifecycle-request-" + index, new MoneyRevenueCropNativeLifecycleProofRequest
            {
                SchemaVersion = checkpointResumes.Length > 0 ? "money_revenue_crop_native_lifecycle_proof_request.v10" :
                    appliedNativeWork ? "money_revenue_crop_native_lifecycle_proof_request.v9" :
                    inventoryEffects ? "money_revenue_crop_native_lifecycle_proof_request.v8" :
                    shared.Count > 0 ? transferredStock ? "money_revenue_crop_native_lifecycle_proof_request.v7" :
                    "money_revenue_crop_native_lifecycle_proof_request.v6" :
                    "money_revenue_crop_native_lifecycle_proof_request.v5",
                CropProductionPlanPath = plan.CropProductionPlanPath, CommitmentId = crop.CommitmentId,
                PlantingLayoutProofPath = layouts[crop.CommitmentId], DailyCapacityProofPath = capacity, Transitions = transitions,
                CheckpointResumes = checkpointResumes
            }).ConfigureAwait(false);
            var lifecycleProof = MoneyRevenueCropNativeLifecycleProofBuilder.Build(lifecycleRequest);
            var lifecycle = await Emit("lifecycle-proof-" + index, lifecycleProof).ConfigureAwait(false);
            lifecycles.Add(lifecycle);
            foreach (var wave in crop.HarvestWaves)
            {
                var retainThroughHead = (shared.Count > 0 || retainSaleStock) && wave.HarvestTotalDay == transitions[^1].TotalDay;
                var retentionRequest = await Emit($"retention-request-{index}-{wave.WaveIndex}", new MoneyRevenueCropOutputRetentionProofRequest
                {
                    SchemaVersion = appliedNativeWork ? retainSaleStock ? "money_revenue_crop_output_retention_proof_request.v8" :
                        "money_revenue_crop_output_retention_proof_request.v7" :
                        inventoryEffects ? "money_revenue_crop_output_retention_proof_request.v6" :
                        transferredStock ? "money_revenue_crop_output_retention_proof_request.v5" :
                        retainSaleStock ? "money_revenue_crop_output_retention_proof_request.v4" :
                        retainThroughHead ? "money_revenue_crop_output_retention_proof_request.v3" :
                        shared.Count > 0 ? "money_revenue_crop_output_retention_proof_request.v2" :
                        "money_revenue_crop_output_retention_proof_request.v1",
                    CropProductionPlanPath = plan.CropProductionPlanPath, NativeLifecycleProofPath = lifecycle,
                    CommitmentId = crop.CommitmentId, WaveIndex = wave.WaveIndex,
                    RetainThroughSnapshotPath = retainThroughHead ? trace.AcceptedSnapshotPath : retainSaleStock || appliedNativeWork ?
                        lifecycleProof.Transitions.Last(row => row.TotalDay == wave.HarvestTotalDay && row.OptionId == "executor.harvest_crop" &&
                            row.ActionCommitmentId == crop.CommitmentId).AfterSnapshotPath : null
                }).ConfigureAwait(false);
                retentions.Add(await Emit($"retention-proof-{index}-{wave.WaveIndex}",
                    MoneyRevenueCropOutputRetentionProofBuilder.Build(retentionRequest)).ConfigureAwait(false));
            }
        }
        artifacts.Validate();
        var manifestValue = new
        {
            schema_version = "crop_teacher_managed_production_evidence.v1", native_run_plan_path = trace.NativeRunPlanPath,
            accepted_snapshot_path = trace.AcceptedSnapshotPath, accepted_ledger_path = trace.AcceptedLedgerPath,
            owner_transitions = trace.Transitions, input_artifact_hashes = artifacts.Hashes.ToDictionary(row => row.Key, row => row.Value),
            planting_layout_proof_paths = layouts.Values.ToArray(), daily_capacity_proof_paths = capacities.ToArray(),
            native_lifecycle_proof_paths = lifecycles.ToArray(), output_retention_proof_paths = retentions.ToArray(),
            native_crop_lifecycle_verified = true, whole_wave_output_retention_verified = true,
            terminal_settlement_verified = false, native_save_content_readback_verified = false,
            formal_product_training_authorized = false
        };
        var manifest = shared.Count == 0 ? await Emit("production-evidence", manifestValue).ConfigureAwait(false) :
            await Emit("production-evidence", AddJointSourceManifest(manifestValue, shared, ordered)).ConfigureAwait(false);
        artifacts.Validate();
        return new(manifest, layouts.Values.ToArray(), capacities.ToArray(), lifecycles.ToArray(), retentions.ToArray(), artifacts.Hashes[manifest]);
    }

    private static T Read<T>(string path) => JsonSerializer.Deserialize<T>(ContentAddressedJsonArtifactReader.ReadAllText(path),
        CropTeacherManagedSeedOwner.JsonOptions) ?? throw new InvalidDataException("Managed crop production artifact is empty.");
    private static bool SamePath(string first, string second) => string.Equals(Path.GetFullPath(first), Path.GetFullPath(second),
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
}
