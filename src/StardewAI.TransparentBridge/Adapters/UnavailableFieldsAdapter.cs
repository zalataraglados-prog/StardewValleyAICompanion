namespace StardewAI.TransparentBridge.Adapters;

public sealed class UnavailableFieldsAdapter : ReadAdapterBase
{
    private readonly string host;
    private readonly int webSocketPort;

    public UnavailableFieldsAdapter(string host, int webSocketPort)
    {
        this.host = host;
        this.webSocketPort = webSocketPort;
    }

    public override string Domain => "unavailable_fields";
    public override int Priority => 1000;

    public override StateAdapterResult Collect(long tick)
    {
        var fields = new Dictionary<string, object>
        {
            ["event_stream_websocket"] = Field(new
            {
                endpoint = $"ws://{host}:{webSocketPort}/api/v1/events/ws",
                schema_version = "event_stream.v1",
                mode = "read_only_push",
                accepts_commands = false,
                cursor_query = "after_sequence",
                fallback_endpoint = "/api/v1/events"
            }, "bridge websocket server /api/v1/events/ws", tick, "bridge_transport")
        };

        return Section("transport", fields, Array.Empty<string>(), "complete");
    }
}
