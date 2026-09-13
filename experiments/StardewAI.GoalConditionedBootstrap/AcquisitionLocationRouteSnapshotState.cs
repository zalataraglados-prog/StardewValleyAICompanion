using System.Text.Json;
using StardewAI.Core.Training;

namespace StardewAI.GoalConditionedBootstrap;

internal sealed class AcquisitionLocationRouteSnapshotState
{
    private readonly IReadOnlyDictionary<string, AcquisitionRouteLocationState>
        locations;
    private readonly IReadOnlyDictionary<string, string> weatherByContext;

    private AcquisitionLocationRouteSnapshotState(
        string currentLocationId,
        int currentTileX,
        int currentTileY,
        int currentTime,
        string farmTypeKey,
        JsonElement routeGraph,
        JsonElement socialRouteDateEvidence,
        JsonElement crabPotNetwork,
        FutureRouteTimingCalibration? timing,
        IReadOnlyDictionary<string, AcquisitionRouteLocationState> locations,
        IReadOnlyDictionary<string, string> weatherByContext,
        string currentWeather,
        bool locationCapabilitiesComplete,
        string[] routeEvidenceBlockingReasons)
    {
        CurrentLocationId = currentLocationId;
        CurrentTileX = currentTileX;
        CurrentTileY = currentTileY;
        CurrentTime = currentTime;
        FarmTypeKey = farmTypeKey;
        RouteGraph = routeGraph;
        SocialRouteDateEvidence = socialRouteDateEvidence;
        CrabPotNetwork = crabPotNetwork;
        Timing = timing;
        this.locations = locations;
        this.weatherByContext = weatherByContext;
        CurrentWeather = currentWeather;
        LocationCapabilitiesComplete = locationCapabilitiesComplete;
        RouteEvidenceBlockingReasons = routeEvidenceBlockingReasons;
    }

    public string CurrentLocationId { get; }

    public int CurrentTileX { get; }

    public int CurrentTileY { get; }

    public int CurrentTime { get; }

    public string FarmTypeKey { get; }

    public JsonElement RouteGraph { get; }

    public JsonElement SocialRouteDateEvidence { get; }

    public JsonElement CrabPotNetwork { get; }

    public FutureRouteTimingCalibration? Timing { get; }

    public string CurrentWeather { get; }

    public bool LocationCapabilitiesComplete { get; }

    public string[] RouteEvidenceBlockingReasons { get; }

    public bool RouteEvidenceAvailable =>
        RouteEvidenceBlockingReasons.Length == 0 && Timing is not null;

    public IEnumerable<AcquisitionRouteLocationState> Locations =>
        locations.Values;

    public bool TryGetLocation(
        string locationId,
        out AcquisitionRouteLocationState location) =>
        locations.TryGetValue(locationId, out location!);

