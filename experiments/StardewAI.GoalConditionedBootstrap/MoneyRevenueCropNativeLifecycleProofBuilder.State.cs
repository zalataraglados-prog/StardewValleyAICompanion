using System.Globalization;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Core.Infrastructure;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class MoneyRevenueCropNativeLifecycleProofBuilder
{
    private static void ValidateSnapshotIdentity(
        SnapshotEnvelope snapshot,
        SnapshotEnvelope source,
        CalendarIdentity sourceCalendar)
    {
        var calendar = ReadCalendar(snapshot);
        Require(snapshot.SchemaVersion == "snapshot.v1" &&
                snapshot.StateHash == SnapshotHash.ComputeStateHash(
                    snapshot.State) &&
                snapshot.GameVersion == source.GameVersion &&
                snapshot.SaveId.Status == FieldStatus.Available &&
                snapshot.PlayerId.Status == FieldStatus.Available &&
                snapshot.SaveId.Value == source.SaveId.Value &&
                snapshot.PlayerId.Value == source.PlayerId.Value &&
                calendar.WorldDateOffset == sourceCalendar.WorldDateOffset,
            "Money Revenue crop lifecycle snapshot identity drifted.");
    }

    private static CropCatalogRow ReadCropCatalogRow(
        SnapshotEnvelope snapshot,
        string seedQualifiedItemId)
    {
        Require(TryStateFieldValue(
                    snapshot,
                    "farm",
                    "crop_catalog",
                    out var catalog) &&
                catalog.ValueKind == JsonValueKind.Array,
            "Money Revenue crop catalog is unavailable.");
        var rows = catalog.EnumerateArray().Where(row =>
                row.ValueKind == JsonValueKind.Object &&
                row.TryGetProperty("seed_id", out var seed) &&
                seed.ValueKind == JsonValueKind.String &&
                NormalizeObjectId(seed.GetString() ?? string.Empty) ==
                    seedQualifiedItemId)
            .ToArray();
        var needsWatering = false;
        Require(rows.Length == 1 &&
                TryReadBool(rows[0], "needs_watering", out needsWatering),
            "Money Revenue crop catalog row is missing exact watering behavior.");
        int? regrow = null;
        if (rows[0].TryGetProperty("regrow_days", out var regrowValue) &&
            regrowValue.ValueKind != JsonValueKind.Null)
        {
            var parsed = 0;
            Require(regrowValue.ValueKind == JsonValueKind.Number &&
                    regrowValue.TryGetInt32(out parsed),
                "Money Revenue crop catalog regrow duration is invalid.");
            regrow = parsed > 0 ? parsed : null;
        }
        return new CropCatalogRow(needsWatering, regrow);
    }

    private static CropState? ReadCrop(
        SnapshotEnvelope snapshot,
        string locationId,
        int x,
        int y)
    {
        Require(string.Equals(locationId, "Farm", StringComparison.Ordinal),
            "Money Revenue native-lifecycle v1 supports the authoritative Farm crop projection only.");
        Require(TryStateFieldValue(snapshot, "farm", "crops", out var crops) &&
                crops.ValueKind == JsonValueKind.Array,
            "Money Revenue farm crop projection is unavailable.");
        var rows = crops.EnumerateArray().Where(row =>
                row.ValueKind == JsonValueKind.Object &&
                ReadInt(row, "tile_x") == x &&
                ReadInt(row, "tile_y") == y &&
                ReadString(row, "location_id") == locationId)
            .ToArray();
        Require(rows.Length <= 1,
            "Money Revenue farm crop projection contains duplicate tiles.");
        if (rows.Length == 0)
            return null;
        var row = rows[0];
        var phaseDays = Array.Empty<int>();
        var currentPhase = 0;
        var phaseCount = 0;
        var phaseDay = 0;
        var dead = false;
        var fullyGrown = false;
        var ready = false;
        var watered = false;
        int? daysUntil = null;
        int? regrowDays = null;
        Require(TryReadIntArray(row, "phase_days", out phaseDays) &&
                phaseDays.Length > 0 &&
                TryReadInt(row, "current_phase", out currentPhase) &&
                TryReadInt(row, "phase_count", out phaseCount) &&
                TryReadInt(row, "day_of_current_phase", out phaseDay) &&
                TryReadBool(row, "dead", out dead) &&
                TryReadBool(row, "fully_grown", out fullyGrown) &&
                TryReadBool(row, "ready_for_harvest", out ready) &&
                TryReadBool(row, "watered", out watered) &&
                TryReadNullableInt(
                    row,
                    "days_until_next_harvest_if_watered",
                    out daysUntil) &&
                TryReadNullableInt(row, "regrow_days", out regrowDays) &&
                currentPhase >= 0 && currentPhase < phaseDays.Length &&
                phaseCount == phaseDays.Length && phaseDay >= 0,
            "Money Revenue farm crop row is incomplete.");
        return new CropState(
            ReadString(row, "harvest_source_seed_id"),
            ReadString(row, "harvest_item_qualified_id"),
            phaseDays,
            currentPhase,
            phaseCount,
            phaseDay,
            dead,
            fullyGrown,
            ready,
            daysUntil,
            regrowDays,
            watered);
    }

    private static int SeedQuantity(
        SnapshotEnvelope snapshot,
        string seedQualifiedItemId)
    {
        var nodes = default(JsonElement);
        Require(TryStateFieldValue(
                    snapshot,
                    "player",
                    "seed_inventory",
                    out nodes) &&
                nodes.ValueKind == JsonValueKind.Array,
            "Money Revenue crop lifecycle seed inventory is unavailable.");
        var total = 0;
        foreach (var slot in nodes.EnumerateArray())
        {
            if (slot.ValueKind == JsonValueKind.Object &&
                NormalizeObjectId(ReadString(slot, "seed_id")) ==
                    seedQualifiedItemId &&
                TryReadInt(slot, "stack", out var stack) && stack >= 0)
            {
                total = checked(total + stack);
            }
        }
        return total;
    }

    private static (int X, int Y) ReadTarget(
        ActionQueueItem item,
        string expectedLocation,
        SnapshotEnvelope before)
    {
        var parameters = item.NormalizedCommand?.Parameters ??
            Array.Empty<SmallModelActionParameter>();
        var x = UniqueParameter(parameters, "target_tile_x");
        var y = UniqueParameter(parameters, "target_tile_y");
        var parsedX = 0;
        var parsedY = 0;
        Require(int.TryParse(x, NumberStyles.Integer, CultureInfo.InvariantCulture,
                    out parsedX) &&
                int.TryParse(y, NumberStyles.Integer, CultureInfo.InvariantCulture,
                    out parsedY),
            "Money Revenue crop action target tile is missing.");
        var location = UniqueParameter(parameters, "target_location");
        var currentLocationMatches = TryStateFieldValue(
                before,
                "player",
                "location_id",
                out var currentLocation) &&
            currentLocation.ValueKind == JsonValueKind.String &&
            currentLocation.GetString() == expectedLocation;
        Require(string.IsNullOrWhiteSpace(location)
                ? currentLocationMatches
                : string.Equals(
                    location,
                    expectedLocation,
                    StringComparison.Ordinal),
            "Money Revenue crop action target location drifted.");
        if (item.OptionId == PlantOption)
        {
            var seed = UniqueParameter(parameters, "seed_id");
            Require(!string.IsNullOrWhiteSpace(seed),
                "Money Revenue crop planting seed identity is missing.");
        }
        return (parsedX, parsedY);
    }

    private static string ReadPlayerLocation(SnapshotEnvelope snapshot)
    {
        Require(TryStateFieldValue(
                    snapshot,
                    "player",
                    "location_id",
                    out var location) &&
                location.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(location.GetString()),
            "Money Revenue crop lifecycle player location is unavailable.");
        return location.GetString() ?? string.Empty;
    }

    private static string UniqueParameter(
        SmallModelActionParameter[] parameters,
        string name)
    {
        var matches = parameters.Where(value => value.Name == name).ToArray();
        Require(matches.Length <= 1,
            "Money Revenue crop action parameter is duplicated: " + name);
        return matches.Length == 1 ? matches[0].Value : string.Empty;
    }

    private static CalendarIdentity ReadCalendar(SnapshotEnvelope snapshot)
    {
        var totalDay = 0;
        var year = 0;
        var season = string.Empty;
        var dayOfMonth = 0;
        Require(snapshot.State.TryGetValue("time", out var time) &&
                time.ValueKind == JsonValueKind.Object &&
                TryReadAvailableInt(time, "total_days", out totalDay) &&
                TryReadAvailableInt(time, "year", out year) &&
                TryReadAvailableString(time, "season", out season) &&
                TryReadAvailableInt(time, "day", out dayOfMonth) &&
                totalDay >= 0 && year >= 1 && dayOfMonth is >= 1 and <= 28,
            "Money Revenue crop lifecycle calendar is unavailable.");
        var seasonIndex = season switch
        {
            "spring" => 0,
            "summer" => 1,
            "fall" => 2,
            "winter" => 3,
            _ => -1
        };
        Require(seasonIndex >= 0,
            "Money Revenue crop lifecycle season is invalid.");
        var ordinal = checked(
            (year - 1) * 112 + seasonIndex * 28 + dayOfMonth - 1);
        return new CalendarIdentity(totalDay, checked(totalDay - ordinal));
    }

    private static bool TryStateFieldValue(
        SnapshotEnvelope snapshot,
        string section,
        string field,
        out JsonElement value)
    {
        value = default;
        return snapshot.State.TryGetValue(section, out var parent) &&
            parent.ValueKind == JsonValueKind.Object &&
            parent.TryGetProperty(field, out var envelope) &&
            envelope.ValueKind == JsonValueKind.Object &&
            ReadableStatus(envelope) &&
            envelope.TryGetProperty("value", out value);
    }

    private static bool TryReadAvailableInt(
        JsonElement parent,
        string name,
        out int value)
    {
        value = 0;
        return parent.TryGetProperty(name, out var field) &&
            field.ValueKind == JsonValueKind.Object &&
            ReadableStatus(field) &&
            field.TryGetProperty("value", out var property) &&
            property.ValueKind == JsonValueKind.Number &&
            property.TryGetInt32(out value);
    }

    private static bool TryReadAvailableString(
        JsonElement parent,
        string name,
        out string value)
    {
        value = string.Empty;
        if (!parent.TryGetProperty(name, out var field) ||
            field.ValueKind != JsonValueKind.Object ||
            !ReadableStatus(field) ||
            !field.TryGetProperty("value", out var property) ||
            property.ValueKind != JsonValueKind.String)
        {
            return false;
        }
        value = property.GetString() ?? string.Empty;
        return value.Length > 0;
    }

    private static bool ReadableStatus(JsonElement field) =>
        field.TryGetProperty("status", out var status) &&
        status.ValueKind == JsonValueKind.String &&
        status.GetString() is "available" or "derived";

    private static bool TryReadBool(
        JsonElement parent,
        string name,
        out bool value)
    {
        value = false;
        if (!parent.TryGetProperty(name, out var property) ||
            property.ValueKind is not (
                JsonValueKind.True or JsonValueKind.False))
        {
            return false;
        }
        value = property.GetBoolean();
        return true;
    }

    private static bool TryReadInt(
        JsonElement parent,
        string name,
        out int value)
    {
        value = 0;
        return parent.TryGetProperty(name, out var property) &&
            property.ValueKind == JsonValueKind.Number &&
            property.TryGetInt32(out value);
    }

    private static bool TryReadNullableInt(
        JsonElement parent,
        string name,
        out int? value)
    {
        value = null;
        if (!parent.TryGetProperty(name, out var property))
            return false;
        if (property.ValueKind == JsonValueKind.Null)
            return true;
        if (property.ValueKind == JsonValueKind.Number &&
            property.TryGetInt32(out var parsed))
        {
            value = parsed;
            return true;
        }
        return false;
    }

    private static bool TryReadIntArray(
        JsonElement parent,
        string name,
        out int[] value)
    {
        value = Array.Empty<int>();
        if (!parent.TryGetProperty(name, out var property) ||
            property.ValueKind != JsonValueKind.Array)
        {
            return false;
        }
        var result = new List<int>();
        foreach (var item in property.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Number ||
                !item.TryGetInt32(out var parsed))
            {
                return false;
            }
            result.Add(parsed);
        }
        value = result.ToArray();
        return true;
    }

    private static int ReadInt(JsonElement parent, string name) =>
        parent.TryGetProperty(name, out var property) &&
        property.TryGetInt32(out var value)
            ? value
            : int.MinValue;

    private static string ReadString(JsonElement parent, string name) =>
        parent.TryGetProperty(name, out var property) &&
        property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? string.Empty
            : string.Empty;

    private static string NormalizeObjectId(string value) =>
        value.StartsWith("(O)", StringComparison.Ordinal)
            ? value
            : "(O)" + value;

    private static string RequiredFile(string path, string label)
    {
        Require(!string.IsNullOrWhiteSpace(path), label + " path is required.");
        var fullPath = Path.GetFullPath(path);
        Require(File.Exists(fullPath), label + " is missing: " + fullPath);
        return fullPath;
    }

    private static bool SamePath(string left, string right) =>
        string.Equals(
            Path.GetFullPath(left),
            Path.GetFullPath(right),
            PathComparison);

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidDataException(message);
    }

    private static StringComparison PathComparison =>
        OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

    private static StringComparer PathComparer =>
        OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
}
