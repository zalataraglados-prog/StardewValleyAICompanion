using StardewValley;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class ShopAccessReadAdapter
{
    private const string CommunityCenterWarpTargetLocation = "CommunityCenter";
    private const int CommunityCenterWarpTargetX = 32;
    private const int CommunityCenterWarpTargetY = 23;

    private static bool IsCommunityCenterWarpAction(string? branch) =>
        string.Equals(branch, "WarpCommunityCenter", StringComparison.OrdinalIgnoreCase);

    private static bool IsCommunityCenterDoorUnlocked() =>
        Game1.MasterPlayer?.mailReceived.Contains("ccDoorUnlock") == true ||
        Game1.MasterPlayer?.mailReceived.Contains("JojaMember") == true;
}
