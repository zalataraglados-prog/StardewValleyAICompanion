using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.Strategy;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.OptionRegistry;

public sealed partial class CandidateOptionAvailabilityEvaluator
{
    private EventCandidate[] RainTotemCraftingCandidates(
        SnapshotEnvelope snapshot,
        SmallModelActionParameter[] boundParameters,
        StrategyCommitmentLedger? commitmentLedger)
    {
        if (!MasterAnglerWindowIntentValidator
                .TryValidateRainTotemSupplyIntent(
                    snapshot,
                    boundParameters,
                    out var intent,
                    out var rejectionReason))
        {
            return new[]
            {
                BlockedRainTotemCraftingCandidate(
                    rejectionReason,
                    boundParameters)
            };
        }
        if (InventoryContainsRainTotem(snapshot))
        {
            return RainTotemUseCandidates(snapshot, intent);
        }

        var crafting = ReadStateFieldValue(
            snapshot,
            "player",
            "rain_totem_crafting");
        if (!crafting.HasValue ||
            crafting.Value.ValueKind != JsonValueKind.Object ||
            !string.Equals(
                ReadString(crafting.Value, "projection_status"),
                "complete_learned_rain_totem_recipe_projection",
                StringComparison.Ordinal) ||
            !crafting.Value.TryGetProperty("rows", out var rows) ||
            rows.ValueKind != JsonValueKind.Array)
        {
            return new[]
            {
                BlockedRainTotemCraftingCandidate(
                    "master_angler_weather_supply_rain_totem_crafting_projection_unavailable",
                    CanonicalRainTotemSupplyParameters(intent))
            };
        }

        var recipeRows = rows.EnumerateArray()
            .Where(row => row.ValueKind == JsonValueKind.Object &&
                string.Equals(
                    ReadString(row, "output_qualified_item_id"),
                    "(O)681",
                    StringComparison.Ordinal) &&
                string.Equals(
                    ReadString(row, "recipe_name"),
                    "Rain Totem",
                    StringComparison.Ordinal) &&
                ReadInt(row, "output_count_per_craft") == 1)
            .ToArray();
        if (recipeRows.Length != 1)
        {
            return new[]
            {
                BlockedRainTotemCraftingCandidate(
                    "master_angler_weather_supply_exact_rain_totem_recipe_unavailable",
                    CanonicalRainTotemSupplyParameters(intent))
            };
        }

        var authorization = RainTotemMaterialReservationContract.Authorization(
            intent.TargetTotalDay,
            intent.AffectedLocationContextId);
        var craftingCandidates = ExactSupportCraftingCandidateBindings(
                snapshot,
                recipeRows[0],
                "rain_totem",
                authorization)
            .Select(binding => BuildRainTotemCraftingCandidate(
                snapshot,
                binding,
                intent,
                commitmentLedger,
                authorization))
            .OrderByDescending(candidate => candidate.Available)
            .ThenBy(candidate => candidate.CandidateId, StringComparer.Ordinal)
            .ToArray();
        if (craftingCandidates.Any(candidate => candidate.Available))
        {
            return craftingCandidates;
        }

        var materialSupply = RainTotemImmediateMaterialSupplyCandidates(
            snapshot,
            recipeRows[0],
            intent);
        return materialSupply.Length > 0
            ? materialSupply
            : craftingCandidates;
    }

    private EventCandidate[] RainTotemUseCandidates(
        SnapshotEnvelope snapshot,
        MasterAnglerRainTotemSupplyIntentValidation intent)
    {
        var projection = ReadStateFieldValue(snapshot, "player", "rain_totem");
        if (!projection.HasValue ||
            projection.Value.ValueKind != JsonValueKind.Object ||
            !string.Equals(
                ReadString(projection.Value, "projection_status"),
                "complete_current_native_rain_totem_context",
                StringComparison.Ordinal) ||
            !string.Equals(
                ReadString(projection.Value, "native_use_gate_status"),
                "ready",
                StringComparison.Ordinal) ||
            !projection.Value.TryGetProperty("context_routing", out var routing) ||
            routing.ValueKind != JsonValueKind.Object ||
            !projection.Value.TryGetProperty("weather_transition", out var transition) ||
            transition.ValueKind != JsonValueKind.Object ||
            !projection.Value.TryGetProperty("animation_contract", out var animation) ||
            animation.ValueKind != JsonValueKind.Object ||
            !projection.Value.TryGetProperty("rows", out var rows) ||
            rows.ValueKind != JsonValueKind.Array)
        {
            return new[]
            {
                BlockedRainTotemCraftingCandidate(
                    "master_angler_weather_supply_rain_totem_use_projection_unavailable",
                    CanonicalRainTotemSupplyParameters(intent))
            };
        }

        var location = ReadStateFieldString(
            snapshot,
            "player",
            "location_id");
        var candidates = rows.EnumerateArray()
            .Where(row =>
                string.Equals(
                    ReadString(row, "qualified_item_id"),
                    "(O)681",
                    StringComparison.Ordinal) &&
                ReadInt(row, "stack_before") > 0 &&
                ReadBool(row, "temporarily_invisible") != true)
            .Select(row => BuildRainTotemUseCandidate(
                snapshot,
                projection.Value,
                routing,
                transition,
                animation,
                row,
                location,
                intent))
            .OrderByDescending(candidate => candidate.Available)
            .ThenBy(candidate => candidate.CandidateId, StringComparer.Ordinal)
            .ToArray();
        return candidates.Length > 0
            ? candidates
            : new[]
            {
                BlockedRainTotemCraftingCandidate(
                    "master_angler_weather_supply_owned_rain_totem_row_unavailable",
                    CanonicalRainTotemSupplyParameters(intent))
            };
    }

