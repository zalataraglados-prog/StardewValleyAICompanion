using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.OptionRegistry;

public sealed partial class CandidateOptionAvailabilityEvaluator
{
    private EventCandidate[] AnimalManagementCandidates(
        SnapshotEnvelope snapshot,
        SmallModelActionParameter[] intent)
    {
        var animalId = ManagementIntentParameter(intent, "animal_id");
        var managementIntent = ManagementIntentParameter(intent, "management_intent");
        var reason = ManagementIntentParameter(intent, "management_reason");
        if (string.IsNullOrWhiteSpace(animalId) ||
            managementIntent is not ("rename" or "toggle_reproduction" or "move_home" or "sell") ||
            string.IsNullOrWhiteSpace(reason))
        {
            return Array.Empty<EventCandidate>();
        }

        var animals = ReadStateFieldValue(snapshot, "farm", "animals");
        if (!animals.HasValue || animals.Value.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<EventCandidate>();
        }

        var animal = animals.Value.EnumerateArray().FirstOrDefault(row =>
            row.ValueKind == JsonValueKind.Object &&
            string.Equals(ReadStringOrNumber(row, "animal_id"), animalId, StringComparison.Ordinal));
        if (animal.ValueKind != JsonValueKind.Object ||
            !string.Equals(ReadString(animal, "runtime_type"), "StardewValley.FarmAnimal", StringComparison.Ordinal))
        {
            return Array.Empty<EventCandidate>();
        }

        var locationId = ReadString(animal, "location_id");
        var currentLocation = ReadStateFieldString(snapshot, "player", "location_id");
        var continuation = AnimalManagementContinuation(intent, animalId, managementIntent, reason);
        if (!string.Equals(currentLocation, locationId, StringComparison.OrdinalIgnoreCase))
        {
            var plan = FindResolvedRoutePlan(snapshot, currentLocation, locationId,
                RouteConnectorCandidates(snapshot, int.MaxValue).Where(value => value.Kind == "route_connector_tile").ToArray());
            return plan?.FirstActionCandidate is null
                ? Array.Empty<EventCandidate>()
                : new[]
                {
                    CloneCandidate(
                        plan.FirstActionCandidate,
                        candidateId: "animal-management-route:" + animalId + ":" + managementIntent + ":" + currentLocation,
                        expectedEffect: plan.FirstActionCandidate.ExpectedEffect + ";animal_management_target=" + animalId,
                        parameters: plan.FirstActionCandidate.Parameters.Concat(continuation).ToArray(),
                        availabilityClass: "animal_management_rolling_route")
                };
        }

        var x = ReadInt(animal, "tile_x");
        var y = ReadInt(animal, "tile_y");
        var stand = FindBestStandTile(snapshot, x, y);
        if (stand is null || ReadString(animal, "management_query_status") != "ready")
        {
            return Array.Empty<EventCandidate>();
        }

        var parameters = BuildAnimalManagementParameters(snapshot, animals.Value, animal, intent,
            animalId, managementIntent, reason, x, y, stand);
        if (parameters.Length == 0)
        {
            return Array.Empty<EventCandidate>();
        }

        var compilerReasons = CompilerProbeBlockingReasons(snapshot, new OptionAvailabilityCandidate
        {
            OptionId = "executor.manage_animal",
            Parameters = parameters
        });
        return new[]
        {
            new EventCandidate
            {
                CandidateId = "manage-animal:" + animalId + ":" + managementIntent,
                Kind = "manage_animal",
                Available = compilerReasons.Length == 0,
                LocationId = locationId,
                TileX = x,
                TileY = y,
                DisplayName = ReadString(animal, "display_name"),
                Quantity = 1,
                ExpectedEffect = AnimalManagementExpectedEffect(animal, intent, managementIntent),
                EstimatedTicks = Math.Max(180,
                    (Math.Abs(ReadStateFieldInt(snapshot, "player", "tile_x") - stand.X) +
                     Math.Abs(ReadStateFieldInt(snapshot, "player", "tile_y") - stand.Y)) * 60 + 180),
                EnergyCost = 0,
                AvailabilityClass = "transparent_native_animal_query_menu",
                BlockReasons = compilerReasons,
                Parameters = parameters
            }
        };
    }

