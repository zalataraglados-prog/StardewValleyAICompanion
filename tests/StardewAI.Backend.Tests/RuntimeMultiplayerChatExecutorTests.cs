using StardewAI.Contracts.Capabilities;

namespace StardewAI.Backend.Tests;

public sealed class RuntimeMultiplayerChatExecutorTests
{
    [Fact]
    public void RuntimeDispatchUsesOnlyNativeChatBoxInputAndSenderLocalReceipt()
    {
        var source = RuntimeHarnessSources.File("ModEntry.MultiplayerChat.cs");
        var all = RuntimeHarnessSources.All;
        Assert.Contains("ExecuteSendMultiplayerChat(pending.Request)", all);
        Assert.Contains("chat.activate();", source);
        Assert.Contains("chat.chatBox.RecieveTextInput(character);", source);
        Assert.Contains("chat.textBoxEnter(chat.chatBox);", source);
        Assert.Contains("MultiplayerChatReceiptEquals", source);
        Assert.DoesNotContain("Game1.multiplayer.sendChatMessage", source);
        Assert.DoesNotContain("chat.receiveChatMessage", source);
        Assert.True(RuntimeTestHarnessDispatchCatalog.IsSupported("executor.send_multiplayer_chat"));
    }
}