    private EventCandidate BuildRainTotemUseCandidate(
        SnapshotEnvelope snapshot,
        JsonElement projection,
        JsonElement routing,
        JsonElement transition,
        JsonElement animation,
        JsonElement row,
        string location,
        MasterAnglerRainTotemSupplyIntentValidation intent)
    {
        var stackBefore = ReadInt(row, "stack_before");
        var parameters = CanonicalRainTotemSupplyParameters(intent)
            .Concat(new[]
            {
                Parameter("target_location", location),
                Parameter("inventory_slot_index", ReadInt(row, "inventory_slot_index")
                    .ToString(CultureInfo.InvariantCulture)),
                Parameter("item_id", "681"),
                Parameter("qualified_item_id", "(O)681"),
                Parameter("inventory_runtime_type", ReadString(row, "inventory_runtime_type")),
                Parameter("inventory_stack_before", stackBefore
                    .ToString(CultureInfo.InvariantCulture)),
                Parameter("inventory_stack_after", (stackBefore - 1)
                    .ToString(CultureInfo.InvariantCulture)),
                Parameter("rain_totem_projection_fingerprint", ReadString(
                    projection,
                    "projection_fingerprint")),
                Parameter("source_location_context_id", ReadString(
                    routing,
                    "source_location_context_id")),
                Parameter("configured_affected_context_id", ReadString(
                    routing,
                    "configured_affected_context_id")),
                Parameter("affected_location_context_id", ReadString(
                    routing,
                    "affected_location_context_id")),
                Parameter("weather_state_owner_context_id", ReadString(
                    routing,
                    "weather_state_owner_context_id")),
                Parameter("allow_rain_totem", BoolText(ReadBool(
                    routing,
                    "allow_rain_totem"))),
                Parameter("tomorrow_is_default_festival", BoolText(ReadBool(
                    transition,
                    "tomorrow_is_default_festival"))),
                Parameter("affected_weather_before", ReadString(
                    transition,
                    "affected_weather_before")),
                Parameter("affected_weather_after", ReadString(
                    transition,
                    "affected_weather_after")),
                Parameter("tomorrow_total_days", ReadInt(
                    transition,
                    "tomorrow_total_days").ToString(CultureInfo.InvariantCulture)),
                Parameter("effective_tomorrow_weather", ReadString(
                    transition,
                    "effective_tomorrow_weather")),
                Parameter("rain_will_take_effect_tomorrow", BoolText(ReadBool(
                    transition,
                    "rain_will_take_effect_tomorrow"))),
                Parameter("native_facing_direction", ReadInt(
                    animation,
                    "facing_direction").ToString(CultureInfo.InvariantCulture)),
                Parameter("native_animation_duration_ms", ReadInt(
                    animation,
                    "animation_duration_ms").ToString(CultureInfo.InvariantCulture)),
                Parameter("native_cloud_sprite_count", ReadInt(
                    animation,
                    "cloud_sprite_count").ToString(CultureInfo.InvariantCulture)),
                Parameter("native_item_sprite_count", ReadInt(
                    animation,
                    "item_sprite_count").ToString(CultureInfo.InvariantCulture)),
                Parameter("native_cloud_batch_count", ReadInt(
                    animation,
                    "cloud_batch_count").ToString(CultureInfo.InvariantCulture)),
                Parameter("native_cloud_delay_step_ms", ReadInt(
                    animation,
                    "cloud_delay_step_ms").ToString(CultureInfo.InvariantCulture)),
                Parameter("native_initial_sound", ReadString(
                    animation,
                    "initial_sound")),
                Parameter("native_delayed_sound", ReadString(
                    animation,
                    "delayed_sound")),
                Parameter("native_delayed_sound_ms", ReadInt(
                    animation,
                    "delayed_sound_ms").ToString(CultureInfo.InvariantCulture)),
                Parameter("native_contract", ReadString(
                    projection,
                    "native_contract"))
            })
            .ToArray();
        var reasons = CompilerProbeBlockingReasons(
            snapshot,
            new OptionAvailabilityCandidate
            {
                OptionId = "executor.use_rain_totem",
                Parameters = parameters
            });
        return new EventCandidate
        {
            CandidateId = "use-rain-totem:" +
                intent.TargetTotalDay.ToString(CultureInfo.InvariantCulture) +
                ":slot" + ReadInt(row, "inventory_slot_index")
                    .ToString(CultureInfo.InvariantCulture),
            Kind = "use_rain_totem",
            Available = reasons.Length == 0,
            LocationId = location,
            ItemId = "681",
            QualifiedItemId = "(O)681",
            Quantity = 1,
            EstimatedTicks = 210,
            EnergyCost = 0,
            AvailabilityClass =
                "master_angler_tomorrow_rain_supply_native_use",
            ExpectedEffect =
                "player.inventory.decreases=(O)681:1;" +
                "weather.tomorrow=Rain;" +
                "master_angler_weather_supply_target_total_day=" +
                intent.TargetTotalDay.ToString(CultureInfo.InvariantCulture) +
                ";fresh_snapshot_and_scheduler_replan_required=true",
            BlockReasons = reasons,
            Parameters = parameters
        };
    }

