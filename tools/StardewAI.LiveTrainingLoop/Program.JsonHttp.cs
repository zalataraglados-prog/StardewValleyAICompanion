using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.Training;
using StardewAI.LiveTrainingLoop;

static partial class Program
{
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

        var trainRequest = JsonSerializer.Serialize(new
        {
            dataset_path = Path.GetFullPath(options.DatasetPath)
        }, JsonOptions);
        var report = await PostJsonStringAsync(http, options.BackendUrl + "/api/v1/training/baseline/train", trainRequest);
        var prediction = await PostJsonStringAsync(http, options.BackendUrl + "/api/v1/planner/baseline/rank-options", trainRequest);
        var bestOption = prediction["ranked_options"]?[0]?["option_id"]?.GetValue<string>() ?? string.Empty;
        AppendProgress(options, "train", iteration, string.Empty, string.Empty, "best_option=" + bestOption + " source=real_runtime_executor");
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

    private static string ReadString(JsonObject value, string property)
    {
        return value[property]?.GetValue<string>() ?? string.Empty;
    }

    private static string ReadStringOrEmpty(JsonObject? value, string property)
    {
        return value?[property]?.GetValue<string>() ?? string.Empty;
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
        if (value is null)
        {
            return 0;
        }

        return value.GetValueKind() == JsonValueKind.Number ? value.GetValue<double>() : 0;
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
        return value is not null && value.GetValueKind() == JsonValueKind.Number ? value.GetValue<double>() : 0;
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
        return value is not null && value.GetValueKind() == JsonValueKind.Number ? value.GetValue<double>() : 0;
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
