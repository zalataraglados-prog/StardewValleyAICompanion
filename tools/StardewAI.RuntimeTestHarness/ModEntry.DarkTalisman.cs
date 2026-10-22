using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Locations;
using TileLocation = xTile.Dimensions.Location;
using TileRectangle = xTile.Dimensions.Rectangle;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private const string DarkTalismanQuestId = "28";
    private static readonly Point DarkTalismanStatueTile = new(54, 35);

    private TrainingExecutionResult ExecuteDarkTalismanKrobusInteraction(TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        var target = request.TargetTileX.HasValue && request.TargetTileY.HasValue
            ? new Point(request.TargetTileX.Value, request.TargetTileY.Value)
            : Point.Zero;
        if (!request.TargetTileX.HasValue || !request.TargetTileY.HasValue)
        {
            reasons.Add("interact_target_tile_required");
        }
        if (!string.Equals(request.InteractionKind, "npc_check_action", StringComparison.Ordinal) ||
            !string.Equals(request.ExpectedActionType, "DarkTalismanKrobus", StringComparison.Ordinal))
        {
            reasons.Add("dark_talisman_krobus_contract_mismatch");
        }
        if (!string.Equals(Game1.currentLocation?.NameOrUniqueName, "Sewer", StringComparison.Ordinal))
        {
            reasons.Add("dark_talisman_krobus_requires_sewer");
        }
        var krobus = Game1.getCharacterFromName("Krobus");
        if (krobus is null || !ReferenceEquals(krobus.currentLocation, Game1.currentLocation) || krobus.TilePoint != target)
        {
            reasons.Add("dark_talisman_krobus_identity_mismatch");
        }
        if (!Game1.player.hasQuest(DarkTalismanQuestId) || Game1.player.mailReceived.Contains("krobusUnseal"))
        {
            reasons.Add("dark_talisman_krobus_progress_precondition_mismatch");
        }
        if (!AreAdjacent(Game1.player.TilePoint, target))
        {
            reasons.Add("interact_target_not_adjacent");
        }
        if (Game1.activeClickableMenu is not null)
        {
            reasons.Add("interact_menu_must_be_clear");
        }

        var questBefore = Game1.player.hasQuest(DarkTalismanQuestId);
        var mailBefore = Game1.player.mailReceived.Contains("krobusUnseal");
        var startedAt = DateTimeOffset.UtcNow.ToString("O");
        if (reasons.Count > 0 || krobus is null)
        {
            return BlockedWithPrimitive(
                request,
                "talk_to_krobus_for_dark_talisman",
                InteractRequestedEffect(request) + ";required_postcondition=quests.mail_received.krobusUnseal=true",
                InteractObservedEffect(),
                reasons.Distinct(StringComparer.Ordinal).ToArray());
        }

        Game1.player.faceDirection(DirectionTo(Game1.player.TilePoint, target));
        var handled = krobus.checkAction(Game1.player, Game1.currentLocation);
        var questAfter = Game1.player.hasQuest(DarkTalismanQuestId);
        var mailAfter = Game1.player.mailReceived.Contains("krobusUnseal");
        var verified = handled && questBefore && !questAfter && !mailBefore && mailAfter;
        var verificationReasons = verified
            ? new[] { "krobus_native_check_action_handled", "quest_28_removed", "krobusUnseal_mail_received" }
            : new[] { handled ? "dark_talisman_krobus_transition_not_observed" : "dark_talisman_krobus_check_action_not_handled" };
        return new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            StartedAt = startedAt,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            ActualTicks = 1,
            PrimitiveKind = "talk_to_krobus_for_dark_talisman",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verificationReasons,
            RequestedEffect = InteractRequestedEffect(request) + ";required_postcondition=quests.mail_received.krobusUnseal=true",
            ObservedEffect = InteractObservedEffect() +
                ";handled=" + handled.ToString().ToLowerInvariant() +
                ";quest_28_active=" + questAfter.ToString().ToLowerInvariant() +
                ";krobusUnseal=" + mailAfter.ToString().ToLowerInvariant(),
            BlockReasons = verified ? Array.Empty<string>() : verificationReasons,
            ChangedFacts = new[]
            {
                new SimulatedFactChange
                {
                    Path = "quests.active.28",
                    Before = questBefore.ToString().ToLowerInvariant(),
                    After = questAfter.ToString().ToLowerInvariant()
                },
                new SimulatedFactChange
                {
                    Path = "quests.mail_received.krobusUnseal",
                    Before = mailBefore.ToString().ToLowerInvariant(),
                    After = mailAfter.ToString().ToLowerInvariant()
                }
            }
        };
    }

    private void StartDarkTalismanStatueInteraction(PendingExecution pending)
    {
        var request = pending.Request;
        var target = request.TargetTileX.HasValue && request.TargetTileY.HasValue
            ? new Point(request.TargetTileX.Value, request.TargetTileY.Value)
            : Point.Zero;
        var reasons = ValidateExecutionRequest(request);
        if (!request.TargetTileX.HasValue || !request.TargetTileY.HasValue)
        {
            reasons.Add("interact_target_tile_required");
        }
        if (!string.Equals(request.InteractionKind, "location_check_action", StringComparison.Ordinal) ||
            !string.Equals(request.ExpectedActionType, "DarkTalismanStatue", StringComparison.Ordinal))
        {
            reasons.Add("dark_talisman_statue_contract_mismatch");
        }
        if (Game1.currentLocation is not Railroad railroad)
        {
            reasons.Add("dark_talisman_statue_requires_railroad");
        }
        else if (target != DarkTalismanStatueTile ||
            railroad.getTileIndexAt(target.X, target.Y, "Buildings", "untitled tile sheet") != 287)
        {
            reasons.Add("dark_talisman_statue_identity_mismatch");
        }
        if (!Game1.player.hasDarkTalisman || DarkTalismanStatueGone())
        {
            reasons.Add("dark_talisman_statue_progress_precondition_mismatch");
        }
        if (!AreAdjacent(Game1.player.TilePoint, target))
        {
            reasons.Add("interact_target_not_adjacent");
        }
        if (Game1.activeClickableMenu is not null)
        {
            reasons.Add("interact_menu_must_be_clear");
        }
        if (reasons.Count > 0 || Game1.currentLocation is not Railroad exactRailroad)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(
                request,
                "remove_dark_talisman_statue",
                InteractRequestedEffect(request) + ";required_postcondition=quests.mail_received.witchStatueGone=true",
                InteractObservedEffect(),
                reasons.Distinct(StringComparer.Ordinal).ToArray()));
            return;
        }

        var goneBefore = DarkTalismanStatueGone();
        Game1.player.faceDirection(DirectionTo(Game1.player.TilePoint, target));
        var handled = exactRailroad.checkAction(
            new TileLocation(target.X, target.Y),
            new TileRectangle(Game1.viewport.X, Game1.viewport.Y, Game1.viewport.Width, Game1.viewport.Height),
            Game1.player);
        if (!handled)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(
                request,
                "remove_dark_talisman_statue",
                InteractRequestedEffect(request),
                InteractObservedEffect() + ";handled=false",
                "dark_talisman_statue_check_action_not_handled"));
            return;
        }
        activeDarkTalismanStatueInteraction = new ActiveDarkTalismanStatueInteraction(
            pending,
            exactRailroad,
            target,
            goneBefore,
            handled);
    }

    private void TickDarkTalismanStatueInteraction()
    {
        var active = activeDarkTalismanStatueInteraction;
        if (active is null)
        {
            return;
        }
        active.ElapsedTicks++;
        var goneAfter = DarkTalismanStatueGone();
        if (goneAfter && Game1.player.freezePause <= 0 && Game1.player.CanMove && !Game1.player.UsingTool)
        {
            activeDarkTalismanStatueInteraction = null;
            active.Pending.Completion.SetResult(BuildDarkTalismanStatueResult(active, verified: true));
            return;
        }
        if (!ReferenceEquals(Game1.currentLocation, active.Railroad))
        {
            activeDarkTalismanStatueInteraction = null;
            active.Pending.Completion.SetResult(BuildDarkTalismanStatueResult(active, verified: false, "dark_talisman_statue_location_changed"));
            return;
        }
        if (active.ElapsedTicks > active.MaxTicks)
        {
            activeDarkTalismanStatueInteraction = null;
            active.Pending.Completion.SetResult(BuildDarkTalismanStatueResult(active, verified: false, "dark_talisman_statue_animation_timeout"));
        }
    }

    private static bool DarkTalismanStatueGone() =>
        Game1.player.mailReceived.Contains("witchStatueGone") ||
        Game1.MasterPlayer.mailReceived.Contains("witchStatueGone") ||
        (Game1.getLocationFromName("Railroad") as Railroad)?.witchStatueGone.Value == true;

    private static TrainingExecutionResult BuildDarkTalismanStatueResult(
        ActiveDarkTalismanStatueInteraction active,
        bool verified,
        params string[] reasons)
    {
        var request = active.Pending.Request;
        var goneAfter = DarkTalismanStatueGone();
        var verificationReasons = verified
            ? new[] { "railroad_native_check_action_handled", "witchStatueGone_transition_observed", "native_freeze_animation_completed" }
            : reasons.Length > 0 ? reasons : new[] { "dark_talisman_statue_transition_not_observed" };
        return new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            StartedAt = active.StartedAt,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            ActualTicks = active.ElapsedTicks,
            PrimitiveKind = "remove_dark_talisman_statue",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verificationReasons,
            RequestedEffect = InteractRequestedEffect(request) + ";required_postcondition=quests.mail_received.witchStatueGone=true",
            ObservedEffect = InteractObservedEffect() +
                ";handled=" + active.Handled.ToString().ToLowerInvariant() +
                ";witchStatueGone=" + goneAfter.ToString().ToLowerInvariant() +
                ";freeze_pause=" + Game1.player.freezePause,
            BlockReasons = verified ? Array.Empty<string>() : verificationReasons,
            ChangedFacts = new[]
            {
                new SimulatedFactChange
                {
                    Path = "quests.mail_received.witchStatueGone",
                    Before = active.GoneBefore.ToString().ToLowerInvariant(),
                    After = goneAfter.ToString().ToLowerInvariant()
                }
            }
        };
    }

    private sealed class ActiveDarkTalismanStatueInteraction
    {
        public ActiveDarkTalismanStatueInteraction(
            PendingExecution pending,
            Railroad railroad,
            Point target,
            bool goneBefore,
            bool handled)
        {
            Pending = pending;
            Railroad = railroad;
            Target = target;
            GoneBefore = goneBefore;
            Handled = handled;
        }

        public PendingExecution Pending { get; }
        public Railroad Railroad { get; }
        public Point Target { get; }
        public bool GoneBefore { get; }
        public bool Handled { get; }
        public string StartedAt { get; } = DateTimeOffset.UtcNow.ToString("O");
        public int ElapsedTicks { get; set; }
        public int MaxTicks { get; } = 600;
    }
}
