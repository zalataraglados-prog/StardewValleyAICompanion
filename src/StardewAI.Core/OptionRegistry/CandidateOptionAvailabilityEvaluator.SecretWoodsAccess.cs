using System;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Core.Infrastructure;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.OptionRegistry;

public sealed partial class CandidateOptionAvailabilityEvaluator
{
    private static readonly string[] SecretWoodsAxeBlockedStages =
    [
        "wait_for_axe_upgrade",
        "wait_for_other_tool_upgrade",
        "blocked_axe_claim_inventory_full",
        "blocked_tool_upgrade_requirements_missing",
        "blocked_no_next_axe_upgrade",
        "blocked_blacksmith_service_unavailable",
        "blocked_tool_upgrade_projection_missing"
    ];

    private bool TryBuildSecretWoodsPrerequisiteCandidates(
        SnapshotEnvelope snapshot,
        MasterAnglerWindowIntentValidation intent,
        SmallModelActionParameter[] intentParameters,
        SmallModelActionParameter[] continuation,
        out EventCandidate[] candidates)
    {
        candidates = [];
        if (!string.Equals(intent.TargetLocation, "Woods", StringComparison.Ordinal))
            return false;

        var access = ReadStateFieldValue(snapshot, "locations", "secret_woods_access");
        if (!access.HasValue || access.Value.ValueKind != JsonValueKind.Object ||
            ReadString(access.Value, "schema_version") != "secret_woods_access.v1" ||
            ReadString(access.Value, "projection_status") != "complete_live_native_contract")
        {
            candidates =
            [
                BlockedSecretWoodsCandidate(
                    "master_angler_secret_woods_access_projection_missing",
                    intentParameters)
            ];
            return true;
        }

        var status = ReadString(access.Value, "status");
        if (status is "secret_woods_open" or "forest_farm_alternative_available")
            return false;

        if (status == "requires_steel_axe")
        {
            candidates = SecretWoodsAxeUpgradeCandidates(
                snapshot,
                intentParameters,
                continuation);
            return true;
        }

        if (status == "ready_to_clear_entry_log")
        {
            var currentLocation = ReadStateFieldString(
                snapshot,
                "player",
                "location_id");
            if (!string.Equals(currentLocation, "Forest", StringComparison.Ordinal))
                return false;

            candidates = SecretWoodsEntryLogCandidates(
                snapshot,
                access.Value,
                intentParameters,
                continuation);
            return true;
        }

        candidates =
        [
            BlockedSecretWoodsCandidate(
                "master_angler_secret_woods_access_" +
                    (string.IsNullOrWhiteSpace(status) ? "status_missing" : status),
                intentParameters)
        ];
        return true;
    }

    private EventCandidate[] SecretWoodsAxeUpgradeCandidates(
        SnapshotEnvelope snapshot,
        SmallModelActionParameter[] intentParameters,
        SmallModelActionParameter[] continuation)
    {
        var projection = ReadStateFieldValue(snapshot, "player", "tool_upgrade");
        if (!projection.HasValue || projection.Value.ValueKind != JsonValueKind.Object ||
            ReadString(projection.Value, "projection_status") != "complete_live_native_contract")
        {
            return
            [
                BlockedSecretWoodsCandidate(
                    "master_angler_secret_woods_tool_upgrade_projection_missing",
                    intentParameters)
            ];
        }

        var stage = ReadString(projection.Value, "lifecycle_stage");
        EventCandidate[] raw = stage switch
        {
            "route_to_blacksmith_tool_upgrade" or
                "route_to_blacksmith_tool_claim" =>
                RouteToAxeToolUpgradeCandidates(
                    snapshot,
                    stage,
                    "master-angler-secret-woods-tool-route",
                    "master_angler_secret_woods_tool_upgrade_route",
                    "secret_woods_access_continuation=" + stage),
            "submit_next_axe_upgrade" =>
                AxeToolUpgradeActionCandidates(
                    snapshot,
                    stage,
                    "master-angler-secret-woods",
                    "master_angler_secret_woods_tool_upgrade",
                    claim: false),
            "claim_ready_axe_upgrade" =>
                AxeToolUpgradeActionCandidates(
                    snapshot,
                    stage,
                    "master-angler-secret-woods",
                    "master_angler_secret_woods_tool_upgrade",
                    claim: true),
            _ when SecretWoodsAxeBlockedStages.Contains(stage, StringComparer.Ordinal) =>
            [
                BlockedSecretWoodsCandidate(
                    "master_angler_secret_woods_" + stage,
                    intentParameters)
            ],
            _ => []
        };

        if (raw.Length == 0)
        {
            return
            [
                BlockedSecretWoodsCandidate(
                    "master_angler_secret_woods_no_exact_tool_upgrade_stage_candidate:" +
                        stage,
                    intentParameters)
            ];
        }
        return raw.Select(candidate => CloneCandidate(
                candidate,
                candidateId: "master-angler-secret-woods:" +
                    stage + ":" + candidate.CandidateId,
                expectedEffect: candidate.ExpectedEffect +
                    ";secret_woods_required_axe_upgrade_level=2" +
                    ";fresh_snapshot_and_secret_woods_access_replan_required=true",
                parameters: candidate.Parameters
                    .Concat(intentParameters)
                    .Concat(continuation)
                    .ToArray(),
                availabilityClass: "master_angler_secret_woods_" + stage))
            .ToArray();
    }