    private static string BoolText(bool? value) =>
        value == true ? "true" : value == false ? "false" : string.Empty;

    private EventCandidate BuildRainTotemCraftingCandidate(
        SnapshotEnvelope snapshot,
        ExactSupportCraftingCandidateBinding binding,
        MasterAnglerRainTotemSupplyIntentValidation intent,
        StrategyCommitmentLedger? commitmentLedger,
        MaterialReservationConsumptionAuthorization authorization)
    {
        var reservation = new MachineCraftingMaterialReservationGuard()
            .Evaluate(
                snapshot,
                binding.Source.TryGetProperty(
                    "ingredient_rows",
                    out var ingredientRows)
                        ? ingredientRows
                        : default,
                binding.UsesWorkbench,
                commitmentLedger,
                authorization);
        var parameters = binding.Parameters
            .Where(parameter =>
                parameter.Name is not "commitment_ledger_id" and
                not "commitment_ledger_revision" and
                not "material_reservation_guard_status" and
                not "material_reservation_ledger_id" and
                not "material_reservation_ledger_revision" and
                not "material_reservation_ids_json" and
                not "material_reservation_consumptions_json")
            .Concat(CanonicalRainTotemSupplyParameters(intent))
            .Concat(new[]
            {
                Parameter("commitment_ledger_id", reservation.LedgerId),
                Parameter(
                    "commitment_ledger_revision",
                    reservation.LedgerRevision.ToString(
                        CultureInfo.InvariantCulture)),
                Parameter(
                    "material_reservation_guard_status",
                    reservation.Status),
                Parameter(
                    "material_reservation_ledger_id",
                    reservation.LedgerId),
                Parameter(
                    "material_reservation_ledger_revision",
                    reservation.LedgerRevision.ToString(
                        CultureInfo.InvariantCulture)),
                Parameter(
                    "material_reservation_ids_json",
                    JsonSerializer.Serialize(reservation.ReservationIds)),
                Parameter(
                    "material_reservation_consumptions_json",
                    RainTotemSettlementConsumptionsJson(
                        snapshot,
                        binding,
                        authorization)),
                Parameter(
                    "material_reservation_portfolio_id",
                    RainTotemMaterialReservationContract.PortfolioId(
                        intent.TargetTotalDay,
                        intent.AffectedLocationContextId)),
                Parameter(
                    "rain_totem_crafting_reason",
                    "master_angler_future_rain_window"),
                Parameter("weather_supply_required_weather", "Rain"),
                Parameter(
                    "weather_supply_scheduler_status",
                    "authoritative_future_window_bound"),
                Parameter(
                    "weather_supply_affected_context_id",
                    intent.AffectedLocationContextId),
                Parameter(
                    "weather_supply_target_total_day",
                    intent.TargetTotalDay.ToString(
                        CultureInfo.InvariantCulture)),
                Parameter(
                    "weather_supply_target_qualified_item_ids_json",
                    JsonSerializer.Serialize(
                        intent.TargetQualifiedItemIds))
            })
            .ToArray();
        var reasons = new List<string>(binding.BlockingReasons);
        reasons.AddRange(reservation.BlockingReasons);
        reasons.AddRange(CompilerProbeBlockingReasons(
            snapshot,
            new OptionAvailabilityCandidate
            {
                OptionId = "executor.craft_rain_totem",
                Parameters = parameters
            },
            commitmentLedger));

        return new EventCandidate
        {
            CandidateId = "craft-rain-totem:" +
                intent.TargetTotalDay.ToString(CultureInfo.InvariantCulture) +
                ":" + intent.AffectedLocationContextId +
                (binding.UsesWorkbench
                    ? ":workbench:" + ReadString(
                        binding.Source,
                        "workbench_access_point_id")
                    : ":personal"),
            Kind = "craft_rain_totem",
            Available = reasons.Count == 0,
            LocationId = binding.LocationId,
            ItemId = ReadString(binding.RecipeRow, "output_item_id"),
            QualifiedItemId = "(O)681",
            Quantity = binding.OutputCount,
            EstimatedTicks = 30,
            EnergyCost = 0,
            AvailabilityClass = binding.UsesWorkbench
                ? "master_angler_tomorrow_rain_supply_native_workbench_crafting"
                : "master_angler_tomorrow_rain_supply_native_personal_crafting",
            ExpectedEffect =
                "player.inventory.materials_consumed_by_native_recipe=true;" +
                "player.inventory.output_increases=(O)681:1;" +
                "master_angler_weather_supply_target_total_day=" +
                intent.TargetTotalDay.ToString(CultureInfo.InvariantCulture) +
                ";fresh_snapshot_and_weather_supply_replan_required=true",
            BlockReasons = reasons
                .Distinct(StringComparer.Ordinal)
                .ToArray(),
            Parameters = parameters
        };
    }

