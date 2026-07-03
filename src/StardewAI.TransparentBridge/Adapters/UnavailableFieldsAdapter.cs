namespace StardewAI.TransparentBridge.Adapters;

public sealed class UnavailableFieldsAdapter : ReadAdapterBase
{
    public override string Domain => "unavailable_fields";
    public override int Priority => 1000;

    public override StateAdapterResult Collect(long tick)
    {
        var fields = new Dictionary<string, object>
        {
            ["task_state"] = Unavailable("planner_not_connected", "backend planner", tick, "not_connected"),
            ["memory_state"] = Unavailable("backend_not_connected", "backend memory", tick, "not_connected"),
            ["user_state"] = Unavailable("backend_not_connected", "backend user profile", tick, "not_connected"),
            ["event_stream_websocket"] = Unavailable("event_stream_websocket_not_implemented", "bridge websocket server", tick)
        };

        return Section("menus", fields, new[]
        {
            "task_state",
            "memory_state",
            "user_state",
            "event_stream_websocket"
        }, "unavailable");
    }
}
