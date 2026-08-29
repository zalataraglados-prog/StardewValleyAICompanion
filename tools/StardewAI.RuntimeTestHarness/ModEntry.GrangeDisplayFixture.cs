using StardewAI.Contracts.Training;
using StardewModdingAPI;
using StardewValley;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private static readonly string[] GrangeFixtureItemIds =
        { "276", "613", "440", "72", "163", "421", "220", "348", "74" };

    private sealed class ActiveGrangeFixture
    {
        public ActiveGrangeFixture(PendingExecution pending, StardewValley.Event festival, bool judged)
        {
            Pending = pending;
            Festival = festival;
            Judged = judged;
            StartedAt = DateTimeOffset.UtcNow.ToString("O");
        }

        public PendingExecution Pending { get; }
        public StardewValley.Event Festival { get; }
        public bool Judged { get; }
        public string StartedAt { get; }
        public int ElapsedTicks { get; set; }
    }

    private ActiveGrangeFixture? activeGrangeFixture;

    private void StartSetupGrangeDisplayFixture(PendingExecution pending)
    {
        var request = pending.Request;
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            pending.Completion.SetResult(Blocked(request, reasons.ToArray()));
            return;
        }
        Game1.exitActiveMenu();
        if (Game1.player.team.grangeMutex.IsLockHeld())
            Game1.player.team.grangeMutex.ReleaseLock();
        Game1.eventUp = false;
        Game1.eventOver = false;
        Game1.dialogueUp = false;
        Game1.Date.Season = Season.Fall;
        Game1.Date.DayOfMonth = 16;
        Game1.season = Season.Fall;
        Game1.currentSeason = "fall";
        Game1.dayOfMonth = 16;
        Game1.timeOfDay = 900;
        var town = Game1.getLocationFromName("Town");
        if (town is null)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "debug_setup_grange_display",
                "festival=fall16", "town=missing", "grange_fixture_town_unavailable"));
            return;
        }
        Game1.currentLocation = town;
        Game1.player.currentLocation = town;
        Game1.player.Position = new Microsoft.Xna.Framework.Vector2(36, 58) * Game1.tileSize;
        Game1.player.UsingTool = false;
        Game1.player.forceCanMove();
        town.currentEvent = null;
        if (!StardewValley.Event.tryToLoadFestival("fall16", out var festival) || festival is null)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "debug_setup_grange_display",
                "festival=fall16", "festival=load_failed", "grange_fixture_festival_load_failed"));
            return;
        }
        town.startEvent(festival);
        if (!ReferenceEquals(town.currentEvent, festival) || !Game1.eventUp)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "debug_setup_grange_display",
                "festival=fall16", "festival=start_rejected", "grange_fixture_festival_start_rejected"));
            return;
        }
        activeGrangeFixture = new ActiveGrangeFixture(pending, festival, request.GrangeJudged == true);
    }

    private void TickSetupGrangeDisplayFixture()
    {
        var active = activeGrangeFixture;
        if (active is null)
            return;
        if (++active.ElapsedTicks > 600 || !Context.IsWorldReady || Game1.eventOver)
        {
            BlockSetupGrangeDisplayFixture(active, "grange_fixture_event_setup_timeout_or_finished");
            return;
        }
        var location = Game1.currentLocation;
        if (location is null || !ReferenceEquals(location.currentEvent, active.Festival))
            return;
        if (!active.Festival.playerControlSequence)
            return;

        var interactionCount = 0;
        Microsoft.Xna.Framework.Point? fixtureInteraction = null;
        Microsoft.Xna.Framework.Point? fixtureStand = null;
        if (location.Map?.Layers.Count > 0)
        {
            var layer = location.Map.Layers[0];
            for (var y = 0; y < layer.LayerHeight; y++)
            for (var x = 0; x < layer.LayerWidth; x++)
            {
                if (location.getTileIndexAt(x, y, "Buildings", "untitled tile sheet") is not (349 or 350 or 351))
                    continue;
                interactionCount++;
                if (fixtureStand.HasValue)
                    continue;
                var action = new Microsoft.Xna.Framework.Point(x, y);
                foreach (var stand in new[]
                {
                    new Microsoft.Xna.Framework.Point(x + 1, y),
                    new Microsoft.Xna.Framework.Point(x - 1, y),
                    new Microsoft.Xna.Framework.Point(x, y + 1),
                    new Microsoft.Xna.Framework.Point(x, y - 1)
                })
                {
                    if (IsTileOnMap(location, stand) && IsTileWalkable(location, stand) &&
                        !IsTileOccupiedByCharacter(location, stand))
                    {
                        fixtureInteraction = action;
                        fixtureStand = stand;
                        break;
                    }
                }
            }
        }
        if (interactionCount == 0 || !fixtureInteraction.HasValue || !fixtureStand.HasValue)
            return;

        Game1.player.team.grangeDisplay.Clear();
        for (var slot = 0; slot < 9; slot++)
            Game1.player.team.grangeDisplay.Add(null);
        for (var slot = 0; slot < GrangeFixtureItemIds.Length; slot++)
        {
            var item = ItemRegistry.Create<StardewValley.Object>("(O)" + GrangeFixtureItemIds[slot]);
            item.Stack = 2;
            item.Quality = slot < 6 ? 4 : 2;
            Game1.player.Items[slot] = item;
        }
        active.Festival.grangeJudged = active.Judged;
        if (active.Judged)
        {
            var item = Game1.player.Items[0]!.getOne();
            item.Stack = 1;
            Game1.player.Items[0]!.Stack--;
            Game1.player.team.grangeDisplay[0] = item;
        }
        Game1.player.Position = fixtureStand.Value.ToVector2() * Game1.tileSize;
        Game1.player.currentLocation = location;
        Game1.player.forceCanMove();
        var verified = ReferenceEquals(location.currentEvent, active.Festival) &&
            active.Festival.id == "festival_fall16" && interactionCount > 0 &&
            Game1.player.TilePoint == fixtureStand.Value && Game1.player.team.grangeDisplay.Count == 9 &&
            Game1.player.Items.Take(9).All(item => item is StardewValley.Object obj && obj.Stack >= 1);
        activeGrangeFixture = null;
        active.Pending.Completion.SetResult(new TrainingExecutionResult
        {
            RunId = active.Pending.Request.RunId, QueueId = active.Pending.Request.QueueId,
            QueueItemId = active.Pending.Request.QueueItemId,
            BeforeStateHash = active.Pending.Request.BeforeStateHash, OptionId = active.Pending.Request.OptionId,
            Status = verified ? "applied" : "blocked", FeedbackAvailable = true,
            StartedAt = active.StartedAt, CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "debug_setup_grange_display",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[] { "real_festival_fall16_event_started", "native_change_to_temporary_town_fair_completed", "native_grange_interaction_tiles_present", "nine_live_scoring_inventory_rows_ready", active.Judged ? "judged_retrieval_fixture_ready" : "pre_judging_fixture_ready" }
                : new[] { "grange_fixture_post_state_mismatch" },
            RequestedEffect = "festival=fall16;judged=" + active.Judged.ToString().ToLowerInvariant(),
            ObservedEffect = "festival=" + (location.currentEvent?.id ?? "none") + ";location=" + location.NameOrUniqueName +
                ";map=" + location.mapPath.Value + ";interaction_tiles=" + interactionCount +
                ";interaction=" + fixtureInteraction?.X + "," + fixtureInteraction?.Y +
                ";stand=" + fixtureStand?.X + "," + fixtureStand?.Y +
                ";display_count=" + Game1.player.team.grangeDisplay.Count,
            BlockReasons = verified ? Array.Empty<string>() : new[] { "grange_fixture_post_state_mismatch" }
        });
    }

    private void BlockSetupGrangeDisplayFixture(ActiveGrangeFixture active, string reason)
    {
        activeGrangeFixture = null;
        active.Pending.Completion.SetResult(BlockedWithPrimitive(active.Pending.Request,
            "debug_setup_grange_display", "festival=fall16;judged=" + active.Judged.ToString().ToLowerInvariant(),
            "festival=" + (Game1.currentLocation?.currentEvent?.id ?? "none") +
            ";location=" + (Game1.currentLocation?.NameOrUniqueName ?? "none") +
            ";current_command=" + active.Festival.CurrentCommand, reason));
    }
}
