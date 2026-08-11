using Microsoft.Xna.Framework;
using StardewAI.Contracts.Mail;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Locations;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private TrainingExecutionResult ExecuteSetupMailFixture(TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
            return BlockedWithPrimitive(request, "debug_setup_mail", "mailbox_first=" + request.TargetRuntimeIdentity, MailFixtureObserved(), reasons.ToArray());
        if (string.IsNullOrWhiteSpace(request.TargetRuntimeIdentity) ||
            !DataLoader.Mail(Game1.content).ContainsKey(request.TargetRuntimeIdentity))
        {
            return BlockedWithPrimitive(request, "debug_setup_mail", "mailbox_first=known_Data_mail_id", MailFixtureObserved(), "mail_fixture_data_id_required");
        }

        var farm = Game1.getFarm();
        var endpoint = FindRuntimeMailboxEndpoint(farm, Game1.player);
        if (endpoint is null)
            return BlockedWithPrimitive(request, "debug_setup_mail", "owned_mailbox_endpoint=true", MailFixtureObserved(), "mail_fixture_owned_mailbox_unavailable");

        Game1.exitActiveMenu();
        Game1.nextClickableMenu.Clear();
        var player = Game1.player;
        while (player.mailbox.Remove(request.TargetRuntimeIdentity)) { }
        while (player.mailReceived.Remove(request.TargetRuntimeIdentity)) { }
        player.mailForTomorrow.RemoveWhere(value => value.Split(new[] { "%&NL&%" }, StringSplitOptions.None)[0] == request.TargetRuntimeIdentity);
        foreach (var directive in MailDirectiveParser.Parse(DataLoader.Mail(Game1.content)[request.TargetRuntimeIdentity]))
        {
            if (directive.Command.Equals("quest", StringComparison.OrdinalIgnoreCase) && directive.Arguments.Length > 0)
                player.questLog.RemoveWhere(quest => quest.id.Value == directive.Arguments[0]);
            if (directive.Command.Equals("craftingrecipe", StringComparison.OrdinalIgnoreCase) && directive.Arguments.Length > 0)
            {
                var recipe = directive.Arguments[0];
                if (!CraftingRecipe.craftingRecipes.ContainsKey(recipe))
                    recipe = recipe.Replace('_', ' ');
                player.craftingRecipes.Remove(recipe);
            }
        }
        player.mailbox.Insert(0, request.TargetRuntimeIdentity);
        Game1.warpFarmer(farm.NameOrUniqueName, endpoint.Value.Stand.X, endpoint.Value.Stand.Y, false);
        player.Position = endpoint.Value.Stand.ToVector2() * Game1.tileSize;

        var verified = player.mailbox.FirstOrDefault() == request.TargetRuntimeIdentity &&
            Game1.activeClickableMenu is null;
        return new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            StartedAt = DateTimeOffset.UtcNow.ToString("O"),
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "debug_setup_mail",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified ? new[] { "real_Data_mail_id_queued_at_owned_mailbox" } : new[] { "mail_fixture_setup_mismatch" },
            RequestedEffect = "mailbox_first=" + request.TargetRuntimeIdentity,
            ObservedEffect = MailFixtureObserved(),
            BlockReasons = verified ? Array.Empty<string>() : new[] { "mail_fixture_setup_mismatch" },
            ChangedFacts = new[]
            {
                new SimulatedFactChange { Path = "quests.mailbox_processing.pending_mail_id", Before = string.Empty, After = player.mailbox.FirstOrDefault() ?? string.Empty },
                new SimulatedFactChange { Path = "player.location_id", Before = string.Empty, After = Game1.currentLocation.NameOrUniqueName },
                new SimulatedFactChange { Path = "player.tile", Before = string.Empty, After = Game1.player.TilePoint.X + "," + Game1.player.TilePoint.Y }
            }
        };
    }

    private static (Point Action, Point Stand)? FindRuntimeMailboxEndpoint(Farm farm, Farmer player)
    {
        var action = player.getMailboxPosition();
        foreach (var stand in new[] { new Point(action.X, action.Y + 1), new Point(action.X - 1, action.Y), new Point(action.X + 1, action.Y), new Point(action.X, action.Y - 1) })
        {
            if (farm.isTilePassable(new xTile.Dimensions.Location(stand.X, stand.Y), Game1.viewport) &&
                !farm.IsTileBlockedBy(stand.ToVector2(), CollisionMask.All & ~CollisionMask.Farmers, CollisionMask.None, useFarmerTile: true))
                return (action, stand);
        }
        return null;
    }

    private static string MailFixtureObserved() =>
        "mailbox_first=" + (Game1.player?.mailbox.FirstOrDefault() ?? string.Empty) +
        ";location=" + (Game1.currentLocation?.NameOrUniqueName ?? string.Empty) +
        ";tile=" + (Game1.player?.TilePoint.X ?? -1) + "," + (Game1.player?.TilePoint.Y ?? -1) +
        ";menu=" + (Game1.activeClickableMenu?.GetType().Name ?? "none");
}
