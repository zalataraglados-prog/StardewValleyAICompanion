using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.BellsAndWhistles;
using StardewValley.Locations;
using StardewValley.Menus;
using TileLocation = xTile.Dimensions.Location;
using TileRectangle = xTile.Dimensions.Rectangle;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private void StartGingerIslandInteraction(PendingExecution pending)
    {
        var request = pending.Request;
        var reasons = ValidateExecutionRequest(request);
        if (!request.TargetTileX.HasValue || !request.TargetTileY.HasValue)
            reasons.Add("interact_target_tile_required");
        if (request.InteractionKind is not ("boat_tunnel_check_action" or "parrot_upgrade_perch"))
            reasons.Add("ginger_island_interaction_kind_unsupported");
        var target = request.TargetTileX.HasValue && request.TargetTileY.HasValue
            ? new Point(request.TargetTileX.Value, request.TargetTileY.Value)
            : Point.Zero;
        if (!AreAdjacent(Game1.player.TilePoint, target))
            reasons.Add("interact_target_not_adjacent");
        if (Game1.activeClickableMenu is not null)
            reasons.Add("interact_menu_must_be_clear");

        var expectedLocation = GingerIslandExpectedLocation(request.ExpectedActionType);
        if (string.IsNullOrWhiteSpace(expectedLocation) ||
            !string.Equals(Game1.currentLocation?.NameOrUniqueName, expectedLocation, StringComparison.Ordinal))
            reasons.Add("ginger_island_location_mismatch");
        var expectedMail = GingerIslandExpectedMail(request.ExpectedActionType);
        if (!GingerIslandLiveIdentityMatches(request, target))
            reasons.Add("ginger_island_live_identity_mismatch");
        if (GingerIslandProgressComplete(request.ExpectedActionType, expectedMail))
            reasons.Add("ginger_island_progress_already_complete");

        if (reasons.Count > 0 || Game1.currentLocation is null)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(
                request,
                "ginger_island_native_interaction",
                InteractRequestedEffect(request),
                InteractObservedEffect(),
                reasons.Distinct(StringComparer.Ordinal).ToArray()));
            return;
        }

        var resourceId = GingerIslandResourceId(request.ExpectedActionType);
        var active = new ActiveGingerIslandInteraction(
            pending,
            Game1.currentLocation,
            target,
            expectedMail,
            resourceId,
            string.IsNullOrWhiteSpace(resourceId) ? 0 : Game1.player.Items.CountId(resourceId),
            Game1.player.Money,
            Game1.netWorldState.Value.GoldenWalnuts);
        Game1.player.faceDirection(DirectionTo(Game1.player.TilePoint, target));
        active.Handled = Game1.currentLocation.checkAction(
            new TileLocation(target.X, target.Y),
            new TileRectangle(Game1.viewport.X, Game1.viewport.Y, Game1.viewport.Width, Game1.viewport.Height),
            Game1.player);
        if (!active.Handled)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(
                request,
                "ginger_island_native_interaction",
                InteractRequestedEffect(request),
                InteractObservedEffect() + ";handled=false",
                "ginger_island_native_check_action_not_handled"));
            return;
        }
        activeGingerIslandInteraction = active;
    }

    private void TickGingerIslandInteraction()
    {
        var active = activeGingerIslandInteraction;
        if (active is null)
            return;
        active.ElapsedTicks++;
        try
        {
            if (!ReferenceEquals(Game1.currentLocation, active.Location) &&
                !GingerIslandTicketTransitionObserved(active))
            {
                CompleteGingerIslandInteraction(active, false, "ginger_island_location_changed_before_transition");
                return;
            }

            if (!active.PromptAnswered)
            {
                if (Game1.activeClickableMenu is DialogueBox dialogue && dialogue.isQuestion)
                {
                    var expectedKey = GingerIslandExpectedQuestionKey(active.Pending.Request.ExpectedActionType);
                    if (!string.Equals(active.Location.lastQuestionKey, expectedKey, StringComparison.Ordinal))
                    {
                        CompleteGingerIslandInteraction(active, false, "ginger_island_dialogue_key_mismatch");
                        return;
                    }
                    var yes = dialogue.responses?.FirstOrDefault(response =>
                        string.Equals(response.responseKey, "Yes", StringComparison.Ordinal));
                    if (yes is null || !active.Location.answerDialogue(yes))
                    {
                        CompleteGingerIslandInteraction(active, false, "ginger_island_yes_response_failed");
                        return;
                    }
                    active.PromptAnswered = true;
                    return;
                }
                if (GingerIslandTransitionObserved(active))
                {
                    active.PromptAnswered = true;
                }
                else if (active.ElapsedTicks > 240)
                {
                    CompleteGingerIslandInteraction(active, false, "ginger_island_native_prompt_timeout");
                    return;
                }
            }

            if (GingerIslandTransitionObserved(active))
            {
                CompleteGingerIslandInteraction(active, true);
                return;
            }
            if (active.ElapsedTicks > 1800)
                CompleteGingerIslandInteraction(active, false, "ginger_island_native_settlement_timeout");
        }
        catch (Exception ex)
        {
            CompleteGingerIslandInteraction(active, false,
                "ginger_island_native_interaction_exception:" + ex.GetType().Name + ":" + ex.Message);
        }
    }

    private void CompleteGingerIslandInteraction(
        ActiveGingerIslandInteraction active,
        bool verified,
        params string[] reasons)
    {
        activeGingerIslandInteraction = null;
        var request = active.Pending.Request;
        var resourceAfter = string.IsNullOrWhiteSpace(active.ResourceId)
            ? 0
            : Game1.player.Items.CountId(active.ResourceId);
        var verificationReasons = verified
            ? new[] { "native_check_action_handled", "native_yes_response_applied", "ginger_island_postcondition_observed" }
            : reasons.Length > 0 ? reasons : new[] { "ginger_island_transition_not_observed" };
        active.Pending.Completion.SetResult(new TrainingExecutionResult
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
            PrimitiveKind = "ginger_island_native_interaction",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verificationReasons,
            RequestedEffect = InteractRequestedEffect(request),
            ObservedEffect = InteractObservedEffect() +
                ";handled=" + active.Handled.ToString().ToLowerInvariant() +
                ";expected_mail=" + active.ExpectedMail +
                ";resource_before=" + active.ResourceBefore +
                ";resource_after=" + resourceAfter +
                ";money_before=" + active.MoneyBefore +
                ";money_after=" + Game1.player.Money +
                ";walnuts_before=" + active.WalnutsBefore +
                ";walnuts_after=" + Game1.netWorldState.Value.GoldenWalnuts,
            BlockReasons = verified ? Array.Empty<string>() : verificationReasons,
            ChangedFacts = new[]
            {
                new SimulatedFactChange
                {
                    Path = "ginger_island." + request.ExpectedActionType,
                    Before = "pending",
                    After = verified ? "native_transition_observed" : "unchanged"
                }
            }
        });
    }

    private static bool GingerIslandTransitionObserved(ActiveGingerIslandInteraction active)
    {
        var type = active.Pending.Request.ExpectedActionType;
        if (type == "WillyBoatTicket")
            return GingerIslandTicketTransitionObserved(active);
        if (type.StartsWith("WillyBoatDonate", StringComparison.Ordinal))
        {
            var resourceAfter = Game1.player.Items.CountId(active.ResourceId);
            var expectedDelta = GingerIslandRequiredResourceCount(type);
            return Game1.MasterPlayer.hasOrWillReceiveMail(active.ExpectedMail) &&
                active.ResourceBefore - resourceAfter == expectedDelta;
        }
        var walnutCost = GingerIslandRequiredWalnuts(type);
        return GingerIslandProgressComplete(type, active.ExpectedMail) &&
            active.WalnutsBefore - Game1.netWorldState.Value.GoldenWalnuts == walnutCost;
    }

    private static bool GingerIslandTicketTransitionObserved(ActiveGingerIslandInteraction active) =>
        active.Pending.Request.ExpectedActionType == "WillyBoatTicket" &&
        active.MoneyBefore - Game1.player.Money == 1000 &&
        (Game1.eventUp || Game1.currentMinigame?.GetType().FullName == "StardewValley.Minigames.BoatJourney" ||
         Game1.currentLocation?.NameOrUniqueName.StartsWith("Island", StringComparison.Ordinal) == true);

    private static bool GingerIslandLiveIdentityMatches(TrainingExecutionRequest request, Point target)
    {
        if (request.ExpectedActionType is "WillyBoatDonateBatteries" or "WillyBoatTicket")
            return Game1.currentLocation is BoatTunnel &&
                Game1.currentLocation.doesTileHaveProperty(target.X, target.Y, "Action", "Buildings") == "BoatTicket";
        if (request.ExpectedActionType == "WillyBoatDonateHardwood")
            return Game1.currentLocation is BoatTunnel && target == new Point(6, 8);
        if (request.ExpectedActionType == "WillyBoatDonateIridium")
            return Game1.currentLocation is BoatTunnel && target == new Point(8, 10);
        if (Game1.currentLocation is not IslandLocation island)
            return false;
        var expectedName = request.ExpectedActionType switch
        {
            "IslandFirstParrot" => "Hut",
            "IslandWesternTurtle" => "Turtle",
            "IslandHouse" => "House",
            "IslandResort" => "Resort",
            _ => string.Empty
        };
        return island.parrotUpgradePerches.Count(perch =>
            perch.tilePosition.Value == target &&
            string.Equals(perch.upgradeName.Value, expectedName, StringComparison.Ordinal)) == 1;
    }

    private static bool GingerIslandProgressComplete(string type, string mail) =>
        !string.IsNullOrWhiteSpace(mail) && Game1.MasterPlayer.hasOrWillReceiveMail(mail) ||
        type == "IslandFirstParrot" && (Game1.getLocationFromName("IslandHut") as IslandHut)?.firstParrotDone.Value == true ||
        type == "IslandWesternTurtle" && (Game1.getLocationFromName("IslandSouth") as IslandSouth)?.westernTurtleMoved.Value == true ||
        type == "IslandHouse" && (Game1.getLocationFromName("IslandWest") as IslandWest)?.farmhouseRestored.Value == true ||
        type == "IslandResort" && (Game1.getLocationFromName("IslandSouth") as IslandSouth)?.resortRestored.Value == true;

    private static string GingerIslandExpectedLocation(string type) => type switch
    {
        "WillyBoatDonateBatteries" or "WillyBoatDonateHardwood" or "WillyBoatDonateIridium" or "WillyBoatTicket" => "BoatTunnel",
        "IslandFirstParrot" => "IslandHut",
        "IslandWesternTurtle" or "IslandResort" => "IslandSouth",
        "IslandHouse" => "IslandWest",
        _ => string.Empty
    };

    private static string GingerIslandExpectedQuestionKey(string type) => type switch
    {
        "WillyBoatTicket" => "Boat",
        "WillyBoatDonateBatteries" or "WillyBoatDonateHardwood" or "WillyBoatDonateIridium" => type,
        "IslandFirstParrot" => "UpgradePerch_Hut",
        "IslandWesternTurtle" => "UpgradePerch_Turtle",
        "IslandHouse" => "UpgradePerch_House",
        "IslandResort" => "UpgradePerch_Resort",
        _ => string.Empty
    };

    private static string GingerIslandExpectedMail(string type) => type switch
    {
        "WillyBoatDonateBatteries" => "willyBoatTicketMachine",
        "WillyBoatDonateHardwood" => "willyBoatHull",
        "WillyBoatDonateIridium" => "willyBoatAnchor",
        "IslandFirstParrot" => "Island_FirstParrot",
        "IslandWesternTurtle" => "Island_Turtle",
        "IslandHouse" => "Island_UpgradeHouse",
        "IslandResort" => "Island_Resort",
        _ => string.Empty
    };

    private static string GingerIslandResourceId(string type) => type switch
    {
        "WillyBoatDonateBatteries" => "787",
        "WillyBoatDonateHardwood" => "709",
        "WillyBoatDonateIridium" => "337",
        _ => string.Empty
    };

    private static int GingerIslandRequiredResourceCount(string type) => type switch
    {
        "WillyBoatDonateBatteries" => 5,
        "WillyBoatDonateHardwood" => 200,
        "WillyBoatDonateIridium" => 5,
        _ => 0
    };

    private static int GingerIslandRequiredWalnuts(string type) => type switch
    {
        "IslandFirstParrot" => 1,
        "IslandWesternTurtle" => 10,
        "IslandHouse" or "IslandResort" => 20,
        _ => 0
    };

    private sealed class ActiveGingerIslandInteraction
    {
        public ActiveGingerIslandInteraction(
            PendingExecution pending,
            GameLocation location,
            Point target,
            string expectedMail,
            string resourceId,
            int resourceBefore,
            int moneyBefore,
            int walnutsBefore)
        {
            Pending = pending;
            Location = location;
            Target = target;
            ExpectedMail = expectedMail;
            ResourceId = resourceId;
            ResourceBefore = resourceBefore;
            MoneyBefore = moneyBefore;
            WalnutsBefore = walnutsBefore;
        }

        public PendingExecution Pending { get; }
        public GameLocation Location { get; }
        public Point Target { get; }
        public string ExpectedMail { get; }
        public string ResourceId { get; }
        public int ResourceBefore { get; }
        public int MoneyBefore { get; }
        public int WalnutsBefore { get; }
        public string StartedAt { get; } = DateTimeOffset.UtcNow.ToString("O");
        public bool Handled { get; set; }
        public bool PromptAnswered { get; set; }
        public int ElapsedTicks { get; set; }
    }
}
