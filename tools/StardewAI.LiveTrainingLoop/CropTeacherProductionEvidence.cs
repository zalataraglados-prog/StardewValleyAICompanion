using System.Text.Json;
using StardewAI.Core.Infrastructure;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.LiveTrainingLoop;

public sealed record CropTeacherManagedProductionEvidence(string ManifestPath, string[] PlantingLayoutProofPaths,
    string[] DailyCapacityProofPaths, string[] NativeLifecycleProofPaths, string[] OutputRetentionProofPaths);

internal static class CropTeacherProductionEvidence
{
    internal static async Task<CropTeacherManagedProductionEvidence> BuildAsync(CropTeacherManagedProductionTrace trace,
        string output, Func<string, string, Task> write)
    {
        trace.ValidateInputsUnchanged();
        var (request, plan) = CropTeacherResumeLoader.ReadVerifiedPlan(trace.NativeRunPlanPath);
        var production = Read<MoneyRevenueCropProductionObligationPlan>(plan.CropProductionPlanPath);
        var firstPlanting = trace.Transitions.ToList().FindIndex(row => row.OperationKind == "owned_seed_planting");
        if (firstPlanting < 0) throw new InvalidDataException("Managed crop production has no accepted original planting boundary.");
        // Acquisition/site preparation precede the original lifecycle's planting denominator.
        // Nothing inside that lifecycle is filtered out or relabelled as resource-neutral.
        var transitions = trace.Transitions.Skip(firstPlanting).Select(row => row.OperationKind == "owned_seed_planting"
            ? row.NativeTransition with { TransitionKind = "crop_actions" } : row.NativeTransition).ToArray();
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
        var refills = requiredDays.Select(day => transitions.FirstOrDefault(row => row.TotalDay == day &&
                SamePath(row.BeforeSnapshotPath, dayStarts[day]) && HasRefill(row.ExecutionReceiptPath)))
            .Where(row => row is not null).Select(row => new MoneyRevenueCropDailyCapacityRefillReceiptRequest(row!.TotalDay,
                row.ExecutionReceiptPath)).ToArray();
        for (var index = 0; index < production.Commitments.Length; index++)
        {
            var crop = production.Commitments[index];
            var capacityRequest = await Emit("capacity-request-" + index, new MoneyRevenueCropDailyCapacityProofRequest
            {
                CropProductionPlanPath = plan.CropProductionPlanPath, CommitmentId = crop.CommitmentId,
                PlantingLayoutProofPaths = layouts.Values.ToArray(), DailySnapshotPaths = requiredDays.Select(day => dayStarts[day]).ToArray(),
                WateringCanRefillReceipts = refills
            }).ConfigureAwait(false);
            var capacity = await Emit("capacity-proof-" + index,
                MoneyRevenueCropDailyCapacityProofBuilder.Build(capacityRequest)).ConfigureAwait(false);
            capacities.Add(capacity);
            var lifecycleRequest = await Emit("lifecycle-request-" + index, new MoneyRevenueCropNativeLifecycleProofRequest
            {
                CropProductionPlanPath = plan.CropProductionPlanPath, CommitmentId = crop.CommitmentId,
                PlantingLayoutProofPath = layouts[crop.CommitmentId], DailyCapacityProofPath = capacity, Transitions = transitions
            }).ConfigureAwait(false);
            var lifecycle = await Emit("lifecycle-proof-" + index,
                MoneyRevenueCropNativeLifecycleProofBuilder.Build(lifecycleRequest)).ConfigureAwait(false);
            lifecycles.Add(lifecycle);
            foreach (var wave in crop.HarvestWaves)
            {
                var retentionRequest = await Emit($"retention-request-{index}-{wave.WaveIndex}", new MoneyRevenueCropOutputRetentionProofRequest
                {
                    CropProductionPlanPath = plan.CropProductionPlanPath, NativeLifecycleProofPath = lifecycle,
                    CommitmentId = crop.CommitmentId, WaveIndex = wave.WaveIndex
                }).ConfigureAwait(false);
                retentions.Add(await Emit($"retention-proof-{index}-{wave.WaveIndex}",
                    MoneyRevenueCropOutputRetentionProofBuilder.Build(retentionRequest)).ConfigureAwait(false));
            }
        }
        artifacts.Validate();
        var manifest = await Emit("production-evidence", new
        {
            schema_version = "crop_teacher_managed_production_evidence.v1", native_run_plan_path = trace.NativeRunPlanPath,
            accepted_snapshot_path = trace.AcceptedSnapshotPath, accepted_ledger_path = trace.AcceptedLedgerPath,
            owner_transitions = trace.Transitions, input_artifact_hashes = artifacts.Hashes.ToDictionary(row => row.Key, row => row.Value),
            planting_layout_proof_paths = layouts.Values.ToArray(), daily_capacity_proof_paths = capacities.ToArray(),
            native_lifecycle_proof_paths = lifecycles.ToArray(), output_retention_proof_paths = retentions.ToArray(),
            native_crop_lifecycle_verified = true, whole_wave_output_retention_verified = true,
            terminal_settlement_verified = false, native_save_content_readback_verified = false,
            formal_product_training_authorized = false
        }).ConfigureAwait(false);
        artifacts.Validate();
        return new(manifest, layouts.Values.ToArray(), capacities.ToArray(), lifecycles.ToArray(), retentions.ToArray());
    }

    private static T Read<T>(string path) => JsonSerializer.Deserialize<T>(ContentAddressedJsonArtifactReader.ReadAllText(path),
        CropTeacherManagedSeedOwner.JsonOptions) ?? throw new InvalidDataException("Managed crop production artifact is empty.");
    private static bool HasRefill(string path)
    {
        using var receipt = JsonDocument.Parse(ContentAddressedJsonArtifactReader.ReadAllText(path));
        return receipt.RootElement.GetProperty("step_results").EnumerateArray().Any(step =>
            step.GetProperty("option_id").GetString() == "executor.water_crop" &&
            step.TryGetProperty("water_before", out var before) && before.TryGetInt32(out var oldWater) && oldWater == 0 &&
            step.TryGetProperty("water_after", out var after) && after.TryGetInt32(out var newWater) && newWater > 0);
    }
    private static bool SamePath(string first, string second) => string.Equals(Path.GetFullPath(first), Path.GetFullPath(second),
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
}
