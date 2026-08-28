using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Tools;
using System.Security.Cryptography;
using System.Text;
using StardewObject = StardewValley.Object;
using TileLocation = xTile.Dimensions.Location;
using TileRectangle = xTile.Dimensions.Rectangle;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private const string FarmComputerQualifiedItemId = "(BC)239";
    private const string FarmComputerNativeContract =
        "GameLocation.checkAction->Object.checkForAction_(BC)239->CheckForActionOnFarmComputer->delay_500ms->ShowFarmComputerReport->Game1.multipleDialogues";

    private void StartFarmComputerReport(PendingExecution pending)
    {
        var request = pending.Request;
        var genericReasons = ValidateExecutionRequest(request);
        if (genericReasons.Count > 0)
        {
            pending.Completion.SetResult(Blocked(request, genericReasons.ToArray()));
            return;
        }
        if (!request.TargetTileX.HasValue || !request.TargetTileY.HasValue ||
            !request.StandTileX.HasValue || !request.StandTileY.HasValue ||
            !request.SafeSlotIndex.HasValue || !request.RestoreSlotIndex.HasValue ||
            !request.FarmComputerExpectedDelayMs.HasValue ||
            !request.FarmComputerExpectedShakeTimer.HasValue ||
            !request.FarmComputerExpectedFreezeMs.HasValue ||
            !request.FarmComputerExpectedLocationActionReturn.HasValue ||
            string.IsNullOrWhiteSpace(request.FarmComputerRootLocationId) ||
            string.IsNullOrWhiteSpace(request.FarmComputerReportSha256))
        {
            pending.Completion.SetResult(FarmComputerBlocked(request, "farm_computer_typed_fields_required"));
            return;
        }
        if (Game1.activeClickableMenu is not null || Game1.dialogueUp ||
            Game1.player.UsingTool || !Game1.player.CanMove)
        {
            pending.Completion.SetResult(FarmComputerBlocked(request, "farm_computer_player_or_menu_not_ready"));
            return;
        }

        var location = Game1.currentLocation;
        var target = new Point(request.TargetTileX.Value, request.TargetTileY.Value);
        var stand = new Point(request.StandTileX.Value, request.StandTileY.Value);
        var reasons = ValidateFarmComputerTarget(location, target, stand, request, out var computer);
        if (reasons.Length > 0)
        {
            pending.Completion.SetResult(FarmComputerBlocked(request, reasons));
            return;
        }
        var maxMovementTiles = Math.Clamp(request.MaxMovementTiles ?? 512, 1, 512);
        var path = TryBuildTilePath(location, Game1.player.TilePoint, stand, maxMovementTiles,
            out var pathReason, avoidSoftObstacles: true, allowRemovableObstacles: false);
        if (path is null)
        {
            pending.Completion.SetResult(FarmComputerBlocked(
                request, "farm_computer_path_unavailable:" + pathReason));
            return;
        }

        nativeObjectInteractions.FarmComputer = new ActiveFarmComputer(
            pending, location, computer!, target, stand, path, maxMovementTiles);
    }

    private static string[] ValidateFarmComputerTarget(
        GameLocation location,
        Point target,
        Point stand,
        TrainingExecutionRequest request,
        out StardewObject? computer)
    {
        var reasons = new List<string>();
        computer = null;
        if (!location.objects.TryGetValue(target.ToVector2(), out var item) ||
            item.GetType() != typeof(StardewObject) ||
            !item.bigCraftable.Value ||
            !string.Equals(item.Name, "Farm Computer", StringComparison.Ordinal) ||
            !string.Equals(item.Type, "Crafting", StringComparison.Ordinal) ||
            !string.Equals(item.ItemId, "239", StringComparison.Ordinal) ||
            !string.Equals(item.QualifiedItemId, FarmComputerQualifiedItemId, StringComparison.Ordinal))
        {
            reasons.Add("farm_computer_exact_object_missing_or_drifted");
        }
        else
        {
            computer = item;
        }
        if (!AreAdjacent(target, stand) || !IsTileOnMap(location, stand) ||
            !IsTileWalkable(location, stand) || IsTileOccupiedByCharacter(location, stand))
        {
            reasons.Add("farm_computer_interaction_geometry_drifted");
        }
        if (IsDestructiveObjectTrap(location, stand))
            reasons.Add("farm_computer_destructive_object_trap_preamble_blocked");

        var safeSlotIndex = request.SafeSlotIndex.GetValueOrDefault(-1);
        if (safeSlotIndex is < 0 or > 11 || safeSlotIndex >= Game1.player.Items.Count)
        {
            reasons.Add("farm_computer_safe_toolbar_slot_drifted");
        }
        else
        {
            var safeItem = Game1.player.Items[safeSlotIndex];
            var safeKindMatches = request.FarmComputerSafeSlotKind switch
            {
                "empty" => safeItem is null,
                "tool" => safeItem is Tool,
                _ => false
            };
            if (!safeKindMatches)
                reasons.Add("farm_computer_safe_toolbar_slot_drifted");
        }
        if (request.RestoreSlotIndex is < 0 or > 11 ||
            request.RestoreSlotIndex != Game1.player.CurrentToolIndex)
        {
            reasons.Add("farm_computer_restore_slot_drifted");
        }

        if (computer is not null &&
            (request.FarmComputerExpectedDelayMs != 500 ||
             request.FarmComputerExpectedShakeTimer != 500 ||
             request.FarmComputerExpectedFreezeMs != 500 ||
             request.FarmComputerExpectedLocationActionReturn != true ||
             !string.Equals(request.FarmComputerRootLocationId,
                 location.GetRootLocation().NameOrUniqueName, StringComparison.Ordinal) ||
             !string.Equals(request.ItemId, computer.ItemId, StringComparison.Ordinal) ||
             !string.Equals(request.QualifiedItemId, computer.QualifiedItemId, StringComparison.Ordinal) ||
             !string.Equals(request.LocationId, location.NameOrUniqueName, StringComparison.Ordinal) ||
             !string.Equals(request.TargetRuntimeType, typeof(StardewObject).FullName, StringComparison.Ordinal) ||
             !string.Equals(request.InteractionKind, "location_object", StringComparison.Ordinal) ||
             !string.Equals(request.ExpectedActionType, "FarmComputer", StringComparison.Ordinal) ||
             !string.Equals(request.NativeContract, FarmComputerNativeContract, StringComparison.Ordinal)))
        {
            reasons.Add("farm_computer_projection_drifted");
        }
        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    private void TickFarmComputerReport()
    {
        var active = nativeObjectInteractions.FarmComputer;
        if (active is null)
            return;
        if (active.NativeInvoked)
        {
            TickFarmComputerReceipt(active);
            return;
        }

        var movement = AdvanceNativeObjectInteractionMovement(
            active, "farm_computer", out var movementFailure);
        if (movement == NativeObjectMovementStatus.Failed)
        {
            CompleteFarmComputer(active, false, movementFailure);
            return;
        }
        if (movement == NativeObjectMovementStatus.Moving)
            return;

        if (!active.Location.objects.TryGetValue(active.Target.ToVector2(), out var currentComputer) ||
            !ReferenceEquals(currentComputer, active.Computer) ||
            !string.Equals(currentComputer.ItemId, active.BeforeItemId, StringComparison.Ordinal) ||
            !string.Equals(currentComputer.QualifiedItemId, active.BeforeQualifiedItemId, StringComparison.Ordinal))
        {
            CompleteFarmComputer(active, false, "farm_computer_object_replaced_or_drifted_while_moving");
            return;
        }
        var safeItem = Game1.player.Items[active.SafeSlotIndex];
        if ((active.SafeSlotKind == "empty" && safeItem is not null) ||
            (active.SafeSlotKind == "tool" && safeItem is not Tool) ||
            IsDestructiveObjectTrap(active.Location, active.Stand))
        {
            CompleteFarmComputer(active, false, "farm_computer_safe_context_drifted_while_moving");
            return;
        }

        Game1.player.CurrentToolIndex = active.SafeSlotIndex;
        if (Game1.player.ActiveObject is not null)
        {
            CompleteFarmComputer(active, false, "farm_computer_active_object_selection_failed");
            return;
        }
        Game1.player.faceDirection(DirectionTo(Game1.player.TilePoint, active.Target));
        active.NativeHandled = active.Location.checkAction(
            new TileLocation(active.Target.X, active.Target.Y),
            new TileRectangle(Game1.viewport.X, Game1.viewport.Y, Game1.viewport.Width, Game1.viewport.Height),
            Game1.player);
        active.ObservedImmediateShakeTimer = active.Computer.shakeTimer;
        active.ObservedImmediateFreezePause = Game1.player.freezePause;
        active.NativeInvoked = true;
    }

    private void TickFarmComputerReceipt(ActiveFarmComputer active)
    {
        active.ElapsedTicks++;
        if (active.ElapsedTicks > active.MaxTicks)
        {
            CompleteFarmComputer(active, false, "farm_computer_delayed_dialogue_timeout");
            return;
        }
        if (Game1.activeClickableMenu is not DialogueBox dialogue || !Game1.dialogueUp)
            return;

        var reportText = dialogue.dialogues.Count == 1 ? dialogue.dialogues[0] : string.Empty;
        var reportSha256 = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(reportText))).ToLowerInvariant();
        active.ObservedReportSha256 = reportSha256;
        var verified = active.NativeHandled == active.ExpectedLocationActionReturn &&
            active.ObservedImmediateShakeTimer == active.ExpectedShakeTimer &&
            active.ObservedImmediateFreezePause == active.ExpectedFreezeMs &&
            string.Equals(reportSha256, active.ExpectedReportSha256, StringComparison.Ordinal) &&
            active.Location.objects.TryGetValue(active.Target.ToVector2(), out var afterComputer) &&
            ReferenceEquals(afterComputer, active.Computer) &&
            string.Equals(afterComputer.ItemId, active.BeforeItemId, StringComparison.Ordinal) &&
            string.Equals(afterComputer.QualifiedItemId, active.BeforeQualifiedItemId, StringComparison.Ordinal);
        CompleteFarmComputer(active, verified,
            verified ? Array.Empty<string>() : new[] { "farm_computer_native_receipt_mismatch" });
    }

    private void CompleteFarmComputer(ActiveFarmComputer active, bool verified, params string[] reasons)
    {
        StopAllMovement();
        nativeObjectInteractions.FarmComputer = null;
        Game1.player.CurrentToolIndex = active.RestoreSlotIndex;
        var verificationReasons = verified
            ? new[]
            {
                "shared_native_object_interaction_movement_reached_exact_adjacent_stand",
                "safe_toolbar_slot_selected_without_active_object",
                "one_native_GameLocation_checkAction_started_farm_computer_branch",
                "native_500ms_delay_opened_DialogueBox",
                "dialogue_report_sha256_matches_transparent_root_aggregate",
                "canonical_item_identity_unchanged",
                "selected_toolbar_slot_restored",
                "report_left_open_for_explicit_player_command"
            }
            : reasons.Length == 0 ? new[] { "farm_computer_post_state_mismatch" } : reasons;
        active.Pending.Completion.SetResult(new TrainingExecutionResult
        {
            RunId = active.Pending.Request.RunId,
            QueueId = active.Pending.Request.QueueId,
            QueueItemId = active.Pending.Request.QueueItemId,
            BeforeStateHash = active.Pending.Request.BeforeStateHash,
            OptionId = active.Pending.Request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            ActualTicks = active.ElapsedTicks,
            StartedAt = active.StartedAt,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            TrainingImpactScope = "player_command_only_executor_evidence",
            PrimitiveKind = "read_farm_computer_report",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verificationReasons,
            RequestedEffect = FarmComputerRequestedEffect(active.Pending.Request),
            ObservedEffect = "report_sha256=" + active.ObservedReportSha256 +
                ";native_handled=" + active.NativeHandled.ToString().ToLowerInvariant() +
                ";immediate_shake_timer=" + active.ObservedImmediateShakeTimer +
                ";immediate_freeze_pause=" + active.ObservedImmediateFreezePause +
                ";menu_type=" + (Game1.activeClickableMenu?.GetType().Name ?? "none") +
                ";selected_slot=" + Game1.player.CurrentToolIndex,
            BlockReasons = verified ? Array.Empty<string>() : verificationReasons,
            ChangedFacts = new[]
            {
                new SimulatedFactChange
                {
                    Path = "menus.active_menu.type",
                    Before = "none",
                    After = Game1.activeClickableMenu?.GetType().Name ?? "none"
                },
                new SimulatedFactChange
                {
                    Path = "player.current_tool_index",
                    Before = active.RestoreSlotIndex.ToString(),
                    After = Game1.player.CurrentToolIndex.ToString()
                }
            }
        });
    }

    private static TrainingExecutionResult FarmComputerBlocked(
        TrainingExecutionRequest request,
        params string[] reasons) =>
        BlockedWithPrimitive(request, "read_farm_computer_report",
            FarmComputerRequestedEffect(request), "farm_computer_current_state=" +
            FarmComputerCurrentObserved(request), reasons);

    private static string FarmComputerRequestedEffect(TrainingExecutionRequest request) =>
        "native_dialogue=FarmComputer;report_sha256=" + request.FarmComputerReportSha256 +
        ";structured_information_already_transparent=true;selected_slot_restored=true";

    private static string FarmComputerCurrentObserved(TrainingExecutionRequest request)
    {
        if (!request.TargetTileX.HasValue || !request.TargetTileY.HasValue ||
            Game1.currentLocation is null || !Game1.currentLocation.objects.TryGetValue(
                new Vector2(request.TargetTileX.Value, request.TargetTileY.Value), out var item))
        {
            return "missing";
        }
        return item.shakeTimer + ":" + item.ItemId + ":" + item.QualifiedItemId;
    }

    private sealed class ActiveFarmComputer : INativeObjectInteractionMovement
    {
        public ActiveFarmComputer(
            PendingExecution pending,
            GameLocation location,
            StardewObject computer,
            Point target,
            Point stand,
            List<Point> path,
            int maxMovementTiles)
        {
            Pending = pending;
            Location = location;
            Computer = computer;
            Target = target;
            Stand = stand;
            Path = path;
            MaxMovementTiles = maxMovementTiles;
            SafeSlotIndex = pending.Request.SafeSlotIndex!.Value;
            SafeSlotKind = pending.Request.FarmComputerSafeSlotKind;
            RestoreSlotIndex = pending.Request.RestoreSlotIndex!.Value;
            BeforeItemId = computer.ItemId;
            BeforeQualifiedItemId = computer.QualifiedItemId;
            ExpectedReportSha256 = pending.Request.FarmComputerReportSha256;
            ExpectedShakeTimer = pending.Request.FarmComputerExpectedShakeTimer!.Value;
            ExpectedFreezeMs = pending.Request.FarmComputerExpectedFreezeMs!.Value;
            ExpectedLocationActionReturn = pending.Request.FarmComputerExpectedLocationActionReturn!.Value;
            LastPosition = Game1.player.Position;
            LastObservedTile = Game1.player.TilePoint;
        }

        public PendingExecution Pending { get; }
        public GameLocation Location { get; }
        public StardewObject Computer { get; }
        public Point Target { get; }
        public Point Stand { get; }
        public List<Point> Path { get; }
        public int MaxMovementTiles { get; }
        public int SafeSlotIndex { get; }
        public string SafeSlotKind { get; }
        public int RestoreSlotIndex { get; }
        public string BeforeItemId { get; }
        public string BeforeQualifiedItemId { get; }
        public string ExpectedReportSha256 { get; }
        public int ExpectedShakeTimer { get; }
        public int ExpectedFreezeMs { get; }
        public bool ExpectedLocationActionReturn { get; }
        public string StartedAt { get; } = DateTimeOffset.UtcNow.ToString("O");
        public int MaxTicks { get; } = 3600;
        public int ElapsedTicks { get; set; }
        public int PathIndex { get; set; }
        public int StuckTicks { get; set; }
        public int MovementTiles { get; set; }
        public Vector2 LastPosition { get; set; }
        public Point LastObservedTile { get; set; }
        public bool NativeInvoked { get; set; }
        public bool NativeHandled { get; set; }
        public int ObservedImmediateShakeTimer { get; set; }
        public int ObservedImmediateFreezePause { get; set; }
        public string ObservedReportSha256 { get; set; } = string.Empty;
    }
}
