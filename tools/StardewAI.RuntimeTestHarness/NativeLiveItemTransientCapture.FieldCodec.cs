using System.Reflection;
using StardewAI.Contracts.Training;

namespace StardewAI.RuntimeTestHarness;

internal static class NativeLiveTransientFieldCodec
{
    internal static FieldInfo RequireField(Type declaringType, string name, Type fieldType)
    {
        var field = declaringType.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
        if (field is null || field.DeclaringType != declaringType || field.IsStatic || field.FieldType != fieldType)
            throw new InvalidDataException("Native transient field layout is unavailable or unsupported: " + declaringType.FullName + "." + name);
        return field;
    }

    internal static T? ReadNullable<T>(FieldInfo field, object instance) where T : class
    {
        var value = field.GetValue(instance);
        if (value is null) return null;
        if (value is T typed) return typed;
        throw new InvalidDataException("Native transient reference field is unreadable: " + field.Name);
    }

    internal static T ReadValue<T>(FieldInfo field, object instance) where T : struct
    {
        if (field.GetValue(instance) is T typed) return typed;
        throw new InvalidDataException("Native transient value field is unreadable: " + field.Name);
    }

    internal class CharacterBudget
    {
        private readonly int maximum;
        internal int Characters { get; private set; }
        internal CharacterBudget(int maximum) => this.maximum = maximum;
        internal string? String(string? value, int fieldMaximum = 4096)
        {
            int length = value?.Length ?? 0;
            if (length > fieldMaximum || length > maximum - Characters || !NativeObservationText.IsWellFormedUtf16(value))
                throw new InvalidDataException("Native transient text exceeds its budget or cannot round-trip through JSON without replacement.");
            Characters += length;
            return value;
        }
    }
}
