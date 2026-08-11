using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Xna.Framework;
using StardewAI.Contracts.Mail;
using StardewAI.Contracts.State;
using StardewValley;
using StardewValley.Locations;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class ProgressQuestReadAdapter
{
    private MailboxProcessingRef? ReadMailboxProcessing(Farmer? player)
    {
        if (player is null)
            return null;

        var queue = player.mailbox.ToArray();
        var pendingMailId = queue.FirstOrDefault() ?? string.Empty;
        var mailData = DataLoader.Mail(Game1.content);
        var raw = string.Empty;
        var dataFound = pendingMailId.Length > 0 && mailData.TryGetValue(pendingMailId, out raw);
        raw ??= string.Empty;
        var dynamicResolution = !dataFound && pendingMailId.StartsWith("passedOut", StringComparison.Ordinal)
            ? "GameLocation.mailbox_passed_out_dynamic_text"
            : string.Empty;
        var parsed = MailDirectiveParser.Parse(raw);
        var directives = parsed.Select(row => new MailDirectiveRef
        {
            Kind = row.Kind,
            ExecutionPhase = row.ExecutionPhase,
            SourceOffset = row.SourceOffset,
            Raw = row.Raw,
            Command = row.Command,
            Arguments = row.Arguments,
            Errors = row.Errors
        }).ToArray();
        var endpoint = FindOwnedMailboxEndpoint(Game1.getFarm(), player);
        var emptySlots = Math.Max(0, player.maxItems.Value - player.Items.Take(player.maxItems.Value).Count(item => item is not null));
        var attachmentSlots = AttachmentSlotUpperBound(parsed, player);
        var menuClear = Game1.activeClickableMenu is null && !Game1.dialogueUp;
        var reasons = new List<string>();
        if (queue.Length == 0) reasons.Add("mailbox_queue_empty");
        if (!dataFound && dynamicResolution.Length == 0) reasons.Add("mail_data_missing");
        if (parsed.SelectMany(row => row.Errors).Any()) reasons.Add("mail_directive_parse_error");
        if (endpoint is null) reasons.Add("owned_mailbox_endpoint_unavailable");
        if (attachmentSlots > emptySlots) reasons.Add("mail_attachment_capacity_insufficient");
        if (!menuClear) reasons.Add("mailbox_menu_or_dialogue_not_clear");

        return new MailboxProcessingRef
        {
            Available = queue.Length > 0 && endpoint is not null,
            QueueCount = queue.Length,
            QueueMailIdsNativeOrder = queue,
            PendingMailId = pendingMailId,
            MailDataFound = dataFound,
            MailDataSha256 = dataFound ? Sha256(raw) : string.Empty,
            DynamicNativeResolution = dynamicResolution,
            Directives = directives,
            ConstructorEffectClasses = ConstructorEffectClasses(parsed),
            AttachmentSlotUpperBound = attachmentSlots,
            InventoryEmptySlots = emptySlots,
            AttachmentCapacitySufficient = attachmentSlots <= emptySlots,
            MailReceivedOnOpen = pendingMailId.Length > 0 &&
                !pendingMailId.Contains("passedOut", StringComparison.Ordinal) &&
                !pendingMailId.Contains("Cooking", StringComparison.Ordinal),
            MailboxLocationId = Game1.getFarm()?.NameOrUniqueName ?? "Farm",
            MailboxActionTileX = endpoint?.Action.X,
            MailboxActionTileY = endpoint?.Action.Y,
            MailboxActionRaw = endpoint?.RawAction ?? string.Empty,
            StandTileX = endpoint?.Stand.X,
            StandTileY = endpoint?.Stand.Y,
            MenuClear = menuClear,
            Status = reasons.Count == 0 ? "ready" : "blocked",
            BlockedDiagnostics = reasons.Distinct(StringComparer.Ordinal).ToArray()
        };
    }

    private static int AttachmentSlotUpperBound(
        IEnumerable<ParsedMailDirective> directives,
        Farmer player)
    {
        var count = 0;
        foreach (var directive in directives.Where(row => row.Kind == "item"))
        {
            switch (directive.Command.ToLowerInvariant())
            {
                case "id":
                case "object":
                    if (!AllPossibleIdsResolveWithoutInventorySlot(directive.Arguments)) count++;
                    break;
                case "tools":
                    count += directive.Arguments.Count(value => value is "Axe" or "Hoe" or "Pickaxe" or "Can" or "Scythe");
                    break;
                case "bigobject":
                case "furniture":
                    count++;
                    break;
                case "itemrecovery" when player.recoveredItem is not null:
                    count++;
                    break;
            }
        }
        return count;
    }

    private static bool AllPossibleIdsResolveWithoutInventorySlot(IReadOnlyList<string> arguments)
    {
        var ids = arguments.Count == 1
            ? arguments
            : arguments.Where((_, index) => index % 2 == 0).ToArray();
        return ids.Count > 0 && ids.All(id => !MailDirectiveParser.AttachmentRequiresInventorySlot(id));
    }

    private static string[] ConstructorEffectClasses(IEnumerable<ParsedMailDirective> directives)
    {
        return directives.Select(row => row.Kind == "action"
                ? "trigger_action"
                : row.Command.ToLowerInvariant() switch
                {
                    "id" or "object" or "tools" or "bigobject" or "furniture" or "itemrecovery" => "attachment",
                    "money" => "money_immediate",
                    "conversationtopic" => "conversation_topic_immediate",
                    "cookingrecipe" => "cooking_recipe_immediate",
                    "craftingrecipe" => "crafting_recipe_immediate",
                    "quest" when row.Arguments.Length > 1 => "quest_immediate",
                    "quest" => "quest_on_close",
                    "specialorder" when row.Arguments.Length > 1 && bool.TryParse(row.Arguments[1], out var immediate) && immediate => "special_order_immediate",
                    "specialorder" => "special_order_on_close",
                    _ => "unknown"
                })
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
    }

    private static MailboxEndpoint? FindOwnedMailboxEndpoint(Farm? farm, Farmer player)
    {
        if (farm is null)
            return null;
        var action = player.getMailboxPosition();
        var stand = FindMailboxStandTile(farm, action.X, action.Y);
        return stand.HasValue ? new MailboxEndpoint(action, stand.Value, "Mailbox") : null;
    }

    private static Point? FindMailboxStandTile(Farm farm, int x, int y)
    {
        var collisionMask = CollisionMask.All & ~CollisionMask.Farmers;
        foreach (var tile in new[]
        {
            new Point(x, y + 1),
            new Point(x - 1, y),
            new Point(x + 1, y),
            new Point(x, y - 1)
        })
        {
            if (farm.isTilePassable(new xTile.Dimensions.Location(tile.X, tile.Y), Game1.viewport) &&
                !farm.IsTileBlockedBy(new Vector2(tile.X, tile.Y), collisionMask, CollisionMask.None, useFarmerTile: true))
            {
                return tile;
            }
        }
        return null;
    }

    private static string Sha256(string value) => Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private sealed record MailboxEndpoint(Point Action, Point Stand, string RawAction);
}
