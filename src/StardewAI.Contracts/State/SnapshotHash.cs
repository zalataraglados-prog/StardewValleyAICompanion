using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace StardewAI.Contracts.State
{
    public static class SnapshotHash
    {
        private static readonly HashSet<string> NonSemanticPropertyNames =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "machine_probe_cache_tick"
            };

        public static string ComputeStateHash(Dictionary<string, JsonElement> state)
        {
            return HashCanonical(CanonicalizeState(state, excludeNonSemanticProperties: true));
        }

        public static string ComputeLegacyStateHash(Dictionary<string, JsonElement> state)
        {
            return HashCanonical(CanonicalizeState(state, excludeNonSemanticProperties: false));
        }

        public static bool MatchesStateHash(
            Dictionary<string, JsonElement> state,
            string stateHash)
        {
            if (string.IsNullOrWhiteSpace(stateHash))
            {
                return false;
            }

            return string.Equals(
                       stateHash,
                       ComputeStateHash(state),
                       StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(
                       stateHash,
                       ComputeLegacyStateHash(state),
                       StringComparison.OrdinalIgnoreCase);
        }

        private static string HashCanonical(string canonical)
        {
            byte[] bytes;
            using (var sha256 = SHA256.Create())
            {
                bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(canonical));
            }
            var hashBuilder = new StringBuilder(bytes.Length * 2);
            foreach (var value in bytes)
            {
                hashBuilder.Append(value.ToString("x2"));
            }

            return hashBuilder.ToString();
        }

        private static string CanonicalizeState(
            IReadOnlyDictionary<string, JsonElement> state,
            bool excludeNonSemanticProperties)
        {
            var builder = new StringBuilder();
            builder.Append('{');
            var first = true;
            foreach (var item in state.OrderBy(item => item.Key, StringComparer.Ordinal))
            {
                if (!first)
                {
                    builder.Append(',');
                }

                first = false;
                builder.Append(JsonSerializer.Serialize(item.Key));
                builder.Append(':');
                WriteCanonical(item.Value, builder, excludeNonSemanticProperties);
            }

            builder.Append('}');
            return builder.ToString();
        }

        public static string Canonicalize(JsonElement element)
        {
            var builder = new StringBuilder();
            WriteCanonical(element, builder, excludeNonSemanticProperties: false);
            return builder.ToString();
        }

        private static void WriteCanonical(
            JsonElement element,
            StringBuilder builder,
            bool excludeNonSemanticProperties)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    builder.Append('{');
                    var first = true;
                    foreach (var property in element.EnumerateObject().OrderBy(item => item.Name, StringComparer.Ordinal))
                    {
                        if (excludeNonSemanticProperties &&
                            NonSemanticPropertyNames.Contains(property.Name))
                        {
                            continue;
                        }

                        if (!first)
                        {
                            builder.Append(',');
                        }

                        first = false;
                        builder.Append(JsonSerializer.Serialize(property.Name));
                        builder.Append(':');
                        WriteCanonical(
                            property.Value,
                            builder,
                            excludeNonSemanticProperties);
                    }

                    builder.Append('}');
                    break;
                case JsonValueKind.Array:
                    builder.Append('[');
                    for (var i = 0; i < element.GetArrayLength(); i++)
                    {
                        if (i > 0)
                        {
                            builder.Append(',');
                        }

                        WriteCanonical(
                            element[i],
                            builder,
                            excludeNonSemanticProperties);
                    }

                    builder.Append(']');
                    break;
                default:
                    builder.Append(element.GetRawText());
                    break;
            }
        }
    }
}