    public bool TryGetWeather(
        string locationId,
        out string weather,
        out string[] evidencePaths)
    {
        weather = string.Empty;
        evidencePaths = Array.Empty<string>();
        if (locations.TryGetValue(locationId, out var location) &&
            !string.IsNullOrWhiteSpace(location.LocationContextId) &&
            weatherByContext.TryGetValue(
                location.LocationContextId,
                out var contextWeather))
        {
            weather = contextWeather;
            evidencePaths = new[]
            {
                "state.locations.social_route_date_evidence.value.locations[].location_context_id",
                "state.time.location_context_weather.value[]"
            };
            return true;
        }
        if (string.Equals(
                locationId,
                CurrentLocationId,
                StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(CurrentWeather))
        {
            weather = CurrentWeather;
            evidencePaths = new[]
            {
                "state.time.weather.value",
                "state.time.is_green_rain.value"
            };
            return true;
        }
        return false;
    }

    public static AcquisitionLocationRouteSnapshotState Read(
        JsonElement snapshot,
        string expectedGameVersion,
        int targetTotalDay,
        string timingCalibrationPath)
    {
        var state = RequiredObject(snapshot, "state");
        var currentLocation = RequiredFieldString(state, "player", "location_id");
        var currentTileX = RequiredFieldInt(state, "player", "tile_x");
        var currentTileY = RequiredFieldInt(state, "player", "tile_y");
        var currentTime = RequiredFieldInt(state, "time", "time");
        var reasons = new List<string>();

        var routeGraph = OptionalFieldValue(
            state,
            "locations",
            "route_graph",
            "location_route_graph_missing",
            reasons);
        var social = OptionalFieldValue(
            state,
            "locations",
            "social_route_date_evidence",
            "location_route_date_evidence_missing",
            reasons);
        var movement = OptionalFieldValue(
            state,
            "player",
            "movement_timing_context",
            "location_route_movement_timing_context_missing",
            reasons);
        var crabPots = TryFieldValue(
            state,
            "player",
            "crab_pot_network",
            out var crabPotValue)
            ? crabPotValue.Clone()
            : default;

        FutureRouteTimingCalibration? timing = null;
        if (movement.ValueKind != JsonValueKind.Undefined)
        {
            var load = new FutureRouteTimingCalibrationLoader().Load(
                File.ReadAllText(Path.GetFullPath(timingCalibrationPath)),
                movement,
                expectedGameVersion,
                targetTotalDay);
            if (load.Status == FutureRouteTimingCalibrationLoadStatus.Loaded)
                timing = load.Calibration;
            else
                reasons.AddRange(load.BlockingReasons);
        }

        var locationRows = ReadLocations(social, targetTotalDay, reasons);
        var weather = ReadWeatherByContext(state);
        var currentWeather = ReadCurrentWeather(state);
        var farmTypeKey = TryFieldValue(
                state,
                "farm",
                "farm_type_key",
                out var farmTypeKeyValue) &&
            farmTypeKeyValue.ValueKind == JsonValueKind.String
                ? farmTypeKeyValue.GetString() ?? string.Empty
                : ReadLegacyFarmTypeKey(state);
        var locationCapabilitiesComplete = locationRows.Count > 0 &&
            locationRows.Values.All(row => row.SeedsIgnoreSeasonsHere.HasValue);

        return new AcquisitionLocationRouteSnapshotState(
            currentLocation,
            currentTileX,
            currentTileY,
            currentTime,
            farmTypeKey,
            routeGraph,
            social,
            crabPots,
            timing,
            locationRows,
            weather,
            currentWeather,
            locationCapabilitiesComplete,
            reasons.Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray());
    }

    private static Dictionary<string, AcquisitionRouteLocationState>
        ReadLocations(
            JsonElement social,
            int targetTotalDay,
            ICollection<string> reasons)
    {
        var result = new Dictionary<string, AcquisitionRouteLocationState>(
            StringComparer.OrdinalIgnoreCase);
        if (social.ValueKind == JsonValueKind.Undefined)
            return result;
        if (ReadString(social, "schema_version") !=
                "social_route_date_evidence.v2" ||
            ReadInt(social, "capture_total_days") != targetTotalDay ||
            ReadBool(social, "all_location_static_walkability_complete") != true ||
            !social.TryGetProperty("locations", out var rows) ||
            rows.ValueKind != JsonValueKind.Array)
        {
            reasons.Add("location_route_date_evidence_invalid");
            return result;
        }
        foreach (var row in rows.EnumerateArray())
        {
            var locationId = ReadString(row, "location_id");
            if (string.IsNullOrWhiteSpace(locationId) ||
                result.ContainsKey(locationId))
            {
                reasons.Add("location_route_date_location_identity_invalid");
                continue;
            }
            result[locationId] = new AcquisitionRouteLocationState(
                locationId,
                ReadString(row, "location_context_id"),
                ReadNullableBool(row, "seeds_ignore_seasons_here"));
        }
        if (result.Count != rows.GetArrayLength())
            reasons.Add("location_route_date_location_count_invalid");
        return result;
    }

    private static Dictionary<string, string> ReadWeatherByContext(
        JsonElement state)
    {
        var result = new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase);
        if (!TryFieldValue(
                state,
                "time",
                "location_context_weather",
                out var rows) ||
            rows.ValueKind != JsonValueKind.Array)
        {
            return result;
        }
        foreach (var row in rows.EnumerateArray())
        {
            var contextId = ReadString(row, "location_context_id");
            var weather = CanonicalWeather(
                ReadBool(row, "is_raining") == true,
                ReadBool(row, "is_lightning") == true,
                ReadBool(row, "is_green_rain") == true);
            if (!string.IsNullOrWhiteSpace(contextId))
                result[contextId] = weather;
        }
        return result;
    }

