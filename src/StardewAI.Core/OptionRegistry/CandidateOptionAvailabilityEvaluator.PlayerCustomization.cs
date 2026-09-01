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
    private static readonly string[] WizardCustomizationTargetNames =
    {
        "customization_name", "customization_favorite_thing", "customization_gender",
        "customization_skin_index", "customization_hair_style_id", "customization_accessory_index",
        "customization_eye_hue", "customization_eye_saturation", "customization_eye_value",
        "customization_hair_hue", "customization_hair_saturation", "customization_hair_value"
    };

    private EventCandidate[] PlayerCustomizationCandidates(
        SnapshotEnvelope snapshot,
        SmallModelActionParameter[] intent)
    {
        var mode = CustomizationIntent(intent, "customization_mode");
        var reason = CustomizationIntent(intent, "customization_reason");
        if (mode is not ("wizard_shrine" or "desert_makeover") || string.IsNullOrWhiteSpace(reason) ||
            CustomizationIntent(intent, "confirm_customization") != "true")
            return Array.Empty<EventCandidate>();

        var projection = ReadStateFieldValue(snapshot, "player", "customization");
        if (!projection.HasValue || projection.Value.ValueKind != JsonValueKind.Object ||
            ReadString(projection.Value, "projection_status") != "complete_locked_base_1.6.15" ||
            ReadString(projection.Value, "invocation_policy") != "player_command_only")
            return Array.Empty<EventCandidate>();
        var branchName = mode == "wizard_shrine" ? "wizard_shrine" : "desert_makeover";
        if (!projection.Value.TryGetProperty(branchName, out var branch) || branch.ValueKind != JsonValueKind.Object)
            return Array.Empty<EventCandidate>();
        var targetLocation = ReadString(branch, "location_id");
        var currentLocation = ReadStateFieldString(snapshot, "player", "location_id");
        var expectedRouteStatus = mode == "wizard_shrine"
            ? "route_to_wizard_shrine_required"
            : "route_to_desert_makeover_required";
        if (!string.Equals(currentLocation, targetLocation, StringComparison.OrdinalIgnoreCase))
            return ReadString(branch, "service_status") == expectedRouteStatus
                ? CustomizationRouteCandidates(snapshot, intent, projection.Value, mode, reason, currentLocation, targetLocation)
                : Array.Empty<EventCandidate>();

        return mode == "wizard_shrine"
            ? WizardCustomizationCandidate(snapshot, intent, projection.Value, branch, reason)
            : DesertMakeoverCandidate(snapshot, projection.Value, branch, reason);
    }

    private EventCandidate[] WizardCustomizationCandidate(
        SnapshotEnvelope snapshot,
        SmallModelActionParameter[] intent,
        JsonElement projection,
        JsonElement branch,
        string reason)
    {
        if (!projection.TryGetProperty("current", out var current) || current.ValueKind != JsonValueKind.Object ||
            !WizardCustomizationTargetNames.Any(name => intent.Any(parameter => parameter.Name == name)))
            return Array.Empty<EventCandidate>();
        if (ReadString(branch, "service_status") != "ready")
            return Array.Empty<EventCandidate>();
        var target = ResolveWizardCustomizationTarget(intent, current);
        if (target is null || !WizardCustomizationTargetValid(target, branch) || !WizardCustomizationChangesCurrent(target, current))
            return Array.Empty<EventCandidate>();
        var endpoint = CustomizationTiles(branch, "action_tiles", "WizardShrine")
            .Select(tile => new { tile, stand = FindBestStandTile(snapshot, tile.X, tile.Y) })
            .Where(row => row.stand is not null)
            .OrderBy(row => Math.Abs(ReadStateFieldInt(snapshot, "player", "tile_x") - row.stand!.X) +
                Math.Abs(ReadStateFieldInt(snapshot, "player", "tile_y") - row.stand!.Y))
            .FirstOrDefault();
        var reasons = new List<string>();
        if (endpoint is null)
            reasons.Add("player_customization_wizard_has_no_reachable_stand");
        var parameters = endpoint is null ? Array.Empty<SmallModelActionParameter>()
            : WizardCustomizationParameters(projection, branch, reason, target, endpoint.tile, endpoint.stand!);
        reasons.AddRange(CompilerProbeBlockingReasons(snapshot, new OptionAvailabilityCandidate
        {
            OptionId = "player.customize",
            Parameters = parameters,
            InvocationSource = OptionInvocationSource.PlayerCommand,
            ExplicitConfirmationGranted = true
        }));
        return new[] { CustomizationCandidate("wizard_shrine", projection, branch, endpoint?.tile.X,
            endpoint?.tile.Y, 900, "wizard_shrine_exact_character_state_applied=true", reasons, parameters) };
    }

    private EventCandidate[] DesertMakeoverCandidate(
        SnapshotEnvelope snapshot,
        JsonElement projection,
        JsonElement branch,
        string reason)
    {
        if (ReadBool(branch, "expected_outfit_available") != true)
            return Array.Empty<EventCandidate>();
        if (ReadString(branch, "service_status") != "ready")
            return Array.Empty<EventCandidate>();
        var tile = CustomizationTiles(branch, "touch_tiles", "DesertMakeover")
            .OrderBy(row => Math.Abs(ReadStateFieldInt(snapshot, "player", "tile_x") - row.X) +
                Math.Abs(ReadStateFieldInt(snapshot, "player", "tile_y") - row.Y)).FirstOrDefault();
        var reasons = new List<string>();
        if (tile is null)
            reasons.Add("player_customization_desert_touch_tile_missing");
        var parameters = tile is null ? Array.Empty<SmallModelActionParameter>()
            : DesertMakeoverParameters(projection, branch, reason, tile);
        reasons.AddRange(CompilerProbeBlockingReasons(snapshot, new OptionAvailabilityCandidate
        {
            OptionId = "player.customize",
            Parameters = parameters,
            InvocationSource = OptionInvocationSource.PlayerCommand,
            ExplicitConfirmationGranted = true
        }));
        return new[] { CustomizationCandidate("desert_makeover", projection, branch, tile?.X, tile?.Y,
            1500, "desert_makeover_expected_outfit_applied=true", reasons, parameters) };
    }

    private EventCandidate[] CustomizationRouteCandidates(
        SnapshotEnvelope snapshot,
        SmallModelActionParameter[] intent,
        JsonElement projection,
        string mode,
        string reason,
        string currentLocation,
        string targetLocation)
    {
        var route = FindResolvedRoutePlan(snapshot, currentLocation, targetLocation,
            RouteConnectorCandidates(snapshot, int.MaxValue)
                .Where(candidate => candidate.Kind == "route_connector_tile").ToArray());
        if (route?.FirstActionCandidate is null)
            return Array.Empty<EventCandidate>();
        var continuation = intent.Where(parameter => parameter.Name.StartsWith("customization_", StringComparison.Ordinal))
            .Select(parameter => Parameter("continuation." + parameter.Name, parameter.Value))
            .Concat(new[]
            {
                Parameter("continuation.option_id", "player.customize"),
                Parameter("continuation.customization_mode", mode),
                Parameter("continuation.customization_reason", reason),
                Parameter("continuation.confirm_customization", "true")
            }).GroupBy(parameter => parameter.Name, StringComparer.Ordinal).Select(group => group.Last()).ToArray();
        return new[]
        {
            CloneCandidate(route.FirstActionCandidate,
                candidateId: "player-customization-route:" + mode + ":" + currentLocation,
                expectedEffect: route.FirstActionCandidate.ExpectedEffect + ";customization_continuation=" + mode,
                parameters: route.FirstActionCandidate.Parameters.Concat(continuation).ToArray(),
                availabilityClass: "player_customization_player_command_rolling_route")
        };
    }

    private static EventCandidate CustomizationCandidate(
        string mode, JsonElement projection, JsonElement branch, int? x, int? y, int ticks,
        string effect, List<string> reasons, SmallModelActionParameter[] parameters) => new()
    {
        CandidateId = "player-customization:" + mode + ":" +
            (ReadString(projection, "projection_fingerprint") is { Length: >= 12 } fingerprint ? fingerprint[..12] : "invalid"),
        Kind = "customize_player",
        Available = reasons.Count == 0,
        AllowedNow = reasons.Count == 0,
        AllowedToday = reasons.Count == 0,
        LocationId = ReadString(branch, "location_id"),
        TileX = x,
        TileY = y,
        EstimatedTicks = ticks,
        EnergyCost = 0,
        AvailabilityClass = "explicit_player_command_native_" + mode,
        ExpectedEffect = effect,
        BlockReasons = reasons.Distinct(StringComparer.Ordinal).ToArray(),
        Parameters = parameters
    };

    private static WizardCustomizationTarget? ResolveWizardCustomizationTarget(
        SmallModelActionParameter[] intent, JsonElement current)
    {
        string Text(string name, string fallback) => intent.FirstOrDefault(p => p.Name == name)?.Value ?? fallback;
        int? Number(string name, int fallback) => int.TryParse(Text(name, fallback.ToString(CultureInfo.InvariantCulture)),
            NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : null;
        var values = new int?[]
        {
            Number("customization_skin_index", ReadInt(current, "skin_index")),
            Number("customization_hair_style_id", ReadInt(current, "hair_style_id")),
            Number("customization_accessory_index", ReadInt(current, "accessory_index"))
        };
        if (values.Any(value => !value.HasValue) || !current.TryGetProperty("eye_hsv", out var eye) ||
            !current.TryGetProperty("hair_hsv", out var hair))
            return null;
        var sliders = new int?[]
        {
            Number("customization_eye_hue", ReadInt(eye, "hue")),
            Number("customization_eye_saturation", ReadInt(eye, "saturation")),
            Number("customization_eye_value", ReadInt(eye, "value")),
            Number("customization_hair_hue", ReadInt(hair, "hue")),
            Number("customization_hair_saturation", ReadInt(hair, "saturation")),
            Number("customization_hair_value", ReadInt(hair, "value"))
        };
        if (sliders.Any(value => !value.HasValue))
            return null;
        return new(Text("customization_name", ReadString(current, "Name")),
            Text("customization_favorite_thing", ReadString(current, "favorite_thing")),
            Text("customization_gender", ReadString(current, "gender")),
            values[0]!.Value, values[1]!.Value, values[2]!.Value,
            sliders[0]!.Value, sliders[1]!.Value, sliders[2]!.Value,
            sliders[3]!.Value, sliders[4]!.Value, sliders[5]!.Value);
    }

    private static bool WizardCustomizationTargetValid(WizardCustomizationTarget target, JsonElement branch) =>
        !string.IsNullOrWhiteSpace(target.Name) && !string.IsNullOrWhiteSpace(target.FavoriteThing) &&
        !target.Name.Any(char.IsControl) && !target.FavoriteThing.Any(char.IsControl) &&
        target.Gender is "male" or "female" && target.Skin is >= 0 and <= 23 &&
        target.Accessory is >= -1 and <= 29 &&
        branch.TryGetProperty("hair_style_ids", out var rows) && rows.ValueKind == JsonValueKind.Array &&
        rows.EnumerateArray().Any(row => row.TryGetInt32(out var id) && id == target.Hair) &&
        new[] { target.EyeH, target.EyeS, target.EyeV, target.HairH, target.HairS, target.HairV }
            .All(value => value is >= 0 and <= 100);

    private static bool WizardCustomizationChangesCurrent(WizardCustomizationTarget target, JsonElement current) =>
        target.Name != ReadString(current, "Name") || target.FavoriteThing != ReadString(current, "favorite_thing") ||
        target.Gender != ReadString(current, "gender") || target.Skin != ReadInt(current, "skin_index") ||
        target.Hair != ReadInt(current, "hair_style_id") || target.Accessory != ReadInt(current, "accessory_index") ||
        !current.TryGetProperty("eye_hsv", out var eye) || !current.TryGetProperty("hair_hsv", out var hair) ||
        target.EyeH != ReadInt(eye, "hue") || target.EyeS != ReadInt(eye, "saturation") ||
        target.EyeV != ReadInt(eye, "value") || target.HairH != ReadInt(hair, "hue") ||
        target.HairS != ReadInt(hair, "saturation") || target.HairV != ReadInt(hair, "value");

    private static SmallModelActionParameter[] WizardCustomizationParameters(
        JsonElement projection, JsonElement branch, string reason, WizardCustomizationTarget target,
        CustomizationTile tile, CandidateTile stand) =>
        CustomizationBaseParameters(projection, branch, "wizard_shrine", reason, tile, stand.X, stand.Y)
            .Concat(new[]
            {
                Parameter("customization_name", target.Name), Parameter("customization_favorite_thing", target.FavoriteThing),
                Parameter("customization_gender", target.Gender), Parameter("customization_skin_index", target.Skin.ToString(CultureInfo.InvariantCulture)),
                Parameter("customization_hair_style_id", target.Hair.ToString(CultureInfo.InvariantCulture)),
                Parameter("customization_accessory_index", target.Accessory.ToString(CultureInfo.InvariantCulture)),
                Parameter("customization_eye_hue", target.EyeH.ToString(CultureInfo.InvariantCulture)),
                Parameter("customization_eye_saturation", target.EyeS.ToString(CultureInfo.InvariantCulture)),
                Parameter("customization_eye_value", target.EyeV.ToString(CultureInfo.InvariantCulture)),
                Parameter("customization_hair_hue", target.HairH.ToString(CultureInfo.InvariantCulture)),
                Parameter("customization_hair_saturation", target.HairS.ToString(CultureInfo.InvariantCulture)),
                Parameter("customization_hair_value", target.HairV.ToString(CultureInfo.InvariantCulture)),
                Parameter("customization_price_gold", ReadInt(branch, "price_gold").ToString(CultureInfo.InvariantCulture)),
                Parameter("customization_money_before", ReadInt(branch, "money_before").ToString(CultureInfo.InvariantCulture)),
                Parameter("expected_menu_type_after", "CharacterCustomization"), Parameter("expected_menu_kind", "wizard")
            }).ToArray();

    private static SmallModelActionParameter[] DesertMakeoverParameters(
        JsonElement projection, JsonElement branch, string reason, CustomizationTile tile)
    {
        var parts = ReadExpectedMakeoverParts(branch);
        return CustomizationBaseParameters(projection, branch, "desert_makeover", reason, tile, tile.X, tile.Y)
            .Concat(new[]
            {
                Parameter("customization_stylist_name", ReadString(branch, "stylist_name")),
                Parameter("customization_passive_festival_day", ReadInt(branch, "passive_festival_day").ToString(CultureInfo.InvariantCulture)),
                Parameter("customization_free_inventory_slots", ReadInt(branch, "free_inventory_slots").ToString(CultureInfo.InvariantCulture)),
                Parameter("customization_equipped_item_count", ReadInt(branch, "equipped_item_count").ToString(CultureInfo.InvariantCulture)),
                Parameter("customization_expected_outfit_index", ReadInt(branch, "expected_outfit_index").ToString(CultureInfo.InvariantCulture)),
                Parameter("customization_uses_player_seed", (ReadBool(branch, "uses_player_seed") == true).ToString().ToLowerInvariant()),
                Parameter("customization_special_laurel_outfit", (ReadBool(branch, "special_laurel_outfit") == true).ToString().ToLowerInvariant()),
                Parameter("customization_expected_hat_qid", parts.GetValueOrDefault("hat").Qid),
                Parameter("customization_expected_hat_color", parts.GetValueOrDefault("hat").Color),
                Parameter("customization_expected_shirt_qid", parts.GetValueOrDefault("shirt").Qid),
                Parameter("customization_expected_shirt_color", parts.GetValueOrDefault("shirt").Color),
                Parameter("customization_expected_pants_qid", parts.GetValueOrDefault("pants").Qid),
                Parameter("customization_expected_pants_color", parts.GetValueOrDefault("pants").Color),
                Parameter("expected_menu_type_after", "none"), Parameter("expected_menu_kind", "desert_makeover_event")
            }).ToArray();
    }

    private static IEnumerable<SmallModelActionParameter> CustomizationBaseParameters(
        JsonElement projection, JsonElement branch, string mode, string reason,
        CustomizationTile tile, int standX, int standY) => new[]
    {
        Parameter("customization_mode", mode), Parameter("customization_reason", reason),
        Parameter("confirm_customization", "true"),
        Parameter("customization_projection_fingerprint", ReadString(projection, "projection_fingerprint")),
        Parameter("target_location", ReadString(branch, "location_id")),
        Parameter("target_tile_x", tile.X.ToString(CultureInfo.InvariantCulture)),
        Parameter("target_tile_y", tile.Y.ToString(CultureInfo.InvariantCulture)),
        Parameter("stand_tile_x", standX.ToString(CultureInfo.InvariantCulture)),
        Parameter("stand_tile_y", standY.ToString(CultureInfo.InvariantCulture)),
        Parameter("customization_action_raw", tile.ActionRaw), Parameter("customization_action_token", tile.Token),
        Parameter("native_contract", ReadString(projection, "native_contract")), Parameter("max_movement_tiles", "512")
    };

    private static Dictionary<string, MakeoverPart> ReadExpectedMakeoverParts(JsonElement branch)
    {
        var result = new Dictionary<string, MakeoverPart>(StringComparer.Ordinal)
        {
            ["hat"] = MakeoverPart.Empty, ["shirt"] = MakeoverPart.Empty, ["pants"] = MakeoverPart.Empty
        };
        if (!branch.TryGetProperty("expected_parts", out var parts) || parts.ValueKind != JsonValueKind.Array)
            return result;
        foreach (var part in parts.EnumerateArray().Where(part => part.ValueKind == JsonValueKind.Object))
            result[ReadString(part, "slot")] = new MakeoverPart(
                ReadString(part, "qualified_item_id"), ReadString(part, "color"));
        return result;
    }

    private static CustomizationTile[] CustomizationTiles(JsonElement branch, string property, string token) =>
        branch.TryGetProperty(property, out var rows) && rows.ValueKind == JsonValueKind.Array
            ? rows.EnumerateArray().Where(row => row.ValueKind == JsonValueKind.Object && ReadString(row, "action_token") == token)
                .Select(row => new CustomizationTile(ReadInt(row, "tile_x"), ReadInt(row, "tile_y"),
                    ReadString(row, "action_raw"), token)).ToArray()
            : Array.Empty<CustomizationTile>();

    private static string CustomizationIntent(SmallModelActionParameter[] intent, string name) =>
        intent.FirstOrDefault(parameter => string.Equals(parameter.Name, name, StringComparison.Ordinal))?.Value ?? string.Empty;

    private sealed record WizardCustomizationTarget(string Name, string FavoriteThing, string Gender, int Skin, int Hair,
        int Accessory, int EyeH, int EyeS, int EyeV, int HairH, int HairS, int HairV);
    private sealed record CustomizationTile(int X, int Y, string ActionRaw, string Token);
    private sealed record MakeoverPart(string Qid, string Color)
    {
        public static readonly MakeoverPart Empty = new(string.Empty, string.Empty);
    }
}
