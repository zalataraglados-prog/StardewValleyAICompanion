using StardewAI.Contracts.Training;
using StardewModdingAPI;
using StardewValley;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private TrainingExecutionResult ExecuteSetupMultiplayerChatFixture(TrainingExecutionRequest request)
    {
        var started = DateTimeOffset.UtcNow.ToString("O");
        var chat = Game1.chatBox;
        if (!Context.IsWorldReady || Game1.IsClient || chat is null)
            return BlockedWithPrimitive(request, "debug_setup_multiplayer_chat",
                "local_multiplayer_chat_fixture=true", "client_or_world_not_ready",
                "multiplayer_chat_fixture_requires_loaded_singleplayer_or_host_world");

        Game1.exitActiveMenu();
        chat.clickAway();
        Game1.StartLocalMultiplayerIfNecessary();
        var recipient = Game1.getAllFarmers().FirstOrDefault(farmer =>
            farmer.UniqueMultiplayerID != Game1.player.UniqueMultiplayerID && !farmer.isUnclaimedFarmhand);
        if (recipient is null)
        {
            recipient = new Farmer
            {
                UniqueMultiplayerID = Game1.player.UniqueMultiplayerID + 700001,
                Name = "Chat Test Farmhand",
                displayName = "Chat Test Farmhand"
            };
        }
        else
        {
            recipient.displayName = "Chat Test Farmhand";
        }
        Game1.otherFarmers.Clear();
        Game1.otherFarmers.Add(recipient.UniqueMultiplayerID, recipient);
        config.DedicatedHostActorId = "ai_host.main";
        config.DedicatedHostFarmerId = Game1.player.UniqueMultiplayerID.ToString(System.Globalization.CultureInfo.InvariantCulture);
        chat.messages.Clear();
        var verified = Game1.IsServer && Game1.IsMultiplayer && recipient.isActive() &&
            Game1.otherFarmers.Count == 1 && chat.messages.Count == 0;
        return new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            StartedAt = started,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "debug_setup_multiplayer_chat",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[] { "isolated_local_server_and_one_active_chat_recipient_ready" }
                : new[] { "multiplayer_chat_fixture_setup_mismatch" },
            RequestedEffect = "local_multiplayer_chat_fixture=true",
            ObservedEffect = "network_role=" + (Game1.IsServer ? "server" : Game1.IsClient ? "client" : "none") +
                ";active_recipient_count=" + Game1.otherFarmers.Count,
            BlockReasons = verified ? Array.Empty<string>() : new[] { "multiplayer_chat_fixture_setup_mismatch" }
        };
    }
}
