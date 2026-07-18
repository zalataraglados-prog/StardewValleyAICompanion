using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.OptionRegistry;

public sealed partial class CandidateOptionAvailabilityEvaluator
{
    private EventCandidate[] PetCareCandidates(SnapshotEnvelope snapshot)
    {
        return PetInteractionCandidates(snapshot).Concat(PetBowlCandidates(snapshot)).ToArray();
    }

    private EventCandidate[] PetInteractionCandidates(SnapshotEnvelope snapshot)
    {
        var pets = ReadStateFieldValue(snapshot, "farm", "pets");
        if (!pets.HasValue || pets.Value.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<EventCandidate>();
        }

        var currentLocation = ReadStateFieldString(snapshot, "player", "location_id");
        var playerX = ReadStateFieldInt(snapshot, "player", "tile_x");
        var playerY = ReadStateFieldInt(snapshot, "player", "tile_y");
        return pets.Value.EnumerateArray()
            .Where(pet => pet.ValueKind == JsonValueKind.Object)
            .Select(pet =>
            {
                var petId = ReadString(pet, "pet_id");
                var locationId = ReadString(pet, "location_id");
                var x = ReadInt(pet, "tile_x");
                var y = ReadInt(pet, "tile_y");
                var sameLocation = string.Equals(locationId, currentLocation, StringComparison.OrdinalIgnoreCase);
                var stand = sameLocation ? FindBestStandTile(snapshot, x, y) : null;
                var reasons = new List<string>();
                var status = ReadString(pet, "action_status");
                if (status != "ready")
                {
                    reasons.Add(string.IsNullOrWhiteSpace(status) ? "pet_interaction_projection_unavailable" : status);
                }
                if (!sameLocation)
                {
                    reasons.Add("pet_not_in_current_location");
                }
                if (stand is null && sameLocation)
                {
                    reasons.Add("pet_no_reachable_adjacent_stand_tile");
                }
                if (string.IsNullOrWhiteSpace(petId))
                {
                    reasons.Add("pet_identity_incomplete");
                }

                var parameters = stand is null ? Array.Empty<SmallModelActionParameter>() : PetInteractionParameters(pet, stand.X, stand.Y);
                if (parameters.Length > 0)
                {
                    reasons.AddRange(CompilerProbeBlockingReasons(snapshot, new OptionAvailabilityCandidate
                    {
                        OptionId = "executor.pet_interact",
                        Parameters = parameters
                    }));
                }
                var distance = stand is null ? 0 : Math.Abs(playerX - stand.X) + Math.Abs(playerY - stand.Y);
                return new EventCandidate
                {
                    CandidateId = "pet-daily-interaction:" + petId,
                    Kind = "pet_daily_interaction",
                    Available = reasons.Count == 0,
                    LocationId = locationId,
                    TileX = x,
                    TileY = y,
                    ExpectedEffect = PetInteractionExpectedEffect(pet),
                    EstimatedTicks = Math.Max(120, distance * 60 + 120),
                    EnergyCost = 0,
                    AvailabilityClass = "transparent_native_pet_check_action",
                    BlockReasons = reasons.Distinct(StringComparer.Ordinal).ToArray(),
                    Parameters = parameters
                };
            })
            .ToArray();
    }

    private EventCandidate[] PetBowlCandidates(SnapshotEnvelope snapshot)
    {
        var bowls = ReadStateFieldValue(snapshot, "farm", "pet_bowls");
        if (!bowls.HasValue || bowls.Value.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<EventCandidate>();
        }

        var currentLocation = ReadStateFieldString(snapshot, "player", "location_id");
        var playerX = ReadStateFieldInt(snapshot, "player", "tile_x");
        var playerY = ReadStateFieldInt(snapshot, "player", "tile_y");
        return bowls.Value.EnumerateArray()
            .Where(bowl => bowl.ValueKind == JsonValueKind.Object)
            .Select(bowl =>
            {
                var locationId = ReadString(bowl, "location_id");
                var x = NullableReadInt(bowl, "action_tile_x");
                var y = NullableReadInt(bowl, "action_tile_y");
                var sameLocation = string.Equals(locationId, currentLocation, StringComparison.OrdinalIgnoreCase);
                var stand = sameLocation && x.HasValue && y.HasValue ? FindBestStandTile(snapshot, x.Value, y.Value) : null;
                var reasons = new List<string>();
                var status = ReadString(bowl, "action_status");
                if (status != "ready")
                {
                    reasons.Add(string.IsNullOrWhiteSpace(status) ? "pet_bowl_projection_unavailable" : status);
                }
                if (!sameLocation)
                {
                    reasons.Add("pet_bowl_not_in_current_location");
                }
                if ((!x.HasValue || !y.HasValue) && status == "ready")
                {
                    reasons.Add("pet_bowl_action_tile_unavailable");
                }
                if (stand is null && sameLocation && x.HasValue && y.HasValue)
                {
                    reasons.Add("pet_bowl_no_reachable_adjacent_stand_tile");
                }

                var parameters = stand is null || !x.HasValue || !y.HasValue
                    ? Array.Empty<SmallModelActionParameter>()
                    : PetBowlParameters(bowl, x.Value, y.Value, stand.X, stand.Y);
                if (parameters.Length > 0)
                {
                    reasons.AddRange(CompilerProbeBlockingReasons(snapshot, new OptionAvailabilityCandidate
                    {
                        OptionId = "executor.fill_pet_bowl",
                        Parameters = parameters
                    }));
                }
                var distance = stand is null ? 0 : Math.Abs(playerX - stand.X) + Math.Abs(playerY - stand.Y);
                return new EventCandidate
                {
                    CandidateId = "fill-pet-bowl:" + locationId + ":" + ReadInt(bowl, "building_tile_x") + "," + ReadInt(bowl, "building_tile_y"),
                    Kind = "fill_pet_bowl",
                    Available = reasons.Count == 0,
                    LocationId = locationId,
                    TileX = x,
                    TileY = y,
                    ExpectedEffect = PetBowlExpectedEffect(bowl),
                    EstimatedTicks = Math.Max(120, distance * 60 + 120),
                    EnergyCost = ReadDouble(bowl, "watering_energy_cost"),
                    AvailabilityClass = "transparent_native_pet_bowl_watering",
                    BlockReasons = reasons.Distinct(StringComparer.Ordinal).ToArray(),
                    Parameters = parameters
                };
            })
            .ToArray();
    }

