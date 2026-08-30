using StardewAI.Contracts.Capabilities;

namespace StardewAI.Backend.Tests;

public sealed class RuntimeMultiplayerWalletExecutorTests
{
    [Fact]
    public void RuntimeDispatchUsesNativeLedgerDialogueAndDigitInputOnly()
    {
        var source = RuntimeHarnessSources.File("ModEntry.MultiplayerWallet.cs");
        var all = RuntimeHarnessSources.All;
        Assert.Contains("StartMultiplayerWallet(pending);", all);
        Assert.Contains("active.Manor.checkAction(", source);
        Assert.Contains("TryClickMultiplayerWalletDialogue", source);
        Assert.Contains("GetField(\"digits\"", source);
        Assert.Contains("MultiplayerWalletImmediateReceiptMatches", source);
        Assert.True(RuntimeTestHarnessDispatchCatalog.IsSupported("executor.manage_multiplayer_wallet"));
    }
}
