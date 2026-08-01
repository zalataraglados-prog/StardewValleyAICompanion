using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Training;
using StardewAI.Core.Training;

static partial class Program
{
    private static int AppendClosedHorizonObservations(
        LiveTrainingOptions options,
        JsonObject beforeSnapshot,
        JsonObject execution)
    {
        if (!string.Equals(ReadString(execution, "status"), "applied", StringComparison.Ordinal) ||
            !string.Equals(ReadString(execution, "primitive_verification_status"), "verified", StringComparison.Ordinal) ||
            execution["after_snapshot_fresh"]?.GetValue<bool>() != true ||
            execution["state_hash_changed"]?.GetValue<bool>() != true)
            return 0;
        var afterSnapshotPath = ReadString(execution, "after_snapshot_path");
        if (string.IsNullOrWhiteSpace(afterSnapshotPath) || !File.Exists(afterSnapshotPath))
            return 0;
        var afterSnapshot = JsonNode.Parse(File.ReadAllText(afterSnapshotPath))?.AsObject();
        if (afterSnapshot is null)
            return 0;

        var before = ReadHorizonDate(beforeSnapshot);
        var after = ReadHorizonDate(afterSnapshot);
        if (before is null || after is null ||
            !string.Equals(before.Value.SaveId, after.Value.SaveId, StringComparison.Ordinal) ||
            after.Value.Ordinal <= before.Value.Ordinal)
            return 0;
        if (string.IsNullOrWhiteSpace(ReadString(execution, "after_state_hash")))
            return 0;

        var writer = new JsonlPolicyHorizonObservationWriter();
        var appended = 0;
        appended += AppendObservation(
            writer,
            options.PolicyHorizonObservationPath,
            Observation(before.Value, PolicyHorizonKinds.Day, execution, afterSnapshotPath, null));
        if (before.Value.Year != after.Value.Year || !string.Equals(before.Value.Season, after.Value.Season, StringComparison.Ordinal))
        {
            appended += AppendObservation(
                writer,
                options.PolicyHorizonObservationPath,
                Observation(before.Value, PolicyHorizonKinds.Season, execution, afterSnapshotPath, null));
        }
        if (before.Value.Year != after.Value.Year)
        {
            appended += AppendObservation(
                writer,
                options.PolicyHorizonObservationPath,
                Observation(before.Value, PolicyHorizonKinds.Year, execution, afterSnapshotPath, null));
        }

        if (before.Value.Year < 3 && after.Value.Year >= 3 &&
            TryReadFieldInt(afterSnapshot, "farm", "grandpa_score", out var grandpaScore) &&
            grandpaScore is >= 0 and <= 21)
        {
            appended += AppendObservation(
                writer,
                options.PolicyHorizonObservationPath,
                Observation(
                    after.Value,
                    PolicyHorizonKinds.Grandpa21,
                    execution,
                    afterSnapshotPath,
                    grandpaScore));
        }

        return appended;
    }

    private static int AppendObservation(
        JsonlPolicyHorizonObservationWriter writer,
        string path,
        PolicyHorizonObservationEnvelope observation) => writer.AppendIfNew(path, observation) ? 1 : 0;

    private static PolicyHorizonObservationEnvelope Observation(
        HorizonDate date,
        string horizon,
        JsonObject execution,
        string evidencePath,
        int? grandpaScore)
    {
        var sourceStateHash = ReadString(execution, "after_state_hash");
        return new PolicyHorizonObservationEnvelope
        {
            ObservationId = "horizon:" + date.SaveId + ":" + date.Year + ":" + date.Season + ":" + date.Day + ":" + horizon + ":" + sourceStateHash,
            SaveId = date.SaveId,
            Year = date.Year,
            Season = date.Season,
            Day = date.Day,
            Time = date.Time,
            Horizon = horizon,
            Closed = true,
            GrandpaScore = grandpaScore,
            SourceStateHash = sourceStateHash,
            EvidenceKind = horizon == PolicyHorizonKinds.Grandpa21
                ? "transparent_after_snapshot_year3_evaluation_boundary"
                : "transparent_native_date_transition",
            EvidencePath = evidencePath
        };
    }

    private static HorizonDate? ReadHorizonDate(JsonObject snapshot)
    {
        if (!TryReadAvailableEnvelopeString(snapshot, "save_id", out var saveId) ||
            !TryReadFieldInt(snapshot, "time", "year", out var year) ||
            !TryReadAvailableFieldString(snapshot, "time", "season", out var season) ||
            !TryReadFieldInt(snapshot, "time", "day", out var day) ||
            !TryReadFieldInt(snapshot, "time", "time", out var time))
            return null;
        var seasonOrdinal = season switch
        {
            "spring" => 0,
            "summer" => 1,
            "fall" => 2,
            "winter" => 3,
            _ => -1
        };
        var hour = time / 100;
        var minute = time % 100;
        if (string.IsNullOrWhiteSpace(saveId) ||
            year < 1 ||
            seasonOrdinal < 0 ||
            day is < 1 or > 28 ||
            hour is < 6 or > 26 ||
            minute is < 0 or >= 60)
            return null;
        return new HorizonDate(
            saveId,
            year,
            season,
            day,
            time,
            ((long)year * 4L + seasonOrdinal) * 28L + day);
    }

    private static bool TryReadFieldInt(JsonObject snapshot, string section, string name, out int value)
    {
        value = 0;
        var field = snapshot["state"]?[section]?[name];
        var node = field?["value"];
        var status = field?["status"]?.GetValue<string>();
        if (status is not ("available" or "derived") ||
            node is not JsonValue jsonValue ||
            node.GetValueKind() != JsonValueKind.Number ||
            !jsonValue.TryGetValue<int>(out value))
            return false;
        return true;
    }

    private static bool TryReadAvailableEnvelopeString(
        JsonObject snapshot,
        string name,
        out string value)
    {
        value = string.Empty;
        var field = snapshot[name];
        var status = field?["status"]?.GetValue<string>();
        var node = field?["value"];
        if (status is not ("available" or "derived") ||
            node is not JsonValue jsonValue ||
            node.GetValueKind() != JsonValueKind.String ||
            !jsonValue.TryGetValue<string>(out var parsed) ||
            string.IsNullOrWhiteSpace(parsed))
            return false;
        value = parsed;
        return true;
    }

    private static bool TryReadAvailableFieldString(
        JsonObject snapshot,
        string section,
        string name,
        out string value)
    {
        value = string.Empty;
        var field = snapshot["state"]?[section]?[name];
        var status = field?["status"]?.GetValue<string>();
        var node = field?["value"];
        if (status is not ("available" or "derived") ||
            node is not JsonValue jsonValue ||
            node.GetValueKind() != JsonValueKind.String ||
            !jsonValue.TryGetValue<string>(out var parsed) ||
            string.IsNullOrWhiteSpace(parsed))
            return false;
        value = parsed;
        return true;
    }

    private readonly record struct HorizonDate(
        string SaveId,
        int Year,
        string Season,
        int Day,
        int Time,
        long Ordinal);
}
