using HarmonyLib;
using Microsoft.Xna.Framework;
using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Reflection;
using System.Text.Json;
using StardewAI.Contracts.Training;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.GameData.Crops;
using StardewValley.Locations;
using StardewValley.Menus;
using StardewValley.Monsters;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;
using StardewValley.Tools;
using XnaRectangle = Microsoft.Xna.Framework.Rectangle;
using TileLocation = xTile.Dimensions.Location;
using TileRectangle = xTile.Dimensions.Rectangle;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry : Mod
{
    private sealed class ActiveSleep
    {
        public ActiveSleep(PendingExecution pending, Point startTile, Point bedTile, Point standTile, List<Point> path, int startYear, int startDay, int startTime, string startSeason)
        {
            Pending = pending;
            StartTile = startTile;
            BedTile = bedTile;
            StandTile = standTile;
            Path = path;
            StartYear = startYear;
            StartDay = startDay;
            StartTime = startTime;
            StartSeason = startSeason;
            MaxTicks = Math.Max(600, path.Count * 90 + 600);
            LastPosition = Game1.player.Position;
        }

        public PendingExecution Pending { get; }
        public Point StartTile { get; }
        public Point BedTile { get; }
        public Point StandTile { get; }
        public List<Point> Path { get; }
        public int StartYear { get; }
        public int StartDay { get; }
        public int StartTime { get; }
        public string StartSeason { get; }
        public string StartedAt { get; } = DateTimeOffset.UtcNow.ToString("O");
        public SleepStage Stage { get; set; } = SleepStage.MoveToStand;
        public int PathIndex { get; set; }
        public int StuckTicks { get; set; }
        public Vector2 LastPosition { get; set; }
        public int PromptWaitTicks { get; set; }
        public int PostSleepWaitTicks { get; set; }
        public int ElapsedTicks { get; set; }
        public int MaxTicks { get; }
        public ShipSummaryClosePhase SummaryPhase { get; set; }
        public int SummaryPhaseStartTick { get; set; }
        public bool SummaryPositionSet { get; set; }
        public bool SummaryPositionVerified { get; set; }
        public Point SummaryPositionTarget { get; set; }
        public bool SummaryButtonPressed { get; set; }
        public bool SummaryButtonReleased { get; set; }
        public int SummaryReleaseRetries { get; set; }
    }

    private enum ShipSummaryClosePhase
    {
        WaitReady,
        Position,
        PositionVerify,
        Press,
        Release,
        WaitClose
    }

    private enum SleepStage
    {
        MoveToStand,
        StepOntoSleepTouchTile,
        TriggerPrompt,
        ConfirmPrompt,
        WaitForNewDay,
        WaitForPostSleepStable
    }

    private sealed class SleepTarget
    {
        public SleepTarget(Point bedTile, Point standTile)
        {
            BedTile = bedTile;
            StandTile = standTile;
        }

        public Point BedTile { get; }
        public Point StandTile { get; }
    }

    private sealed class ActiveWait
    {
        public ActiveWait(PendingExecution pending, int targetTicks)
        {
            Pending = pending;
            TargetTicks = targetTicks;
            StartedAt = DateTimeOffset.UtcNow.ToString("O");
        }

        public PendingExecution Pending { get; }
        public int TargetTicks { get; }
        public int ElapsedTicks { get; set; }
        public string StartedAt { get; }
    }

    private enum ShipPhase
    {
        BinFace,
        BinPress,
        BinRelease,
        WaitForShippingMenu,
        SlotDispatch,
        WaitForSlotDispatch,
        VerifyAndClose
    }

    private enum DialogueAdvanceStage
    {
        WaitTransition,
        Press,
        ReleaseAfterAdvance,
        WaitAdvanceEffect,
        CheckClose
    }

    private enum SkullKeyChestStage
    {
        OpenChest,
        WaitForOpenAnimation,
        ClaimItem,
        WaitForPostcondition
    }

    private sealed class ActiveSkullKeyChestInteraction
    {
        public ActiveSkullKeyChestInteraction(PendingExecution pending, MineShaft mine, Point target)
        {
            Pending = pending;
            Mine = mine;
            Target = target;
            HasSkullKeyBefore = Game1.player.hasSkullKey;
        }

        public PendingExecution Pending { get; }
        public MineShaft Mine { get; }
        public Point Target { get; }
        public bool HasSkullKeyBefore { get; }
        public string StartedAt { get; } = DateTimeOffset.UtcNow.ToString("O");
        public int MaxTicks { get; } = 360;
        public int ElapsedTicks { get; set; }
        public int StageStartedAtTick { get; set; }
        public int ClaimAttempts { get; set; }
        public bool OpenHandled { get; set; }
        public bool ClaimHandled { get; set; }
        public int KeyObservedAtTick { get; set; }
        public int LastDismissAttemptTick { get; set; }
        public int DismissAttempts { get; set; }
        public bool DismissActionHeld { get; set; }
        public SkullKeyChestStage Stage { get; set; }
    }

    private sealed class ActiveDialogueAdvance
    {
        public ActiveDialogueAdvance(PendingExecution pending, DialogueBox initialMenu)
        {
            Pending = pending;
            InitialMenu = initialMenu;
            InitialSpeakerName = initialMenu.characterDialogue?.speaker?.Name ?? string.Empty;
            StartedAt = DateTimeOffset.UtcNow.ToString("O");
            MaxTicks = 600;
            MaxPressAttempts = 60;
            BeforeMenuType = "DialogueBox";
            BeforeIsQuestion = initialMenu.isQuestion;
            BeforeResponseCount = initialMenu.responses?.Length ?? 0;
            BeforeSpeakerName = InitialSpeakerName;
            BeforeEventUp = Game1.eventUp;
        }

        public PendingExecution Pending { get; }
        public DialogueBox InitialMenu { get; }
        public string InitialSpeakerName { get; }
        public string StartedAt { get; }
        public int ElapsedTicks { get; set; }
        public int MaxTicks { get; }
        public int MaxPressAttempts { get; }
        public int PressAttempts { get; set; }
        public int AdvanceWaitTicks { get; set; }
        public int TransitionWaitTicks { get; set; }
        public int CheckCloseTicks { get; set; }
        public DialogueAdvanceStage Stage { get; set; } = DialogueAdvanceStage.WaitTransition;
        public bool SawDialogueFinishedBeforePress { get; set; }
        public bool SawShowTypingBeforePress { get; set; }
        public bool SawTransitioningBeforePress { get; set; }
        public string BeforeMenuType { get; }
        public bool BeforeIsQuestion { get; }
        public int BeforeResponseCount { get; }
        public string BeforeSpeakerName { get; }
        public bool BeforeEventUp { get; }
    }

    private sealed class ActiveShipInventoryToBin
    {
        public ActiveShipInventoryToBin(PendingExecution pending, ShippingBin bin, Point actionTile, int slotIndex,
            string qualifiedItemId, string unqualifiedItemId, int quantity,
            int inventoryCountBefore, int binCountBefore, int binTotalCountBefore,
            int binDistinctCountBefore, string binSignatureBefore, int basicShippedCountBefore,
            string beforeSlotQualifiedId, int beforeSlotStack, string beforeSlotItemId)
        {
            Pending = pending;
            Bin = bin;
            ActionTile = actionTile;
            SlotIndex = slotIndex;
            QualifiedItemId = qualifiedItemId;
            UnqualifiedItemId = unqualifiedItemId;
            Quantity = quantity;
            InventoryCountBefore = inventoryCountBefore;
            BinCountBefore = binCountBefore;
            BinTotalCountBefore = binTotalCountBefore;
            BinDistinctCountBefore = binDistinctCountBefore;
            BinSignatureBefore = binSignatureBefore;
            BasicShippedCountBefore = basicShippedCountBefore;
            BeforeSlotQualifiedId = beforeSlotQualifiedId;
            BeforeSlotStack = beforeSlotStack;
            BeforeSlotItemId = beforeSlotItemId;
            StartedAt = DateTimeOffset.UtcNow.ToString("O");
            Phase = ShipPhase.BinFace;
            PhaseStartTick = 0;
        }

        public PendingExecution Pending { get; }
        public ShippingBin Bin { get; }
        public Point ActionTile { get; }
        public int SlotIndex { get; }
        public string QualifiedItemId { get; }
        public string UnqualifiedItemId { get; }
        public int Quantity { get; }
        public int InventoryCountBefore { get; }
        public int BinCountBefore { get; }
        public int BinTotalCountBefore { get; }
        public int BinDistinctCountBefore { get; }
        public string BinSignatureBefore { get; }
        public int BasicShippedCountBefore { get; }
        public string BeforeSlotQualifiedId { get; }
        public int BeforeSlotStack { get; }
        public string BeforeSlotItemId { get; }
        public string StartedAt { get; }
        public ShipPhase Phase { get; set; }
        public int PhaseStartTick { get; set; }
        public bool FacingSet { get; set; }
        public bool NativeActionDispatched { get; set; }
        public bool ButtonPressed { get; set; }
        public bool ButtonReleased { get; set; }
        public int ReleaseRetries { get; set; }
        public bool SawShippingMenu { get; set; }
        public bool SlotClickDispatched { get; set; }
        public bool SawShipDispatch { get; set; }
        public int AfterSlotStack { get; set; }
        public string AfterSlotQualifiedId { get; set; } = string.Empty;
        public int ElapsedTicks { get; set; }
        public int MaxTicks { get; } = 300;
    }

    private sealed class ShippingReceipt
    {
        [System.Text.Json.Serialization.JsonPropertyName("receipt_id")]
        public string ReceiptId { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("status")]
        public string Status { get; set; } = "pending";

        [System.Text.Json.Serialization.JsonPropertyName("run_id")]
        public string RunId { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("queue_id")]
        public string QueueId { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("queue_item_id")]
        public string QueueItemId { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("request_nonce")]
        public string RequestNonce { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("feedback_appended")]
        public bool FeedbackAppended { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("unqualified_item_id")]
        public string UnqualifiedItemId { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("qualified_item_id")]
        public string QualifiedItemId { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("quantity")]
        public int Quantity { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("source_date")]
        public string SourceDate { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("source_season")]
        public string SourceSeason { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("source_day_of_month")]
        public string SourceDayOfMonth { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("source_year")]
        public string SourceYear { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("pre_basic_shipped_count")]
        public int PreBasicShippedCount { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("pre_inventory_count")]
        public int PreInventoryCount { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("pre_bin_count")]
        public int PreBinCount { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("pre_bin_total")]
        public int PreBinTotal { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("pre_bin_distinct")]
        public int PreBinDistinct { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("pre_bin_signature")]
        public string PreBinSignature { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("pre_slot_stack")]
        public int PreSlotStack { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("pre_slot_qualified_id")]
        public string PreSlotQualifiedId { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("after_inventory_count")]
        public int AfterInventoryCount { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("after_bin_count")]
        public int AfterBinCount { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("after_bin_total")]
        public int AfterBinTotal { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("after_bin_distinct")]
        public int AfterBinDistinct { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("after_bin_signature")]
        public string AfterBinSignature { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("after_slot_stack")]
        public int AfterSlotStack { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("after_slot_qualified_id")]
        public string AfterSlotQualifiedId { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("slot_index")]
        public int SlotIndex { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("created_at")]
        public string CreatedAt { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("expires_at")]
        public string? ExpiresAt { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("settled_at")]
        public string? SettledAt { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("settlement_status")]
        public string? SettlementStatus { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("settlement_reason")]
        public string? SettlementReason { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("settled_basic_shipped_count")]
        public int? SettledBasicShippedCount { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("settled_game_date")]
        public string? SettledGameDate { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("settled_season")]
        public string? SettledSeason { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("settled_day_of_month")]
        public string? SettledDayOfMonth { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("settled_year")]
        public string? SettledYear { get; set; }
    }}
