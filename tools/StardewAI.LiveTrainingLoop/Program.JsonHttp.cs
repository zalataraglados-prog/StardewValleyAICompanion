using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.Training;
using StardewAI.Core.Training;
using StardewAI.LiveTrainingLoop;

static partial class Program
{
    private static string SnapshotUrlForProfile(
        string value,
        string profile,
        bool forceRefresh,
        string? expectedStateHash = null,
        long? expectedGameTick = null)
    {
        var uri = new Uri(value, UriKind.Absolute);
        var query = uri.Query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Where(pair =>
            {
                var name = Uri.UnescapeDataString(pair.Split('=', 2)[0]);
                return !string.Equals(name, "profile", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(name, "fresh", StringComparison.OrdinalIgnoreCase);
            })
            .Append("profile=" + Uri.EscapeDataString(profile))
            .ToList();
        if (forceRefresh)
        {
            query.Add("fresh=1");
        }
        else if (!string.IsNullOrWhiteSpace(expectedStateHash) &&
                 expectedGameTick > 0)
        {
            query.Add("expected_state_hash=" + Uri.EscapeDataString(expectedStateHash));
            query.Add("expected_game_tick=" + expectedGameTick.Value.ToString(
                System.Globalization.CultureInfo.InvariantCulture));
        }

        var builder = new UriBuilder(uri)
        {
            Query = string.Join("&", query)
        };
        return builder.Uri.AbsoluteUri;
    }

    private static string SnapshotIngestUrl(LiveTrainingOptions options)
    {
        var ingestUrl = options.BackendUrl + "/api/v1/snapshots";
        if (!Uri.TryCreate(
                options.BridgeSnapshotUrl,
                UriKind.Absolute,
                out var bridgeUri))
        {
            return ingestUrl;
        }

        foreach (var pair in bridgeUri.Query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            if (!string.Equals(
                    Uri.UnescapeDataString(parts[0]),
                    "profile",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var profile = parts.Length == 2
                ? Uri.UnescapeDataString(parts[1])
                : string.Empty;
            return string.IsNullOrWhiteSpace(profile)
                ? ingestUrl
                : ingestUrl + "?profile=" +
                    Uri.EscapeDataString(profile);
        }

        return ingestUrl;
    }

    private static async Task<(JsonObject? TrainingReport, JsonObject? Prediction)> TrainIfNeededAsync(HttpClient http, LiveTrainingOptions options, int iteration)
    {
        if (options.SkipTraining)
        {
            return (null, null);
        }

        if (iteration % options.TrainEvery != 0 && iteration != options.MaxAttempts)
        {
            return (null, null);
        }

        if (options.RequireStructuredPolicy)
        {
            var outputRoot = Path.Combine(options.Root, "datasets", "formal-policy");
            var horizonPath = File.Exists(options.PolicyHorizonObservationPath)
                ? options.PolicyHorizonObservationPath
                : null;
            var dataset = new PolicyTrajectoryDatasetBuilder().Build(
                options.PolicyTrajectoryDatasetPath,
                outputRoot,
                horizonPath,
                options.KnowledgeDictionaryVersion);
            var training = new StructuredPolicyTrainer().Train(
                dataset.ManifestPath,
                options.PolicyCheckpointPath);
            new FormalTrainingManifestStore().UpdateArtifacts(
                options.ManifestPath,
                options.RunId,
                dataset.ManifestPath,
                training.CheckpointPath,
                training.CheckpointSha256);
            var structuredReport = JsonSerializer.SerializeToNode(new
            {
                schema_version = "structured_policy_live_training.v1",
                checkpoint_id = training.Checkpoint.CheckpointId,
                checkpoint_path = training.CheckpointPath,
                checkpoint_sha256 = training.CheckpointSha256,
                dataset_manifest_path = dataset.ManifestPath,
                accepted_rows = dataset.Manifest.Counts.AcceptedRows,
                rejected_rows = dataset.Manifest.Counts.RejectedRows,
                train_rows = dataset.Manifest.Partitions.Single(value => value.Partition == PolicyDatasetPartitions.Train).Rows,
                validation_rows = dataset.Manifest.Partitions.Single(value => value.Partition == PolicyDatasetPartitions.Validation).Rows,
                test_rows = dataset.Manifest.Partitions.Single(value => value.Partition == PolicyDatasetPartitions.Test).Rows,
                train_pairs = training.Checkpoint.Training.TrainPairs
            }, JsonOptions)?.AsObject() ?? new JsonObject();
            AppendProgress(
                options,
                "train_structured",
                iteration,
                string.Empty,
                string.Empty,
                "checkpoint=" + training.Checkpoint.CheckpointId +
                " rows=" + dataset.Manifest.Counts.AcceptedRows +
                " pairs=" + training.Checkpoint.Training.TrainPairs +
                " source=" + options.ExecutorFeedbackSource);
            return (structuredReport, null);
        }

        var trainRequest = JsonSerializer.Serialize(new
        {
            dataset_path = Path.GetFullPath(options.DatasetPath)
        }, JsonOptions);
        var report = await PostJsonStringAsync(http, options.BackendUrl + "/api/v1/training/baseline/train", trainRequest);
        var prediction = await PostJsonStringAsync(http, options.BackendUrl + "/api/v1/planner/baseline/rank-options", trainRequest);
        var bestOption = prediction["ranked_options"]?[0]?["option_id"]?.GetValue<string>() ?? string.Empty;
        AppendProgress(options, "train", iteration, string.Empty, string.Empty, "best_option=" + bestOption + " source=" + options.ExecutorFeedbackSource);
        return (report, prediction);
    }

    private static async Task<JsonObject> PostJsonStringAsync(HttpClient http, string url, string json)
    {
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await http.PostAsync(url, content);
        var body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(url + " failed with " + (int)response.StatusCode + ": " + body);
        }

        return JsonNode.Parse(body)?.AsObject() ?? new JsonObject();
    }

    private static string ReadString(JsonObject? value, string property)
    {
        return value is not null &&
            value.TryGetPropertyValue(property, out var node) &&
            node is JsonValue jsonValue &&
            jsonValue.TryGetValue<string>(out var result)
                ? result
                : string.Empty;
    }

    private static string ReadStringOrEmpty(JsonObject? value, string property)
    {
        return ReadString(value, property);
    }

    private static int? ReadQueueParameterInt(JsonObject? item, string name)
    {
        var parameters = item?["normalized_command"]?["parameters"]?.AsArray();
        if (parameters is null)
        {
            return null;
        }

        foreach (var parameter in parameters)
        {
            var parameterObject = parameter?.AsObject();
            if (string.Equals(ReadStringOrEmpty(parameterObject, "name"), name, StringComparison.Ordinal) &&
                int.TryParse(ReadStringOrEmpty(parameterObject, "value"), out var result))
            {
                return result;
            }
        }

        return null;
    }

    private static long? ReadQueueParameterLong(JsonObject? item, string name)
    {
        var value = ReadQueueParameterString(item, name);
        return long.TryParse(value, out var result) ? result : null;
    }

    private static bool? ReadQueueParameterBool(JsonObject? item, string name)
    {
        var value = ReadQueueParameterString(item, name);
        return bool.TryParse(value, out var result) ? result : null;
    }

    private static string ReadQueueParameterString(JsonObject? item, string name)
    {
        var parameters = item?["normalized_command"]?["parameters"]?.AsArray();
        if (parameters is null)
        {
            return string.Empty;
        }

        foreach (var parameter in parameters)
        {
            var parameterObject = parameter?.AsObject();
            if (string.Equals(ReadStringOrEmpty(parameterObject, "name"), name, StringComparison.Ordinal))
            {
                return ReadStringOrEmpty(parameterObject, "value");
            }
        }

        return string.Empty;
    }

    private static double? ReadQueueParameterDouble(
        JsonObject? item,
        string name)
    {
        return double.TryParse(
            ReadQueueParameterString(item, name),
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out var value)
                ? value
                : null;
    }

    private static bool? ReadNullableBoolQueueParameter(JsonObject? item, string name)
    {
        return bool.TryParse(ReadQueueParameterString(item, name), out var value) ? value : null;
    }

    private static int ReadInt(JsonObject value, string property)
    {
        return value[property]?.GetValue<int>() ?? 0;
    }

    private static long ReadLong(JsonObject value, string property)
    {
        return value[property]?.GetValue<long>() ?? 0;
    }

    private static double ReadDouble(JsonObject value, string property)
    {
        return value[property]?.GetValue<double>() ?? 0;
    }

    private static string ReadChangedFactString(JsonObject execution, string path)
    {
        var facts = execution["changed_facts"]?.AsArray();
        if (facts is null)
        {
            return string.Empty;
        }

        foreach (var fact in facts)
        {
            var factObject = fact?.AsObject();
            if (string.Equals(ReadStringOrEmpty(factObject, "path"), path, StringComparison.Ordinal))
            {
                return ReadStringOrEmpty(factObject, "after");
            }
        }

        return string.Empty;
    }

    private static double ReadChangedFactDouble(JsonObject execution, string path)
    {
        return double.TryParse(ReadChangedFactString(execution, path), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var value)
            ? value
            : 0d;
    }

    private static bool ReadChangedFactBool(JsonObject execution, string path)
    {
        return bool.TryParse(ReadChangedFactString(execution, path), out var value) && value;
    }

    private static double ReadFieldDouble(JsonObject snapshot, string section, string name)
    {
        var value = snapshot["state"]?[section]?[name]?["value"];
        return TryReadJsonNumber(value, out var number) ? number : 0;
    }

    private static string ReadFieldString(JsonObject snapshot, string section, string name)
    {
        var value = snapshot["state"]?[section]?[name]?["value"];
        if (value is null || value.GetValueKind() != JsonValueKind.String)
        {
            return "unknown";
        }

        return value.GetValue<string>() ?? "unknown";
    }

    private static double ReadNestedFieldDouble(JsonObject snapshot, string section, string field, string property)
    {
        var value = snapshot["state"]?[section]?[field]?["value"]?[property];
        return TryReadJsonNumber(value, out var number) ? number : 0;
    }

    private static string ReadNestedFieldString(JsonObject snapshot, string section, string field, string property)
    {
        var value = snapshot["state"]?[section]?[field]?["value"]?[property];
        return value is not null && value.GetValueKind() == JsonValueKind.String
            ? value.GetValue<string>() ?? "unknown"
            : "unknown";
    }

    private static int CountFieldArray(JsonObject snapshot, string section, string field)
    {
        return snapshot["state"]?[section]?[field]?["value"]?.AsArray().Count ?? 0;
    }

    private static int CountNestedArray(JsonObject snapshot, string section, string field, string property)
    {
        return snapshot["state"]?[section]?[field]?["value"]?[property]?.AsArray().Count ?? 0;
    }

    private static double ReadFirstNestedArrayDouble(JsonObject snapshot, string section, string field, string arrayProperty, string valueProperty)
    {
        var value = snapshot["state"]?[section]?[field]?["value"]?[arrayProperty]?.AsArray().FirstOrDefault()?[valueProperty];
        return TryReadJsonNumber(value, out var number) ? number : 0;
    }

    private static bool TryReadJsonNumber(JsonNode? value, out double number)
    {
        number = 0;
        if (value is not JsonValue jsonValue || value.GetValueKind() != JsonValueKind.Number)
            return false;
        if (jsonValue.TryGetValue<double>(out number))
            return true;
        if (jsonValue.TryGetValue<int>(out var intValue))
        {
            number = intValue;
            return true;
        }
        if (jsonValue.TryGetValue<long>(out var longValue))
        {
            number = longValue;
            return true;
        }
        if (jsonValue.TryGetValue<decimal>(out var decimalValue))
        {
            number = (double)decimalValue;
            return true;
        }
        return double.TryParse(
            value.ToJsonString(),
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out number);
    }

    private static int CountCropsNeedingWater(JsonObject snapshot)
    {
        var crops = snapshot["state"]?["farm"]?["crops"]?["value"]?.AsArray();
        if (crops is null)
        {
            return 0;
        }

        return crops.Count(item => item?["needs_watering"]?.GetValue<bool>() == true);
    }

    private static int ReadUnavailableCount(JsonObject snapshot)
    {
        return snapshot["unavailable_fields"]?.AsArray().Count ?? 0;
    }

    private static int AvailableMinutes(JsonObject snapshot)
    {
        var time = (int)ReadFieldDouble(snapshot, "time", "time");
        if (time <= 0)
        {
            return 0;
        }

        var hour = time / 100;
        var minute = time % 100;
        var current = hour * 60 + minute;
        return Math.Max(0, 26 * 60 - current);
    }

    private static string[] ReadArrayStrings(JsonObject value, string property)
    {
        var array = value[property]?.AsArray();
        if (array is null)
        {
            return Array.Empty<string>();
        }

        return array
            .Select(item => item?.GetValue<string>() ?? string.Empty)
            .Where(item => item.Length > 0)
            .ToArray();
    }

    private static int[] ReadArrayInts(JsonObject value, string property)
    {
        var array = value[property]?.AsArray();
        return array is null
            ? Array.Empty<int>()
            : array.Select(item => item?.GetValue<int>() ?? 0).ToArray();
    }

    private static NumericFeature Number(string name, double value)
    {
        return new NumericFeature { Name = name, Value = value };
    }

    private static CategoricalFeature Category(string name, string value)
    {
        return new CategoricalFeature { Name = name, Value = string.IsNullOrWhiteSpace(value) ? "unknown" : value };
    }

    private static BooleanFeature Flag(string name, bool value)
    {
        return new BooleanFeature { Name = name, Value = value };
    }
}
