namespace StardewAI.Contracts.Training;

public static class NativeObservationText
{
    public static bool IsWellFormedUtf16(string? value)
    {
        if (value is null) return true;
        for (var index = 0; index < value.Length; index++)
        {
            if (char.IsHighSurrogate(value[index]))
            {
                if (++index >= value.Length || !char.IsLowSurrogate(value[index])) return false;
            }
            else if (char.IsLowSurrogate(value[index])) return false;
        }
        return true;
    }
}