    private EventCandidate[] SecretWoodsEntryLogCandidates(
        SnapshotEnvelope snapshot,
        JsonElement access,
        SmallModelActionParameter[] intentParameters,
        SmallModelActionParameter[] continuation)
    {
        var clumps = ReadStateFieldValue(snapshot, "current_location", "resource_clumps");
        if (!clumps.HasValue || clumps.Value.ValueKind != JsonValueKind.Array)
            return [BlockedSecretWoodsCandidate("master_angler_secret_woods_current_clumps_missing", intentParameters)];

        var x = ReadInt(access, "entry_log_tile_x");
        var y = ReadInt(access, "entry_log_tile_y");
        var clump = clumps.Value.EnumerateArray().SingleOrDefault(row =>
            row.ValueKind == JsonValueKind.Object &&
            ReadInt(row, "tile_x") == x &&
            ReadInt(row, "tile_y") == y &&
            ReadInt(row, "parent_sheet_index") == 602);
        if (clump.ValueKind != JsonValueKind.Object)
            return [BlockedSecretWoodsCandidate("master_angler_secret_woods_entry_log_projection_drifted", intentParameters)];

        var width = ReadInt(clump, "width");
        var height = ReadInt(clump, "height");
        var standSelection = FindBestResourceClumpStandTile(
            snapshot,
            x,
            y,
            width,
            height);
        var stand = standSelection?.Stand;
        var hit = standSelection?.Hit;
        var hits = Math.Max(1, ReadInt(clump, "expected_tool_hits_to_clear"));
        var parameters = new[]
        {
            Parameter("target_location", "Forest"),
            Parameter("target_tile_x", (hit?.X ?? x).ToString(CultureInfo.InvariantCulture)),
            Parameter("target_tile_y", (hit?.Y ?? y).ToString(CultureInfo.InvariantCulture)),
            Parameter("stand_tile_x", (stand?.X ?? x).ToString(CultureInfo.InvariantCulture)),
            Parameter("stand_tile_y", (stand?.Y ?? y).ToString(CultureInfo.InvariantCulture)),
            Parameter("route_distance_tiles", (stand?.Distance ?? 0).ToString(CultureInfo.InvariantCulture)),
            Parameter("resource_clump_purpose", "secret_woods_entry_log"),
            Parameter("resource_clump_tile_x", x.ToString(CultureInfo.InvariantCulture)),
            Parameter("resource_clump_tile_y", y.ToString(CultureInfo.InvariantCulture)),
            Parameter("resource_clump_width", width.ToString(CultureInfo.InvariantCulture)),
            Parameter("resource_clump_height", height.ToString(CultureInfo.InvariantCulture)),
            Parameter("resource_clump_parent_sheet_index", "602"),
            Parameter("target_runtime_type", ReadString(clump, "runtime_type")),
            Parameter("tool_slot_index", ReadInt(clump, "tool_slot_index").ToString(CultureInfo.InvariantCulture)),
            Parameter("required_tool_kind", "axe"),
            Parameter("minimum_tool_upgrade_level", "2"),
            Parameter("max_tool_swings", hits.ToString(CultureInfo.InvariantCulture)),
            Parameter("max_movement_tiles", "512"),
            Parameter("expected_output_items_json", ReadString(clump, "expected_core_output_items_json")),
            Parameter("expected_foraging_experience_delta", "25"),
            Parameter("native_contract", ReadString(clump, "native_contract"))
        };
        var blocks = CompilerProbeBlockingReasons(snapshot, new OptionAvailabilityCandidate
        {
            OptionId = "executor.break_current_location_resource_clump",
            Parameters = parameters
        }).ToList();
        if (!string.Equals(
                ReadString(clump, "clear_obstacle_executor_status"),
                "ready",
                StringComparison.Ordinal))
        {
            blocks.Add("master_angler_secret_woods_entry_log_not_ready");
        }
        if (stand is null || hit is null)
            blocks.Add("master_angler_secret_woods_entry_log_no_reachable_perimeter_stand");

        var boundParameters = parameters
            .Concat(intentParameters)
            .Concat(continuation)
            .ToArray();
        return
        [
            new EventCandidate
            {
                CandidateId = "master-angler-secret-woods:clear-entry-log:Forest:1,6",
                Kind = "clear_current_location_resource_clump",
                Available = blocks.Count == 0,
                AllowedNow = blocks.Count == 0,
                AllowedToday = blocks.Count == 0,
                LocationId = "Forest",
                TileX = hit?.X ?? x,
                TileY = hit?.Y ?? y,
                QualifiedItemId = "(O)709",
                Quantity = 8,
                EstimatedTicks = Math.Max(60, (stand?.Distance ?? 0) * 60 + hits * 60),
                EnergyCost = hits * 2,
                AvailabilityClass = "master_angler_secret_woods_exact_entry_log",
                ExpectedEffect =
                    "current_location.resource_clumps[1,6].present=false" +
                    ";secret_woods_entry_log_removed=true" +
                    ";secret_woods_connector_source=Forest" +
                    ";secret_woods_connector_target=Woods" +
                    ";fresh_snapshot_and_master_angler_replan_required=true",
                BlockReasons = blocks.Distinct(StringComparer.Ordinal).ToArray(),
                Parameters = boundParameters
            }
        ];
    }

    private static EventCandidate BlockedSecretWoodsCandidate(
        string reason,
        SmallModelActionParameter[] parameters) => new()
        {
            CandidateId = "master-angler:secret-woods-access:blocked",
            Kind = "secret_woods_access_wait",
            Available = false,
            EstimatedTicks = -1,
            AvailabilityClass = "master_angler_secret_woods_access_fail_closed",
            ExpectedEffect = "secret_woods_access_not_compiled;fresh_snapshot_required=true",
            BlockReasons = [reason],
            Parameters = parameters
        };
}
