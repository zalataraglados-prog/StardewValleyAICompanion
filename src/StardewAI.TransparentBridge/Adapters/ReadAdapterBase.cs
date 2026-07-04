using StardewAI.Contracts.State;

namespace StardewAI.TransparentBridge.Adapters;

public abstract class ReadAdapterBase : IStateAdapter
{
    public abstract string Domain { get; }
    public abstract int Priority { get; }
    public abstract StateAdapterResult Collect(long tick);

    protected static FieldEnvelope<T> Field<T>(T value, string source, long readAtTick, string adapter = "vanilla_1_6")
    {
        return new FieldEnvelope<T>
        {
            Value = value,
            Status = value is null ? FieldStatus.Unavailable : FieldStatus.Available,
            Source = new SourceRef { Kind = value is null ? "unavailable" : "game_object", Path = source },
            Adapter = adapter,
            ReadAtTick = readAtTick,
            Confidence = value is null ? 0.0 : 1.0,
            Reason = value is null ? "value_unavailable" : null
        };
    }

    protected static FieldEnvelope<object?> Unavailable(string reason, string source, long readAtTick, string adapter = "not_implemented")
    {
        return new FieldEnvelope<object?>
        {
            Value = null,
            Status = FieldStatus.Unavailable,
            Source = new SourceRef { Kind = "unavailable", Path = source },
            Adapter = adapter,
            ReadAtTick = readAtTick,
            Confidence = 0.0,
            Reason = reason
        };
    }

    protected static StateAdapterResult Section(
        string sectionName,
        IReadOnlyDictionary<string, object> fields,
        IReadOnlyList<string>? unavailableFields = null,
        string completeness = "partial")
    {
        return new StateAdapterResult(sectionName, fields, unavailableFields ?? Array.Empty<string>(), completeness);
    }
}