    private static SmallModelActionParameter[] PetInteractionParameters(JsonElement pet, int standX, int standY)
    {
        return new[]
        {
            Parameter("target_location", ReadString(pet, "location_id")),
            Parameter("location_id", ReadString(pet, "location_id")),
            Parameter("target_tile_x", ReadInt(pet, "tile_x").ToString()),
            Parameter("target_tile_y", ReadInt(pet, "tile_y").ToString()),
            Parameter("stand_tile_x", standX.ToString()),
            Parameter("stand_tile_y", standY.ToString()),
            Parameter("target_runtime_type", ReadString(pet, "runtime_type")),
            Parameter("target_runtime_identity", ReadString(pet, "pet_id")),
            Parameter("target_name", ReadString(pet, "name")),
            Parameter("safe_slot_index", ReadInt(pet, "safe_slot_index").ToString()),
            Parameter("expected_friendship_before", ReadInt(pet, "friendship_toward_farmer").ToString()),
            Parameter("expected_friendship_after", ReadInt(pet, "friendship_after_daily_interaction").ToString()),
            Parameter("expected_last_pet_day_before", NullableReadInt(pet, "last_pet_day_for_player")?.ToString() ?? "missing"),
            Parameter("expected_last_pet_day_after", ReadInt(pet, "current_total_days").ToString()),
            Parameter("expected_times_pet_before", ReadInt(pet, "times_pet_before").ToString()),
            Parameter("expected_times_pet_after", ReadInt(pet, "times_pet_after_daily_interaction").ToString()),
            Parameter("expected_granted_friendship_before", (ReadBool(pet, "granted_friendship_for_pet") == true).ToString().ToLowerInvariant()),
            Parameter("expected_granted_friendship_after", (ReadBool(pet, "granted_friendship_after_daily_interaction") == true).ToString().ToLowerInvariant()),
            Parameter("expected_pet_love_mail_before", (ReadBool(pet, "pet_love_mail_before") == true).ToString().ToLowerInvariant()),
            Parameter("expected_pet_love_mail_after", (ReadBool(pet, "pet_love_mail_after_daily_interaction") == true).ToString().ToLowerInvariant()),
            Parameter("expected_marnie_pet_adoption_mail_before_or_pending", (ReadBool(pet, "marnie_pet_adoption_mail_before_or_pending") == true).ToString().ToLowerInvariant()),
            Parameter("expected_marnie_pet_adoption_mail_after_or_pending", (ReadBool(pet, "marnie_pet_adoption_mail_after_daily_interaction") == true).ToString().ToLowerInvariant()),
            Parameter("pet_gift_trigger_expected", (ReadBool(pet, "gift_trigger_will_succeed") == true).ToString().ToLowerInvariant()),
            Parameter("pet_gift_selection_status", ReadString(pet, "gift_selection_status")),
            Parameter("pet_love_progress_delta", ReadInt(pet, "daily_interaction_friendship_delta").ToString()),
            Parameter("max_movement_tiles", "512")
        };
    }

