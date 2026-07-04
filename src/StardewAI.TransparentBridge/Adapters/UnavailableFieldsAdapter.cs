namespace StardewAI.TransparentBridge.Adapters;

public sealed class UnavailableFieldsAdapter : ReadAdapterBase
{
    public override string Domain => "unavailable_fields";
    public override int Priority => 1000;

    public override StateAdapterResult Collect(long tick)
    {
        var fields = new Dictionary<string, object>
        {
            ["event_stream_websocket"] = Unavailable("event_stream_websocket_not_implemented", "bridge websocket server", tick)
        };

        return Section("transport", fields, new[]
        {
            "event_stream_websocket"
        }, "unavailable");
    }
}
