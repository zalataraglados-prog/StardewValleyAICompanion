using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;

namespace StardewAI.Testing
{
    public static class NativeRandomStateFingerprint
    {
        public static bool TryCapture(Random random, out string fingerprint)
        {
            fingerprint = string.Empty;
            if (random is null || random.GetType() != typeof(Random)) return false;
            try
            {
                using var stream = new MemoryStream();
                using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
                    Write(random, writer, new Dictionary<object, int>(ReferenceComparer.Instance), 0);
                using var hash = SHA256.Create();
                fingerprint = BitConverter.ToString(hash.ComputeHash(stream.ToArray())).Replace("-", "").ToLowerInvariant();
                return true;
            }
            catch (Exception error) when (error is InvalidOperationException || error is MemberAccessException ||
                error is NotSupportedException || error is ArgumentException)
            { return false; }
        }

        private static void Write(object? value, BinaryWriter writer, Dictionary<object, int> seen, int depth)
        {
            if (depth > 8 || seen.Count > 32) throw new InvalidOperationException("random_state_bounds");
            if (value is null) { writer.Write("null"); return; }
            var type = value.GetType();
            writer.Write(type.FullName!);
            if (type.IsPrimitive)
            {
                switch (value)
                {
                    case bool b: writer.Write(b); return;
                    case byte b: writer.Write(b); return;
                    case sbyte b: writer.Write(b); return;
                    case short n: writer.Write(n); return;
                    case ushort n: writer.Write(n); return;
                    case int n: writer.Write(n); return;
                    case uint n: writer.Write(n); return;
                    case long n: writer.Write(n); return;
                    case ulong n: writer.Write(n); return;
                    default: throw new NotSupportedException("random_state_primitive");
                }
            }
            if (seen.TryGetValue(value, out var reference)) { writer.Write(reference); return; }
            seen.Add(value, seen.Count);
            if (value is Array array)
            {
                if (array.Rank != 1 || array.Length > 256 || !type.GetElementType()!.IsPrimitive)
                    throw new NotSupportedException("random_state_array");
                writer.Write(array.Length);
                foreach (var item in array) Write(item, writer, seen, depth + 1);
                return;
            }
            if (type != typeof(Random) && (type.Assembly != typeof(Random).Assembly ||
                !type.FullName!.StartsWith("System.Random+", StringComparison.Ordinal)))
                throw new NotSupportedException("random_state_runtime_type");
            var allFields = new List<FieldInfo>();
            for (var current = type; current != null && current != typeof(object) && current != typeof(ValueType); current = current.BaseType)
                allFields.AddRange(current.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly));
            var fields = allFields.OrderBy(field => field.DeclaringType!.FullName, StringComparer.Ordinal)
                .ThenBy(field => field.Name, StringComparer.Ordinal).ToArray();
            if (fields.Length == 0 || fields.Length > 16) throw new NotSupportedException("random_state_layout");
            foreach (var field in fields) { writer.Write(field.DeclaringType!.FullName!); writer.Write(field.Name); Write(field.GetValue(value), writer, seen, depth + 1); }
        }

        private sealed class ReferenceComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceComparer Instance = new ReferenceComparer();
            public new bool Equals(object? left, object? right) => ReferenceEquals(left, right);
            public int GetHashCode(object value) => RuntimeHelpers.GetHashCode(value);
        }
    }
}
