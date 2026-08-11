using StardewAI.Contracts.Training;
using StardewValley.Menus;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private sealed class ActiveMailProcessing
    {
        public ActiveMailProcessing(
            PendingExecution pending,
            LetterViewerMenu menu,
            MailAttachmentExpectation[] attachments,
            Dictionary<string, int> inventoryBefore,
            int maxStaminaBefore)
        {
            Pending = pending;
            Menu = menu;
            Attachments = attachments;
            InventoryBefore = inventoryBefore;
            MaxStaminaBefore = maxStaminaBefore;
        }

        public PendingExecution Pending { get; }
        public LetterViewerMenu Menu { get; }
        public MailAttachmentExpectation[] Attachments { get; }
        public Dictionary<string, int> InventoryBefore { get; }
        public int MaxStaminaBefore { get; }
        public string StartedAt { get; } = DateTimeOffset.UtcNow.ToString("O");
        public int StardropCount => Attachments.Count(row => row.QualifiedItemId == "(O)434");
        public int ElapsedTicks { get; set; }
        public int LastInputTick { get; set; } = -10;
        public int PageClicks { get; set; }
        public int AttachmentClicks { get; set; }
        public bool QuestAccepted { get; set; }
        public bool CloseClicked { get; set; }
        public MailProcessingStage Stage { get; set; } = MailProcessingStage.WaitReady;
    }

    private sealed record MailAttachmentExpectation(string QualifiedItemId, int Stack, int Quality);

    private enum MailProcessingStage
    {
        WaitReady,
        AdvancePages,
        AcceptQuest,
        CollectAttachments,
        CloseLetter,
        WaitNativeAftermath
    }
}