    private static string RainTotemSettlementConsumptionsJson(
        SnapshotEnvelope snapshot,
        ExactSupportCraftingCandidateBinding binding,
        MaterialReservationConsumptionAuthorization authorization)
    {
        if (!MaterialInventoryGraphReader.TryRead(snapshot, out var graph) ||
            graph is null ||
            !binding.Source.TryGetProperty(
                "ingredient_rows",
                out var ingredients))
        {
            return "[]";
        }

        var exact = ExactCraftingMaterialConsumptionReader.Read(
            ingredients,
            binding.UsesWorkbench,
            graph.PlayerId);
        return JsonSerializer.Serialize(
            RainTotemMaterialReservationContract.SettlementConsumptions(
                authorization,
                exact));
    }

    private static SmallModelActionParameter[]
        CanonicalRainTotemSupplyParameters(
            MasterAnglerRainTotemSupplyIntentValidation intent) =>
        new[]
        {
            Parameter(
                MasterAnglerWindowIntentValidator
                    .WeatherSupplyStatusParameter,
                "authoritative_tomorrow_rain_window_match"),
            Parameter(
                MasterAnglerWindowIntentValidator
                    .WeatherSupplyTargetDayParameter,
                intent.TargetTotalDay.ToString(CultureInfo.InvariantCulture)),
            Parameter(
                MasterAnglerWindowIntentValidator
                    .WeatherSupplyTargetIdsParameter,
                JsonSerializer.Serialize(intent.TargetQualifiedItemIds)),
            Parameter(
                MasterAnglerWindowIntentValidator
                    .WeatherSupplyAffectedContextParameter,
                intent.AffectedLocationContextId),
            Parameter(
                MasterAnglerWindowIntentValidator
                    .WeatherSupplyWindowIndexPathParameter,
                intent.WindowIndexPath),
            Parameter(
                MasterAnglerWindowIntentValidator
                    .WeatherSupplyWindowIndexSha256Parameter,
                intent.WindowIndexSha256),
            Parameter(
                MasterAnglerWindowIntentValidator
                    .WeatherSupplyReplanRequiredParameter,
                "true")
        };

    private static bool InventoryContainsRainTotem(
        SnapshotEnvelope snapshot)
    {
        var inventory = ReadStateFieldValue(snapshot, "player", "inventory");
        return inventory.HasValue &&
            inventory.Value.ValueKind == JsonValueKind.Array &&
            inventory.Value.EnumerateArray().Any(row =>
                string.Equals(
                    ReadString(row, "qualified_item_id"),
                    "(O)681",
                    StringComparison.Ordinal) &&
                ReadInt(row, "stack") > 0);
    }

    private static EventCandidate BlockedRainTotemCraftingCandidate(
        string reason,
        SmallModelActionParameter[] parameters) => new()
        {
            CandidateId = "master-angler:rain-totem-crafting:blocked",
            Kind = "craft_rain_totem",
            Available = false,
            ExpectedEffect =
                "master_angler_weather_supply_not_compiled;fresh_snapshot_required=true",
            AvailabilityClass =
                "master_angler_weather_supply_fail_closed",
            BlockReasons = new[]
            {
                string.IsNullOrWhiteSpace(reason)
                    ? "master_angler_weather_supply_validation_failed"
                    : reason
            },
            Parameters = parameters
        };
}
