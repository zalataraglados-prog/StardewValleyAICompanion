using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.LiveTrainingLoop;

public sealed record CropTeacherManagedTerminalEvidence(string ManifestPath, string ManifestSha256,
    string TerminalProofPath, string RecurrenceProofPath, long SettledRevenue);

internal static class CropTeacherTerminalSettlementEvidence
{
    internal static async Task<CropTeacherManagedTerminalEvidence> BuildAsync(CropTeacherManagedProductionTrace trace,
        CropTeacherManagedProductionEvidence production, CropTeacherTerminalShippingTarget target,
        string output, Func<string, string, Task> write)
    {
        trace.ValidateInputsUnchanged();
        var artifacts = new CropSeedRuntimeArtifacts(trace.InputHashes);
        void Require(bool condition, string reason)
        {
            if (!condition) throw new InvalidDataException("Managed terminal evidence: " + reason);
        }
        string Pin(string path)
        {
            var full = Path.GetFullPath(path);
            artifacts.Pin(full);
            return artifacts.Hashes[full];
        }
        var manifest = artifacts.Read<JsonElement>(production.ManifestPath);
        Require(!string.IsNullOrWhiteSpace(production.ManifestSha256) && Pin(production.ManifestPath) == production.ManifestSha256,
            "production manifest hash differs.");
        Require(manifest.GetProperty("schema_version").GetString() == "crop_teacher_managed_production_evidence.v1" &&
            manifest.GetProperty("native_crop_lifecycle_verified").GetBoolean() &&
            manifest.GetProperty("whole_wave_output_retention_verified").GetBoolean() &&
            !manifest.GetProperty("terminal_settlement_verified").GetBoolean() &&
            !manifest.GetProperty("formal_product_training_authorized").GetBoolean(), "production certificate is not admissible.");
        Require(Same(manifest.GetProperty("native_run_plan_path").GetString()!, trace.NativeRunPlanPath), "native plan differs.");
        foreach (var row in manifest.GetProperty("input_artifact_hashes").EnumerateObject())
            Require(Pin(row.Name) == row.Value.GetString(), "production input changed.");
        var (_, plan) = CropTeacherResumeLoader.ReadVerifiedPlan(trace.NativeRunPlanPath);
        Pin(trace.NativeRunPlanPath);
        var cropPlanHash = Pin(plan.CropProductionPlanPath);
        var retention = artifacts.Read<MoneyRevenueCropOutputRetentionProof>(target.RetentionProofPath);
        Require(production.OutputRetentionProofPaths.Any(path => Same(path, target.RetentionProofPath)) &&
            manifest.GetProperty("output_retention_proof_paths").EnumerateArray().Any(row => Same(row.GetString()!, target.RetentionProofPath)) &&
            Pin(target.RetentionProofPath) == target.RetentionProofSha256 && retention.NativeOutputRetentionVerified &&
            !retention.FormalProductTrainingAuthorized && Same(retention.CropProductionPlanPath, plan.CropProductionPlanPath) &&
            retention.CropProductionPlanSha256 == cropPlanHash && retention.HarvestItemQualifiedId == target.QualifiedItemId &&
            retention.HarvestTotalDay == target.TotalDay && target.RequiredQuantity == retention.MinimumUnits &&
            target.RequiredQuantity > 0 && target.AcceptedDepositCount == target.RequiredQuantity &&
            target.InitialQuantity >= target.RequiredQuantity && target.UnitPrice > 0 && target.Quality >= 0 && target.SlotIndex >= 0,
            "target differs from the original selected retained wave/cursor.");
        var initial = artifacts.Read<SnapshotEnvelope>(retention.AfterSnapshotPath);
        Require(Pin(retention.AfterSnapshotPath) == retention.AfterSnapshotSha256 && initial.StateHash == retention.AfterStateHash &&
            Same(manifest.GetProperty("accepted_snapshot_path").GetString()!, retention.AfterSnapshotPath), "retained production head differs.");
        JsonElement Field(string domain, string name) => initial.State[domain].GetProperty(name).GetProperty("value");
        Require(initial.SaveId.Value == target.SaveId && initial.PlayerId.Value == target.PlayerId && initial.GameVersion == target.GameVersion &&
            Field("time", "total_days").GetInt32() == target.TotalDay && Field("player", "location_id").GetString() == "Farm",
            "retained actor/game/day differs.");
        var slots = Field("player", "inventory").EnumerateArray().Where(row => row.GetProperty("slot_index").GetInt32() == target.SlotIndex).ToArray();
        Require(slots.Length == 1 && slots[0].GetProperty("qualified_item_id").GetString() == target.QualifiedItemId &&
            slots[0].GetProperty("stack").GetInt32() == target.InitialQuantity && slots[0].GetProperty("quality").GetInt32() == target.Quality &&
            slots[0].GetProperty("sell_to_store_price").GetInt32() == target.UnitPrice && slots[0].GetProperty("can_be_shipped").GetBoolean(),
            "retained slot/variant/price differs.");
        var bins = Field("farm", "shipping_bins").EnumerateArray().Where(row => row.GetProperty("tile_x").GetInt32() == target.BinTileX &&
            row.GetProperty("tile_y").GetInt32() == target.BinTileY).ToArray();
        Require(bins.Length == 1 && bins[0].GetProperty("interaction_stand_tile_x").GetInt32() == target.StandTileX &&
            bins[0].GetProperty("interaction_stand_tile_y").GetInt32() == target.StandTileY &&
            bins[0].GetProperty("pending_settlement_value").GetInt64() == 0, "selected bin/stand differs.");

        var starts = trace.Transitions.Select((row, index) => (row, index))
            .Where(pair => Same(pair.row.NativeTransition.BeforeSnapshotPath, retention.AfterSnapshotPath)).ToArray();
        Require(starts.Length == 1, "terminal suffix must have one original production boundary.");
        var suffix = trace.Transitions.Skip(starts[0].index).ToArray();
        var routes = new List<MoneyRevenueTerminalQueueTransitionProof>();
        var deposits = new List<MoneyRevenueTerminalQueueTransitionProof>();
        var home = new List<MoneyRevenueTerminalQueueTransitionProof>();
        var previous = retention.AfterSnapshotPath;
        var sequence = starts[0].index == 0 ? 0 : trace.Transitions[starts[0].index - 1].OwnerSequence;
        var run = suffix[0].NativeTransition.RunId;
        var executor = suffix[0].NativeTransition.ExecutorVersion;
        Require(!string.IsNullOrWhiteSpace(run) && !string.IsNullOrWhiteSpace(executor), "missing native identity.");
        for (var i = 0; i < suffix.Length; i++)
        {
            var row = suffix[i]; var native = row.NativeTransition;
            Require(row.OwnerSequence > sequence && Same(native.BeforeSnapshotPath, previous) && native.RunId == run &&
                native.ExecutorVersion == executor && !string.IsNullOrWhiteSpace(native.SelectedCandidateId) && native.TotalDay == target.TotalDay,
                "suffix sequence/head/native identity differs.");
            foreach (var path in new[] { native.QueuePath, native.BeforeSnapshotPath, native.ExecutionReceiptPath, native.AfterSnapshotPath })
                Require(trace.InputHashes.Keys.Any(key => Same(key, path)) && !string.IsNullOrWhiteSpace(Pin(path)), "native artifact is not pinned by owner trace.");
            var transition = new MoneyRevenueTerminalQueueTransitionProof
            {
                TransitionId = "owner-terminal-" + row.OwnerSequence, QueuePath = native.QueuePath,
                BeforeSnapshotPath = native.BeforeSnapshotPath, ExecutionReceiptPath = native.ExecutionReceiptPath,
                AfterSnapshotPath = native.AfterSnapshotPath, RunId = native.RunId, ExecutorVersion = native.ExecutorVersion,
                SelectedCandidateId = native.SelectedCandidateId
            };
            if (row.OperationKind == "terminal_shipping" && native.TransitionKind == "shipping_approach" && i == 0) routes.Add(transition);
            else if (row.OperationKind == "terminal_shipping" && native.TransitionKind == "shipping_deposit" && home.Count == 0 && deposits.Count < target.RequiredQuantity) deposits.Add(transition);
            else if (row.OperationKind == "supporting_route" && native.TransitionKind == "supporting_route_to_sleep" && deposits.Count == target.RequiredQuantity) home.Add(transition);
            else Require(i == suffix.Length - 1 && row.OperationKind == "overnight_control" && native.TransitionKind == "native_day_rollover" &&
                deposits.Count == target.RequiredQuantity, "unsupported or unconsumed suffix transition.");
            previous = native.AfterSnapshotPath; sequence = row.OwnerSequence;
        }
        var sleep = suffix[^1];
        Require(sleep.OperationKind == "overnight_control" && sleep.NativeTransition.TransitionKind == "native_day_rollover" &&
            deposits.Count == target.RequiredQuantity && Same(previous, trace.AcceptedSnapshotPath), "missing final native settlement/head.");
        Pin(trace.AcceptedSnapshotPath); Pin(trace.AcceptedLedgerPath);
        var final = artifacts.Read<SnapshotEnvelope>(trace.AcceptedSnapshotPath);
        var ledger = artifacts.Read<StrategyCommitmentLedger>(trace.AcceptedLedgerPath);
        Require(final.SaveId.Value == target.SaveId && final.PlayerId.Value == target.PlayerId && final.GameVersion == target.GameVersion,
            "final accepted actor/game differs.");
        Require(ledger.SourceStateHash == final.StateHash && ledger.SaveId == target.SaveId && ledger.PlayerId == target.PlayerId,
            "final accepted ledger differs from the same native source/actor.");
        output = Path.GetFullPath(output);
        Require(!artifacts.Hashes.Keys.Any(path => Same(path, output) || Below(path, output) || Below(output, path)), "output overlaps retained inputs.");
        if (Directory.Exists(output) || File.Exists(output)) throw new IOException("Managed terminal output already exists.");
        artifacts.Validate();
        Directory.CreateDirectory(output);
        async Task<string> Emit<T>(string name, T value)
        {
            var path = Path.Combine(output, name + ".json");
            await write(path, JsonSerializer.Serialize(value, CropTeacherManagedSeedOwner.JsonOptions)).ConfigureAwait(false);
            Require(JsonNode.DeepEquals(JsonSerializer.SerializeToNode(value, CropTeacherManagedSeedOwner.JsonOptions), artifacts.Read<JsonNode>(path)), "writer changed original proof output.");
            return path;
        }
        var overnight = sleep.NativeTransition;
        var recurrenceManifest = await Emit("recurrence-manifest", new MoneyRevenueRecurrenceProofManifest
        {
            Transitions = [new MoneyRevenueRecurrenceTransitionProof
            {
                TransitionId = "owner-terminal-" + sleep.OwnerSequence, ProofKind = MoneyRevenueRecurrenceProofKinds.NativeShippingSettlement,
                QueuePath = overnight.QueuePath, BeforeSnapshotPath = overnight.BeforeSnapshotPath,
                ExecutionReceiptPath = overnight.ExecutionReceiptPath, AfterSnapshotPath = overnight.AfterSnapshotPath,
                RunId = overnight.RunId, ExecutorVersion = overnight.ExecutorVersion, SelectedCandidateId = overnight.SelectedCandidateId
            }]
        }).ConfigureAwait(false);
        var recurrence = MoneyRevenueRecurrenceProofBuilder.Build(recurrenceManifest);
        Require(recurrence.ProofVerified && !recurrence.FormalProductTrainingAuthorized, "original recurrence did not verify.");
        var recurrencePath = await Emit("recurrence-proof", recurrence).ConfigureAwait(false);
        var requestPath = await Emit("terminal-request", new MoneyRevenueCropTerminalSettlementProofRequest
        {
            CropProductionPlanPath = plan.CropProductionPlanPath, OutputRetentionProofPath = target.RetentionProofPath,
            RouteTransitions = routes.ToArray(), ShippingDepositTransitions = deposits.ToArray(), SettlementRouteTransitions = home.ToArray(),
            RevenueRecurrenceManifestPath = recurrenceManifest, RevenueRecurrenceReceiptPath = recurrencePath
        }).ConfigureAwait(false);
        // The original builder owns all settlement math and its mandatory support verification.
        var proof = MoneyRevenueCropTerminalSettlementProofBuilder.Build(requestPath);
        Require(proof.Status == "verified_native_crop_terminal_settlement" && proof.NativeTerminalSettlementVerified &&
            !proof.FormalProductTrainingAuthorized && proof.SettledQuantity == target.RequiredQuantity && proof.UnitPrice == target.UnitPrice &&
            proof.SlotIndex == target.SlotIndex && proof.HarvestItemQualifiedId == target.QualifiedItemId &&
            proof.SettledRevenue >= retention.MinimumGrossRevenue && proof.TerminalActionAfterStateHash == final.StateHash,
            "original terminal proof differs from selected target/final head.");
        var terminalPath = await Emit("terminal-proof", proof).ConfigureAwait(false);
        artifacts.Validate(); trace.ValidateInputsUnchanged();
        var result = await Emit("terminal-evidence", new
        {
            schema_version = "crop_teacher_managed_terminal_evidence.v1", native_run_plan_path = trace.NativeRunPlanPath,
            accepted_snapshot_path = trace.AcceptedSnapshotPath, accepted_ledger_path = trace.AcceptedLedgerPath,
            target, production_manifest_path = production.ManifestPath, production_manifest_sha256 = production.ManifestSha256,
            terminal_proof_path = terminalPath, recurrence_manifest_path = recurrenceManifest, recurrence_proof_path = recurrencePath,
            owner_terminal_transitions = suffix, input_artifact_hashes = artifacts.Hashes.ToDictionary(row => row.Key, row => row.Value),
            settled_revenue = proof.SettledRevenue, terminal_settlement_verified = true,
            semantic_saved_content_verified = false, formal_product_training_authorized = false
        }).ConfigureAwait(false);
        artifacts.Validate(); trace.ValidateInputsUnchanged();
        return new(result, artifacts.Hashes[result], terminalPath, recurrencePath, proof.SettledRevenue);
    }

    private static bool Same(string left, string right) => string.Equals(Path.GetFullPath(left), Path.GetFullPath(right),
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    private static bool Below(string path, string parent) => Path.GetFullPath(path).StartsWith(Path.TrimEndingDirectorySeparator(Path.GetFullPath(parent)) + Path.DirectorySeparatorChar,
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
}
