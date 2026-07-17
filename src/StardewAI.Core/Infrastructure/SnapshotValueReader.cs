using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;

namespace StardewAI.Core.Infrastructure
{
    internal static class SnapshotValueReader
    {
        public static JsonElement? ReadStateFieldValue(SnapshotEnvelope snapshot, string section, string field)
        {
            if (!snapshot.State.TryGetValue(section, out var sectionElement) ||
                sectionElement.ValueKind != JsonValueKind.Object ||
                !sectionElement.TryGetProperty(field, out var envelope) ||
                envelope.ValueKind != JsonValueKind.Object ||
                !envelope.TryGetProperty("value", out var value))
            {
                return null;
            }

            return value;
        }

        public static string ReadStateFieldStatus(SnapshotEnvelope snapshot, string section, string field)
        {
            if (!snapshot.State.TryGetValue(section, out var sectionElement) ||
                sectionElement.ValueKind != JsonValueKind.Object ||
                !sectionElement.TryGetProperty(field, out var envelope) ||
                envelope.ValueKind != JsonValueKind.Object ||
                !envelope.TryGetProperty("status", out var status) ||
                status.ValueKind != JsonValueKind.String)
            {
                return string.Empty;
            }

            return status.GetString() ?? string.Empty;
        }

        public static bool ReadableStatus(string? status)
        {
            return string.Equals(status, "available", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(status, "derived", StringComparison.OrdinalIgnoreCase);
        }

        public static string? ReadParameter(IEnumerable<SmallModelActionParameter> parameters, string name)
        {
            return parameters.FirstOrDefault(parameter =>
                string.Equals(parameter.Name, name, StringComparison.OrdinalIgnoreCase))?.Value;
        }

        public static int? ReadIntParameter(IEnumerable<SmallModelActionParameter> parameters, string name)
        {
            return int.TryParse(ReadParameter(parameters, name), out var value) ? value : null;
        }

        public static int? ReadIntParameter(SmallModelAction action, string name)
        {
            return ReadIntParameter(action.Parameters, name);
        }

        public static double? ReadDoubleParameter(IEnumerable<SmallModelActionParameter> parameters, string name)
        {
            return double.TryParse(ReadParameter(parameters, name), out var value) ? value : null;
        }

        public static double? ReadDoubleParameter(SmallModelAction action, string name)
        {
            return ReadDoubleParameter(action.Parameters, name);
        }

        public static string? ReadParameter(SmallModelAction action, string name)
        {
            return ReadParameter(action.Parameters, name);
        }

        public static int ReadStateFieldInt(SnapshotEnvelope snapshot, string section, string field, int fallback = 0)
        {
            var value = ReadStateFieldValue(snapshot, section, field);
            return value.HasValue && value.Value.ValueKind == JsonValueKind.Number && value.Value.TryGetInt32(out var result)
                ? result
                : fallback;
        }

        public static int? ReadStateFieldIntOptional(SnapshotEnvelope snapshot, string section, string field)
        {
            var value = ReadStateFieldValue(snapshot, section, field);
            return value.HasValue && value.Value.ValueKind == JsonValueKind.Number && value.Value.TryGetInt32(out var result)
                ? result
                : null;
        }

        public static double? ReadStateFieldDoubleOptional(SnapshotEnvelope snapshot, string section, string field)
        {
            var value = ReadStateFieldValue(snapshot, section, field);
            return value.HasValue && value.Value.ValueKind == JsonValueKind.Number && value.Value.TryGetDouble(out var result)
                ? result
                : null;
        }

        public static string ReadStateFieldString(SnapshotEnvelope snapshot, string section, string field, string fallback = "")
        {
            var value = ReadStateFieldValue(snapshot, section, field);
            return value.HasValue && value.Value.ValueKind == JsonValueKind.String
                ? value.Value.GetString() ?? fallback
                : fallback;
        }

        public static bool ReadStateFieldBool(SnapshotEnvelope snapshot, string section, string field, bool fallback = false)
        {
            var value = ReadStateFieldValue(snapshot, section, field);
            return value.HasValue
                ? value.Value.ValueKind switch
                {
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    _ => fallback
                }
                : fallback;
        }

        public static int ReadInt(JsonElement element, string property, int fallback = 0)
        {
            return element.ValueKind == JsonValueKind.Object &&
                element.TryGetProperty(property, out var value) &&
                value.ValueKind == JsonValueKind.Number &&
                value.TryGetInt32(out var parsed)
                    ? parsed
                    : fallback;
        }

        public static int? ReadIntOptional(JsonElement element, string property)
        {
            return element.ValueKind == JsonValueKind.Object &&
                element.TryGetProperty(property, out var value) &&
                value.ValueKind == JsonValueKind.Number &&
                value.TryGetInt32(out var parsed)
                    ? parsed
                    : null;
        }

        public static int? NullableReadInt(JsonElement element, string property)
        {
            return ReadIntOptional(element, property);
        }

        public static int? ReadNullableInt(JsonElement element, string property)
        {
            return ReadIntOptional(element, property);
        }

        public static double? NullableReadDouble(JsonElement element, string property)
        {
            return element.ValueKind == JsonValueKind.Object &&
                element.TryGetProperty(property, out var value) &&
                value.ValueKind == JsonValueKind.Number &&
                value.TryGetDouble(out var parsed)
                    ? parsed
                    : null;
        }

        public static string ReadString(JsonElement element, string property, string fallback = "")
        {
            return element.ValueKind == JsonValueKind.Object &&
                element.TryGetProperty(property, out var value) &&
                value.ValueKind == JsonValueKind.String
                    ? value.GetString() ?? fallback
                    : fallback;
        }

        public static double ReadDouble(JsonElement element, string property, double fallback = 0d)
        {
            return element.ValueKind == JsonValueKind.Object &&
                element.TryGetProperty(property, out var value) &&
                value.ValueKind == JsonValueKind.Number &&
                value.TryGetDouble(out var parsed)
                    ? parsed
                    : fallback;
        }

        public static bool ReadBool(JsonElement element, string property, bool fallback = false)
        {
            if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(property, out var value))
            {
                return fallback;
            }

            return value.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => fallback
            };
        }

        public static SmallModelActionParameter Parameter(string name, string? value)
        {
            return new SmallModelActionParameter { Name = name, Value = value ?? string.Empty };
        }
    }
}
