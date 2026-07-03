namespace StardewAI.TransparentBridge.Adapters;

public interface IStateAdapter
{
    string Domain { get; }
    int Priority { get; }
    StateAdapterResult Collect(long tick);
}
