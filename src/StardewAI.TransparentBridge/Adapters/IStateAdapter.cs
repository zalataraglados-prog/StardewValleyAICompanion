namespace StardewAI.TransparentBridge.Adapters;

public sealed record StateAdapterResult(
    string SectionName,
    IReadOnlyDictionary<string, object> Fields,
    IReadOnlyList<string> UnavailableFields,
    string Completeness);

public interface IStateAdapter
{
    string Domain { get; }
    int Priority { get; }
    StateAdapterResult Collect(long tick);
}
