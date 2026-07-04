namespace StardewAI.TransparentBridge;

public sealed class BridgeConfig
{
    public string Host { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 8765;
    public string PermissionMode { get; set; } = "observer";
}