    private static SmallModelActionParameter[] PetBowlParameters(JsonElement bowl, int x, int y, int standX, int standY)
    {
        return new[]
        {
            Parameter("target_location", ReadString(bowl, "location_id")),
            Parameter("location_id", ReadString(bowl, "location_id")),
            Parameter("target_tile_x", x.ToString()),
            Parameter("target_tile_y", y.ToString()),
            Parameter("stand_tile_x", standX.ToString()),
            Parameter("stand_tile_y", standY.ToString()),
            Parameter("building_tile_x", ReadInt(bowl, "building_tile_x").ToString()),
            Parameter("building_tile_y", ReadInt(bowl, "building_tile_y").ToString()),
            Parameter("target_runtime_type", ReadString(bowl, "runtime_type")),
            Parameter("target_runtime_identity", ReadString(bowl, "assigned_pet_id")),
            Parameter("required_tool_kind", "Watering Can"),
            Parameter("tool_slot_index", ReadInt(bowl, "watering_can_slot_index").ToString()),
            Parameter("watering_can_slot_index", ReadInt(bowl, "watering_can_slot_index").ToString()),
            Parameter("expected_water_before", ReadInt(bowl, "watering_can_water_left").ToString()),
            Parameter("expected_water_after", ReadInt(bowl, "expected_watering_can_water_after").ToString()),
            Parameter("expected_watering_can_bottomless", (ReadBool(bowl, "watering_can_bottomless") == true).ToString().ToLowerInvariant()),
            Parameter("expected_bowl_watered_before", (ReadBool(bowl, "watered") == true).ToString().ToLowerInvariant()),
            Parameter("expected_bowl_watered_after", "true"),
            Parameter("expected_friendship_before", NullableReadInt(bowl, "friendship_before_next_day")?.ToString() ?? "missing"),
            Parameter("expected_next_day_friendship_after", NullableReadInt(bowl, "friendship_after_fill_and_next_day_update")?.ToString() ?? "missing"),
            Parameter("expected_pet_love_mail_before", (ReadBool(bowl, "pet_love_mail_before") == true).ToString().ToLowerInvariant()),
            Parameter("expected_next_day_pet_love_mail", (ReadBool(bowl, "pet_love_mail_after_fill_and_next_day_update") == true).ToString().ToLowerInvariant()),
            Parameter("expected_marnie_pet_adoption_mail_before_or_pending", (ReadBool(bowl, "marnie_pet_adoption_mail_before_or_pending") == true).ToString().ToLowerInvariant()),
            Parameter("expected_next_day_marnie_pet_adoption_mail", (ReadBool(bowl, "marnie_pet_adoption_mail_after_fill_and_next_day_update") == true).ToString().ToLowerInvariant()),
            Parameter("pet_love_progress_delta", NullableReadInt(bowl, "delayed_friendship_delta")?.ToString() ?? "0"),
            Parameter("delayed_settlement", ReadString(bowl, "delayed_settlement")),
            Parameter("expected_energy_cost", ReadDouble(bowl, "watering_energy_cost").ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)),
            Parameter("max_movement_tiles", "512")
        };
    }

    private static string PetInteractionExpectedEffect(JsonElement pet)
    {
        return "pet_id=" + ReadString(pet, "pet_id") +
            ";friendship_before=" + ReadInt(pet, "friendship_toward_farmer") +
            ";friendship_after=" + ReadInt(pet, "friendship_after_daily_interaction") +
            ";last_pet_day=" + ReadInt(pet, "current_total_days") +
            ";times_pet_after=" + ReadInt(pet, "times_pet_after_daily_interaction") +
            ";pet_love_mail_after=" + (ReadBool(pet, "pet_love_mail_after_daily_interaction") == true).ToString().ToLowerInvariant() +
            ";marnie_pet_adoption_mail_after_or_pending=" + (ReadBool(pet, "marnie_pet_adoption_mail_after_daily_interaction") == true).ToString().ToLowerInvariant() +
            ";gift_trigger=" + (ReadBool(pet, "gift_trigger_will_succeed") == true).ToString().ToLowerInvariant() +
            ";gift_selection_status=" + ReadString(pet, "gift_selection_status");
    }

    private static string PetBowlExpectedEffect(JsonElement bowl)
    {
        return "pet_bowl_watered=true" +
            ";assigned_pet_id=" + ReadString(bowl, "assigned_pet_id") +
            ";immediate_friendship_delta=0" +
            ";next_day_friendship_after=" + NullableReadInt(bowl, "friendship_after_fill_and_next_day_update") +
            ";next_day_pet_love_mail=" + (ReadBool(bowl, "pet_love_mail_after_fill_and_next_day_update") == true).ToString().ToLowerInvariant() +
            ";next_day_marnie_pet_adoption_mail=" + (ReadBool(bowl, "marnie_pet_adoption_mail_after_fill_and_next_day_update") == true).ToString().ToLowerInvariant() +
            ";expected_energy_cost=" + ReadDouble(bowl, "watering_energy_cost").ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) +
            ";expected_water_after=" + ReadInt(bowl, "expected_watering_can_water_after") +
            ";settlement=Pet.dayUpdate";
    }
}