    private static string ReadCurrentWeather(JsonElement state)
    {
        var greenRain = TryFieldValue(
                state,
                "time",
                "is_green_rain",
                out var greenRainValue) &&
            greenRainValue.ValueKind is JsonValueKind.True or JsonValueKind.False &&
            greenRainValue.GetBoolean();
        var raw = TryFieldValue(state, "time", "weather", out var weather) &&
            weather.ValueKind == JsonValueKind.String
                ? weather.GetString() ?? string.Empty
                : string.Empty;
        return CanonicalWeather(
            raw is "rain" or "lightning" || greenRain,
            raw == "lightning",
            greenRain);
    }

    private static string CanonicalWeather(
        bool raining,
        bool lightning,
        bool greenRain) =>
        greenRain ? "green_rain" :
        lightning ? "storm" :
        raining ? "rain" :
        "sun";

    private static string ReadLegacyFarmTypeKey(JsonElement state)
    {
        if (!TryFieldValue(state, "farm", "farm_type", out var value) ||
            value.ValueKind != JsonValueKind.Number ||
            !value.TryGetInt32(out var type))
        {
            return string.Empty;
        }
        return type switch
        {
            0 => "Standard",
            1 => "Riverland",
            2 => "Forest",
            3 => "Hilltop",
            4 => "Wilderness",
            5 => "FourCorners",
            6 => "Beach",
            _ => string.Empty
        };
    }

    private static JsonElement OptionalFieldValue(
        JsonElement state,
        string section,
        string field,
        string reason,
        ICollection<string> reasons)
    {
        if (TryFieldValue(state, section, field, out var value))
            return value.Clone();
        reasons.Add(reason);
        return default;
    }

    internal static bool TryFieldValue(
        JsonElement state,
        string section,
        string field,
        out JsonElement value)
    {
        value = default;
        return state.TryGetProperty(section, out var sectionValue) &&
            sectionValue.ValueKind == JsonValueKind.Object &&
            sectionValue.TryGetProperty(field, out var wrapper) &&
            wrapper.ValueKind == JsonValueKind.Object &&
            ReadString(wrapper, "status") == "available" &&
            wrapper.TryGetProperty("value", out value);
    }

    private static JsonElement RequiredObject(JsonElement value, string name) =>
        value.TryGetProperty(name, out var result) &&
        result.ValueKind == JsonValueKind.Object
            ? result
            : throw new InvalidDataException($"Snapshot property {name} is missing.");

    private static string RequiredFieldString(
        JsonElement state,
        string section,
        string field) =>
        TryFieldValue(state, section, field, out var value) &&
        value.ValueKind == JsonValueKind.String &&
        !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()!
            : throw new InvalidDataException(
                $"Snapshot field {section}.{field} is missing.");

    private static int RequiredFieldInt(
        JsonElement state,
        string section,
        string field) =>
        TryFieldValue(state, section, field, out var value) &&
        value.ValueKind == JsonValueKind.Number &&
        value.TryGetInt32(out var result)
            ? result
            : throw new InvalidDataException(
                $"Snapshot field {section}.{field} is missing.");

    internal static string ReadString(JsonElement value, string property) =>
        value.ValueKind == JsonValueKind.Object &&
        value.TryGetProperty(property, out var result) &&
        result.ValueKind == JsonValueKind.String
            ? result.GetString() ?? string.Empty
            : string.Empty;

    internal static int? ReadInt(JsonElement value, string property) =>
        value.ValueKind == JsonValueKind.Object &&
        value.TryGetProperty(property, out var result) &&
        result.ValueKind == JsonValueKind.Number &&
        result.TryGetInt32(out var parsed)
            ? parsed
            : null;

    internal static bool? ReadBool(JsonElement value, string property) =>
        value.ValueKind == JsonValueKind.Object &&
        value.TryGetProperty(property, out var result) &&
        result.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? result.GetBoolean()
            : null;

    private static bool? ReadNullableBool(JsonElement value, string property) =>
        ReadBool(value, property);
}

internal sealed record AcquisitionRouteLocationState(
    string LocationId,
    string LocationContextId,
    bool? SeedsIgnoreSeasonsHere);
