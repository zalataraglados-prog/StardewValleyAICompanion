namespace StardewAI.TransparentBridge.Transport;

public interface IBridgeTransport
{
    string Name { get; }
    bool IsReadOnly { get; }
}

public sealed class LoopbackHttpBridgeTransport : IBridgeTransport
{
    public string Name => "loopback_http";
    public bool IsReadOnly => true;
}
