using System.Text.Json;
using StardewAI.Contracts.Mail;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Menus;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private void StartMailProcessing(PendingExecution pending, LetterViewerMenu menu)
    {
        var reasons = ValidateExecutionRequest(pending.Request);
        if (reasons.Count > 0)
        {
            pending.Completion.SetResult(Blocked(pending.Request, reasons.ToArray()));
            return;
        }

        if (activeMailProcessing is not null)
        {
            pending.Completion.SetResult(MailBlocked(pending.Request, "mail_processing_executor_busy"));
            return;
        }

        if (!menu.isMail || menu.isFromCollection ||
            !string.Equals(menu.mailTitle, pending.Request.TargetRuntimeIdentity, StringComparison.Ordinal))
        {
            pending.Completion.SetResult(MailBlocked(pending.Request, "mail_runtime_identity_mismatch"));
            return;
        }

        if (!TryParseMailAttachments(pending.Request.ExpectedOutputItemsJson, out var expected) ||
            !MailAttachmentsMatch(menu, expected))
        {
            pending.Completion.SetResult(MailBlocked(pending.Request, "mail_attachment_projection_mismatch"));
            return;
        }

        if (!string.Equals(menu.questID ?? string.Empty, pending.Request.QuestId, StringComparison.Ordinal) ||
            !string.Equals(menu.specialOrderId ?? string.Empty, pending.Request.QuestKey, StringComparison.Ordinal))
        {
            pending.Completion.SetResult(MailBlocked(pending.Request, "mail_quest_or_special_order_identity_mismatch"));
            return;
        }

        var requiredSlots = expected.Count(row => MailDirectiveParser.AttachmentRequiresInventorySlot(row.QualifiedItemId));
        var emptySlots = Math.Max(0, Game1.player.MaxItems - Game1.player.Items.Take(Game1.player.MaxItems).Count(item => item is not null));
        if (requiredSlots > emptySlots)
        {
            pending.Completion.SetResult(MailBlocked(pending.Request, "mail_attachment_capacity_insufficient"));
            return;
        }

        var inventoryBefore = expected
            .Select(row => row.QualifiedItemId)
            .Distinct(StringComparer.Ordinal)
            .ToDictionary(id => id, CountInventoryItems, StringComparer.Ordinal);
        activeMailProcessing = new ActiveMailProcessing(
            pending,
            menu,
            expected,
            inventoryBefore,
            Game1.player.MaxStamina);
    }

    private void TickMailProcessing()
    {
        if (activeMailProcessing is null)
            return;

        var active = activeMailProcessing;
        active.ElapsedTicks++;
        try
        {
            if (active.ElapsedTicks > 1800)
            {
                FinishMailProcessing(active, "blocked", "timeout", "mail_native_processing_timeout");
                return;
            }

            if (active.Stage != MailProcessingStage.WaitNativeAftermath &&
                !ReferenceEquals(Game1.activeClickableMenu, active.Menu))
            {
                FinishMailProcessing(active, "blocked", "observed_mismatch", "mail_menu_changed_before_native_close");
                return;
            }

            if (active.ElapsedTicks - active.LastInputTick < 4)
                return;

            switch (active.Stage)
            {
                case MailProcessingStage.WaitReady:
                    if (active.Menu.scale < 1f || !active.Menu.readyToClose())
                        return;
                    active.Stage = MailProcessingStage.AdvancePages;
                    break;

                case MailProcessingStage.AdvancePages:
                    if (active.Menu.page < active.Menu.mailMessage.Count - 1)
                    {
                        var point = active.Menu.forwardButton.bounds.Center;
                        active.Menu.receiveLeftClick(point.X, point.Y);
                        active.PageClicks++;
                        active.LastInputTick = active.ElapsedTicks;
                        return;
                    }
                    active.Stage = MailProcessingStage.AcceptQuest;
                    break;

                case MailProcessingStage.AcceptQuest:
                    if (active.Menu.HasQuestOrSpecialOrder)
                    {
                        var button = active.Menu.acceptQuestButton;
                        if (button is null || !button.visible)
                        {
                            FinishMailProcessing(active, "blocked", "observed_mismatch", "mail_accept_button_not_visible");
                            return;
                        }
                        var point = button.bounds.Center;
                        active.Menu.receiveLeftClick(point.X, point.Y);
                        active.QuestAccepted = true;
                        active.LastInputTick = active.ElapsedTicks;
                        return;
                    }
                    active.Stage = MailProcessingStage.CollectAttachments;
                    break;

                case MailProcessingStage.CollectAttachments:
                    var attachment = active.Menu.itemsToGrab.FirstOrDefault(row => row.item is not null);
                    if (attachment is not null)
                    {
                        if (!attachment.visible)
                        {
                            FinishMailProcessing(active, "blocked", "observed_mismatch", "mail_attachment_not_visible_on_final_page");
                            return;
                        }
                        var point = attachment.bounds.Center;
                        active.Menu.receiveLeftClick(point.X, point.Y);
                        active.AttachmentClicks++;
                        active.LastInputTick = active.ElapsedTicks;
                        return;
                    }
                    active.Stage = MailProcessingStage.CloseLetter;
                    break;

                case MailProcessingStage.CloseLetter:
                    if (active.Menu.upperRightCloseButton is null || !active.Menu.readyToClose())
                    {
                        FinishMailProcessing(active, "blocked", "observed_mismatch", "mail_close_button_unavailable");
                        return;
                    }
                    var close = active.Menu.upperRightCloseButton.bounds.Center;
                    active.Menu.receiveLeftClick(close.X, close.Y);
                    active.CloseClicked = true;
                    active.LastInputTick = active.ElapsedTicks;
                    active.Stage = MailProcessingStage.WaitNativeAftermath;
                    break;

                case MailProcessingStage.WaitNativeAftermath:
                    TickMailNativeAftermath(active);
                    break;
            }
        }
        catch (Exception ex)
        {
            FinishMailProcessing(active, "blocked", "exception", "mail_native_processing_exception:" + ex.GetType().Name);
        }
    }

    private void TickMailNativeAftermath(ActiveMailProcessing active)
    {
        if (ReferenceEquals(Game1.activeClickableMenu, active.Menu))
            return;

        if (Game1.activeClickableMenu is ItemGrabMenu { source: 4 })
            return;

        if (Game1.activeClickableMenu is DialogueBox dialogue && active.StardropCount > 0)
        {
            if (active.ElapsedTicks - active.LastInputTick >= 12)
            {
                dialogue.receiveLeftClick(
                    dialogue.xPositionOnScreen + dialogue.width / 2,
                    dialogue.yPositionOnScreen + dialogue.height / 2);
                active.LastInputTick = active.ElapsedTicks;
            }
            return;
        }

        if (Game1.activeClickableMenu is not null)
        {
            FinishMailProcessing(active, "blocked", "observed_mismatch", "unexpected_menu_after_mail:" + Game1.activeClickableMenu.GetType().Name);
            return;
        }

        if (Game1.nextClickableMenu.Any(menu => menu is ItemGrabMenu { source: 4 }))
            return;

        if (active.StardropCount > 0 &&
            (Game1.player.MaxStamina != active.MaxStaminaBefore + active.StardropCount * 34 || !Game1.player.CanMove))
        {
            return;
        }

        var failures = MailReceiptFailures(active);
        FinishMailProcessing(
            active,
            failures.Length == 0 ? "applied" : "blocked",
            failures.Length == 0 ? "verified" : "observed_mismatch",
            failures.Length == 0 ? "mail_native_lifecycle_completed" : string.Join(";", failures));
    }

    private string[] MailReceiptFailures(ActiveMailProcessing active)
    {
        var failures = new List<string>();
        foreach (var group in active.Attachments
            .Where(row => MailDirectiveParser.AttachmentRequiresInventorySlot(row.QualifiedItemId))
            .GroupBy(row => row.QualifiedItemId, StringComparer.Ordinal))
        {
            var expected = active.InventoryBefore[group.Key] + group.Sum(row => row.Stack);
            if (CountInventoryItems(group.Key) < expected)
                failures.Add("mail_attachment_receipt_mismatch:" + group.Key);
        }

        if (!string.IsNullOrWhiteSpace(active.Pending.Request.QuestId) &&
            !Game1.player.questLog.Any(quest => string.Equals(quest.id.Value, active.Pending.Request.QuestId, StringComparison.Ordinal)))
        {
            failures.Add("mail_quest_not_accepted:" + active.Pending.Request.QuestId);
        }
        if (!string.IsNullOrWhiteSpace(active.Pending.Request.QuestKey) &&
            !Game1.player.team.specialOrders.Any(order => string.Equals(order.questKey.Value, active.Pending.Request.QuestKey, StringComparison.Ordinal)))
        {
            failures.Add("mail_special_order_not_accepted:" + active.Pending.Request.QuestKey);
        }
        return failures.ToArray();
    }

    private void FinishMailProcessing(ActiveMailProcessing active, string status, string verification, string reason)
    {
        activeMailProcessing = null;
        var verified = status == "applied";
        active.Pending.Completion.SetResult(new TrainingExecutionResult
        {
            RunId = active.Pending.Request.RunId,
            QueueId = active.Pending.Request.QueueId,
            QueueItemId = active.Pending.Request.QueueItemId,
            BeforeStateHash = active.Pending.Request.BeforeStateHash,
            OptionId = active.Pending.Request.OptionId,
            Status = status,
            FeedbackAvailable = true,
            StartedAt = active.StartedAt,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "process_mail_letter",
            PrimitiveVerificationStatus = verification,
            PrimitiveVerificationReasons = new[] { reason, "page_clicks=" + active.PageClicks, "attachment_clicks=" + active.AttachmentClicks },
            RequestedEffect = "native_letter_completed=" + active.Pending.Request.TargetRuntimeIdentity,
            ObservedEffect = "active_menu=" + (Game1.activeClickableMenu?.GetType().Name ?? "none") +
                ";max_stamina=" + Game1.player.MaxStamina +
                ";quest_accepted=" + active.QuestAccepted.ToString().ToLowerInvariant(),
            BlockReasons = verified ? Array.Empty<string>() : new[] { reason },
            ChangedFacts = new[]
            {
                new SimulatedFactChange { Path = "menus.active_menu.type", Before = "LetterViewerMenu", After = Game1.activeClickableMenu?.GetType().Name ?? "none" },
                new SimulatedFactChange { Path = "player.max_stamina", Before = active.MaxStaminaBefore.ToString(), After = Game1.player.MaxStamina.ToString() }
            }
        });
    }

    private static TrainingExecutionResult MailBlocked(TrainingExecutionRequest request, string reason) =>
        BlockedWithPrimitive(request, "process_mail_letter", "native_letter_completed=" + request.TargetRuntimeIdentity,
            "active_menu=" + (Game1.activeClickableMenu?.GetType().Name ?? "none"), reason);

    private static bool TryParseMailAttachments(string json, out MailAttachmentExpectation[] attachments)
    {
        try
        {
            using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "[]" : json);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                attachments = Array.Empty<MailAttachmentExpectation>();
                return false;
            }
            attachments = document.RootElement.EnumerateArray()
                .Where(row => row.TryGetProperty("present", out var present) && present.ValueKind == JsonValueKind.True)
                .Select(row => new MailAttachmentExpectation(
                    row.TryGetProperty("qualified_item_id", out var id) ? id.GetString() ?? string.Empty : string.Empty,
                    row.TryGetProperty("stack", out var stack) && stack.TryGetInt32(out var parsedStack) ? parsedStack : 0,
                    row.TryGetProperty("quality", out var quality) && quality.TryGetInt32(out var parsedQuality) ? parsedQuality : 0))
                .ToArray();
            return attachments.All(row => !string.IsNullOrWhiteSpace(row.QualifiedItemId) && row.Stack > 0);
        }
        catch (JsonException)
        {
            attachments = Array.Empty<MailAttachmentExpectation>();
            return false;
        }
    }

    private static bool MailAttachmentsMatch(LetterViewerMenu menu, MailAttachmentExpectation[] expected)
    {
        var actual = menu.itemsToGrab
            .Where(row => row.item is not null)
            .Select(row => new MailAttachmentExpectation(row.item.QualifiedItemId, row.item.Stack, row.item.Quality))
            .ToArray();
        return actual.SequenceEqual(expected);
    }

}
