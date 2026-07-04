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
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        public static string ComputeStateHash(Dictionary<string, JsonElement> state)
        {
            var canonical = Canonicalize(JsonSerializer.SerializeToElement(state, JsonOptions));
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

        public static string Canonicalize(JsonElement element)
        {
            var builder = new StringBuilder();
            WriteCanonical(element, builder);
            return builder.ToString();
        }

        private static void WriteCanonical(JsonElement element, StringBuilder builder)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    builder.Append('{');
                    var first = true;
                    foreach (var property in element.EnumerateObject().OrderBy(item => item.Name, StringComparer.Ordinal))
                    {
                        if (!first)
                        {
                            builder.Append(',');
                        }

                        first = false;
                        builder.Append(JsonSerializer.Serialize(property.Name));
                        builder.Append(':');
                        WriteCanonical(property.Value, builder);
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

                        WriteCanonical(element[i], builder);
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