    private static SmallModelActionParameter[] BuildAnimalManagementParameters(
        SnapshotEnvelope snapshot,
        JsonElement animals,
        JsonElement animal,
        SmallModelActionParameter[] intent,
        string animalId,
        string managementIntent,
        string reason,
        int x,
        int y,
        CandidateTile stand)
    {
        var targetName = ManagementIntentParameter(intent, "target_name");
        var targetAllow = ManagementIntentParameter(intent, "target_allow_reproduction");
        var saleConfirmed = ManagementIntentParameter(intent, "confirm_irreversible_sale");
        JsonElement? targetHome = null;

        if (managementIntent == "rename" &&
            (string.IsNullOrWhiteSpace(targetName) ||
             string.Equals(targetName, ReadString(animal, "display_name"), StringComparison.Ordinal) ||
             animals.EnumerateArray().Any(other =>
                 !string.Equals(ReadStringOrNumber(other, "animal_id"), animalId, StringComparison.Ordinal) &&
                 string.Equals(ReadString(other, "display_name"), targetName, StringComparison.Ordinal))))
        {
            return Array.Empty<SmallModelActionParameter>();
        }
        if (managementIntent == "toggle_reproduction" &&
            (ReadBool(animal, "management_can_toggle_reproduction") != true ||
             targetAllow is not ("true" or "false") ||
             bool.Parse(targetAllow) == ReadBool(animal, "management_allow_reproduction")))
        {
            return Array.Empty<SmallModelActionParameter>();
        }
        if (managementIntent == "sell" &&
            (saleConfirmed != "true" ||
             string.IsNullOrWhiteSpace(ReadString(animal, "management_home_building_type")) ||
             !NullableReadInt(animal, "management_home_building_tile_x").HasValue ||
             !NullableReadInt(animal, "management_home_building_tile_y").HasValue))
        {
            return Array.Empty<SmallModelActionParameter>();
        }
        if (managementIntent == "move_home")
        {
            var type = ManagementIntentParameter(intent, "target_home_building_type");
            var homeX = ManagementIntentInt(intent, "target_home_building_tile_x");
            var homeY = ManagementIntentInt(intent, "target_home_building_tile_y");
            if (string.IsNullOrWhiteSpace(type) || !homeX.HasValue || !homeY.HasValue ||
                !animal.TryGetProperty("management_compatible_move_homes", out var homes) ||
                homes.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<SmallModelActionParameter>();
            }
            var matchingHome = homes.EnumerateArray().FirstOrDefault(home =>
                ReadString(home, "building_type") == type &&
                ReadInt(home, "building_tile_x") == homeX.Value &&
                ReadInt(home, "building_tile_y") == homeY.Value &&
                ReadBool(home, "is_under_construction") != true &&
                ReadInt(home, "available_slots") > 0);
            if (matchingHome.ValueKind != JsonValueKind.Object)
            {
                return Array.Empty<SmallModelActionParameter>();
            }
            targetHome = matchingHome;
        }

        var result = new List<SmallModelActionParameter>
        {
            Parameter("management_intent", managementIntent),
            Parameter("management_reason", reason),
            Parameter("animal_id", animalId),
            Parameter("location_id", ReadString(animal, "location_id")),
            Parameter("target_location", ReadString(animal, "location_id")),
            Parameter("target_tile_x", x.ToString(CultureInfo.InvariantCulture)),
            Parameter("target_tile_y", y.ToString(CultureInfo.InvariantCulture)),
            Parameter("stand_tile_x", stand.X.ToString(CultureInfo.InvariantCulture)),
            Parameter("stand_tile_y", stand.Y.ToString(CultureInfo.InvariantCulture)),
            Parameter("target_runtime_type", ReadString(animal, "runtime_type")),
            Parameter("target_runtime_identity", animalId),
            Parameter("expected_name_before", ReadString(animal, "display_name")),
            Parameter("target_name", targetName),
            Parameter("safe_slot_index", ReadInt(animal, "management_safe_slot_index").ToString(CultureInfo.InvariantCulture)),
            Parameter("requires_initial_pet", (ReadBool(animal, "management_requires_initial_pet") == true).ToString().ToLowerInvariant()),
            Parameter("expected_allow_reproduction_before", (ReadBool(animal, "management_allow_reproduction") == true).ToString().ToLowerInvariant()),
            Parameter("target_allow_reproduction", targetAllow),
            Parameter("expected_sell_price", ReadInt(animal, "management_sell_price").ToString(CultureInfo.InvariantCulture)),
            Parameter("expected_money_before", ReadStateFieldInt(snapshot, "player", "money").ToString(CultureInfo.InvariantCulture)),
            Parameter("confirm_irreversible_sale", saleConfirmed),
            Parameter("expected_home_building_type_before", ReadString(animal, "management_home_building_type")),
            Parameter("expected_home_building_tile_x_before", NullableReadInt(animal, "management_home_building_tile_x")?.ToString(CultureInfo.InvariantCulture) ?? string.Empty),
            Parameter("expected_home_building_tile_y_before", NullableReadInt(animal, "management_home_building_tile_y")?.ToString(CultureInfo.InvariantCulture) ?? string.Empty),
            Parameter("max_movement_tiles", "512")
        };
        if (targetHome.HasValue)
        {
            result.Add(Parameter("target_home_building_type", ReadString(targetHome.Value, "building_type")));
            result.Add(Parameter("target_home_building_tile_x", ReadInt(targetHome.Value, "building_tile_x").ToString(CultureInfo.InvariantCulture)));
            result.Add(Parameter("target_home_building_tile_y", ReadInt(targetHome.Value, "building_tile_y").ToString(CultureInfo.InvariantCulture)));
            result.Add(Parameter("target_home_indoor_location_id", ReadString(targetHome.Value, "indoor_location_id")));
            result.Add(Parameter("expected_target_home_occupant_count_before", ReadInt(targetHome.Value, "occupant_count").ToString(CultureInfo.InvariantCulture)));
            result.Add(Parameter("expected_target_home_capacity", ReadInt(targetHome.Value, "capacity").ToString(CultureInfo.InvariantCulture)));
        }
        return result.ToArray();
    }

