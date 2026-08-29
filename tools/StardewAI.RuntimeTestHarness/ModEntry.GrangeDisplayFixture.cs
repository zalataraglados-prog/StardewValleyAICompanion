using StardewAI.Contracts.Training;
using StardewModdingAPI;
using StardewValley;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private static readonly string[] GrangeFixtureItemIds =
        { "276", "613", "440", "72", "163", "421", "220", "348", "74" };

    private enum FallFairFixtureMode
    {
        Grange,
        Fishing,
        Slingshot,
        Strength
    }

    private sealed class ActiveGrangeFixture
    {
        public ActiveGrangeFixture(
            PendingExecution pending,
            StardewValley.Event festival,
            bool judged,
            FallFairFixtureMode mode)
        {
            Pending = pending;
            Festival = festival;
            Judged = judged;
            Mode = mode;
            StartedAt = DateTimeOffset.UtcNow.ToString("O");
        }

        public PendingExecution Pending { get; }
        public StardewValley.Event Festival { get; }
        public bool Judged { get; }
        public FallFairFixtureMode Mode { get; }
        public string StartedAt { get; }
        public int ElapsedTicks { get; set; }
    }

    private ActiveGrangeFixture? activeGrangeFixture;

    private void StartSetupGrangeDisplayFixture(PendingExecution pending)
    {
        StartSetupFallFairFixture(pending, pending.Request.GrangeJudged == true, FallFairFixtureMode.Grange);
    }

    private void StartSetupFairFishingGameFixture(PendingExecution pending)
    {
        StartSetupFallFairFixture(pending, judged: false, FallFairFixtureMode.Fishing);
    }

    private void StartSetupFairSlingshotGameFixture(PendingExecution pending)
    {
        StartSetupFallFairFixture(pending, judged: false, FallFairFixtureMode.Slingshot);
    }

    private void StartSetupFairStrengthGameFixture(PendingExecution pending)
    {
        StartSetupFallFairFixture(pending, judged: true, FallFairFixtureMode.Strength);
    }

    private void StartSetupFallFairFixture(
        PendingExecution pending,
        bool judged,
        FallFairFixtureMode mode)
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
        activeGrangeFixture = new ActiveGrangeFixture(pending, festival, judged, mode);
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
                var tileIndex = location.getTileIndexAt(x, y, "Buildings", "untitled tile sheet");
                var matchesFixture = active.Mode switch
                {
                    FallFairFixtureMode.Grange => tileIndex is 349 or 350 or 351,
                    FallFairFixtureMode.Fishing => tileIndex is 503 or 504,
                    FallFairFixtureMode.Slingshot => tileIndex is 501 or 502,
                    FallFairFixtureMode.Strength => tileIndex == 540,
                    _ => false
                };
                if (!matchesFixture)
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
                    if ((active.Mode != FallFairFixtureMode.Strength || stand.X == 29) &&
                        IsTileOnMap(location, stand) && IsTileWalkable(location, stand) &&
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

        if (active.Mode == FallFairFixtureMode.Grange)
        {
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
        }
        else if (active.Mode == FallFairFixtureMode.Strength)
        {
            active.Festival.grangeJudged = true;
            Game1.player.festivalScore = 1999;
            Game1.player.mailReceived.Remove("CF_Fair");
            Game1.player.mailForTomorrow.Remove("CF_Fair");
        }
        else
        {
            Game1.player.Money = Math.Max(Game1.player.Money, 500);
            Game1.player.festivalScore = 0;
            Game1.player.mailReceived.Remove("CF_Fair");
            Game1.player.mailForTomorrow.Remove("CF_Fair");
        }
        Game1.player.Position = fixtureStand.Value.ToVector2() * Game1.tileSize;
        Game1.player.currentLocation = location;
        Game1.player.forceCanMove();
        var verified = ReferenceEquals(location.currentEvent, active.Festival) &&
            active.Festival.id == "festival_fall16" && interactionCount > 0 &&
            Game1.player.TilePoint == fixtureStand.Value &&
            (active.Mode == FallFairFixtureMode.Strength
                ? active.Festival.grangeJudged && Game1.player.festivalScore == 1999 &&
                  fixtureStand.Value.X == 29 && !Game1.player.hasOrWillReceiveMail("CF_Fair")
                : active.Mode != FallFairFixtureMode.Grange
                ? Game1.player.Money >= 500 && Game1.player.festivalScore == 0 && !Game1.player.hasOrWillReceiveMail("CF_Fair")
                : Game1.player.team.grangeDisplay.Count == 9 &&
                  Game1.player.Items.Take(9).All(item => item is StardewValley.Object obj && obj.Stack >= 1));
        activeGrangeFixture = null;
        active.Pending.Completion.SetResult(new TrainingExecutionResult
        {
            RunId = active.Pending.Request.RunId, QueueId = active.Pending.Request.QueueId,
            QueueItemId = active.Pending.Request.QueueItemId,
            BeforeStateHash = active.Pending.Request.BeforeStateHash, OptionId = active.Pending.Request.OptionId,
            Status = verified ? "applied" : "blocked", FeedbackAvailable = true,
            StartedAt = active.StartedAt, CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = active.Mode switch
            {
                FallFairFixtureMode.Fishing => "debug_setup_fair_fishing_game",
                FallFairFixtureMode.Slingshot => "debug_setup_fair_slingshot_game",
                FallFairFixtureMode.Strength => "debug_setup_fair_strength_game",
                _ => "debug_setup_grange_display"
            },
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? active.Mode switch
                {
                    FallFairFixtureMode.Fishing => new[] { "real_festival_fall16_event_started", "native_change_to_temporary_town_fair_completed", "native_fishing_game_interaction_tiles_present", "unacquired_stardrop_token_demand_and_entry_money_ready" },
                    FallFairFixtureMode.Slingshot => new[] { "real_festival_fall16_event_started", "native_change_to_temporary_town_fair_completed", "native_slingshot_game_interaction_tiles_present", "unacquired_stardrop_token_demand_and_entry_money_ready" },
                    FallFairFixtureMode.Strength => new[] { "real_festival_fall16_event_started", "native_change_to_temporary_town_fair_completed", "native_strength_game_tile_540_and_x29_stand_present", "exact_one_unacquired_stardrop_token_demand_ready" },
                    _ => new[] { "real_festival_fall16_event_started", "native_change_to_temporary_town_fair_completed", "native_grange_interaction_tiles_present", "nine_live_scoring_inventory_rows_ready", active.Judged ? "judged_retrieval_fixture_ready" : "pre_judging_fixture_ready" }
                }
                : new[] { FairFixtureMismatchReason(active.Mode) },
            RequestedEffect = FairFixtureRequestedEffect(active),
            ObservedEffect = "festival=" + (location.currentEvent?.id ?? "none") + ";location=" + location.NameOrUniqueName +
                ";map=" + location.mapPath.Value + ";interaction_tiles=" + interactionCount +
                ";interaction=" + fixtureInteraction?.X + "," + fixtureInteraction?.Y +
                ";stand=" + fixtureStand?.X + "," + fixtureStand?.Y +
                ";display_count=" + Game1.player.team.grangeDisplay.Count +
                ";money=" + Game1.player.Money + ";festival_score=" + Game1.player.festivalScore,
            BlockReasons = verified ? Array.Empty<string>() : new[] { FairFixtureMismatchReason(active.Mode) }
        });
    }

    private void BlockSetupGrangeDisplayFixture(ActiveGrangeFixture active, string reason)
    {
        activeGrangeFixture = null;
        active.Pending.Completion.SetResult(BlockedWithPrimitive(active.Pending.Request,
            active.Mode switch
            {
                FallFairFixtureMode.Fishing => "debug_setup_fair_fishing_game",
                FallFairFixtureMode.Slingshot => "debug_setup_fair_slingshot_game",
                FallFairFixtureMode.Strength => "debug_setup_fair_strength_game",
                _ => "debug_setup_grange_display"
            },
            FairFixtureRequestedEffect(active),
            "festival=" + (Game1.currentLocation?.currentEvent?.id ?? "none") +
            ";location=" + (Game1.currentLocation?.NameOrUniqueName ?? "none") +
            ";current_command=" + active.Festival.CurrentCommand, reason));
    }

    private static string FairFixtureRequestedEffect(ActiveGrangeFixture active) => active.Mode switch
    {
        FallFairFixtureMode.Fishing => "festival=fall16;fair_fishing_game=ready",
        FallFairFixtureMode.Slingshot => "festival=fall16;fair_slingshot_game=ready",
        FallFairFixtureMode.Strength => "festival=fall16;fair_strength_game=ready;festival_score=1999;grange_judged=true",
        _ => "festival=fall16;judged=" + active.Judged.ToString().ToLowerInvariant()
    };

    private static string FairFixtureMismatchReason(FallFairFixtureMode mode) => mode switch
    {
        FallFairFixtureMode.Fishing => "fair_fishing_fixture_post_state_mismatch",
        FallFairFixtureMode.Slingshot => "fair_slingshot_fixture_post_state_mismatch",
        FallFairFixtureMode.Strength => "fair_strength_fixture_post_state_mismatch",
        _ => "grange_fixture_post_state_mismatch"
    };
}