    private static SmallModelActionParameter[] AnimalManagementContinuation(
        SmallModelActionParameter[] intent,
        string animalId,
        string managementIntent,
        string reason)
    {
        var names = new[]
        {
            "target_name", "target_allow_reproduction", "confirm_irreversible_sale",
            "target_home_building_type", "target_home_building_tile_x", "target_home_building_tile_y"
        };
        return new[]
            {
                Parameter("continuation.option_id", "animals.manage_animal"),
                Parameter("continuation.animal_id", animalId),
                Parameter("continuation.management_intent", managementIntent),
                Parameter("continuation.management_reason", reason)
            }
            .Concat(names.Select(name => Parameter("continuation." + name, ManagementIntentParameter(intent, name))))
            .ToArray();
    }

    private static string AnimalManagementExpectedEffect(
        JsonElement animal,
        SmallModelActionParameter[] intent,
        string managementIntent) => managementIntent switch
    {
        "rename" => "animal_id=" + ReadStringOrNumber(animal, "animal_id") + ";name=" + ManagementIntentParameter(intent, "target_name"),
        "toggle_reproduction" => "animal_id=" + ReadStringOrNumber(animal, "animal_id") + ";allow_reproduction=" + ManagementIntentParameter(intent, "target_allow_reproduction"),
        "move_home" => "animal_id=" + ReadStringOrNumber(animal, "animal_id") + ";home=" + ManagementIntentParameter(intent, "target_home_building_type") + "@" + ManagementIntentParameter(intent, "target_home_building_tile_x") + "," + ManagementIntentParameter(intent, "target_home_building_tile_y"),
        "sell" => "animal_id=" + ReadStringOrNumber(animal, "animal_id") + ";sold=true;money_delta=" + ReadInt(animal, "management_sell_price"),
        _ => string.Empty
    };

    private static string ManagementIntentParameter(SmallModelActionParameter[] parameters, string name)
    {
        var value = IntentParameter(parameters, name);
        return string.IsNullOrWhiteSpace(value) ? IntentParameter(parameters, "continuation." + name) : value;
    }

    private static int? ManagementIntentInt(SmallModelActionParameter[] parameters, string name)
    {
        var value = ManagementIntentParameter(parameters, name);
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
    }
}
