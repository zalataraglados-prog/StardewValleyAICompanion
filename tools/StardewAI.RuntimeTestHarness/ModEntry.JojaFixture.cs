using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Locations;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private static readonly string[] JojaFixtureMailIds =
    {
        "JojaMember", "JojaGreeting", "ccIsComplete", "ccBulletin",
        "ccVault", "ccBoilerRoom", "ccCraftsRoom", "ccPantry", "ccFishTank",
        "jojaVault", "jojaBoilerRoom", "jojaCraftsRoom", "jojaPantry", "jojaFishTank"
    };

    private TrainingExecutionResult ExecuteSetupJojaDevelopmentFixture(TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            return Blocked(request, reasons.ToArray());
        }

        var fixture = request.JojaFixtureCase switch
        {
            "membership_without_greeting" => new JojaFixture(false, false, string.Empty),
            "membership_with_greeting" => new JojaFixture(false, true, string.Empty),
            "project_vault" => new JojaFixture(true, true, "vault"),
            "project_boiler_room" => new JojaFixture(true, true, "boiler_room"),
            "project_crafts_room" => new JojaFixture(true, true, "crafts_room"),
            "project_pantry" => new JojaFixture(true, true, "pantry"),
            "project_fish_tank" => new JojaFixture(true, true, "fish_tank"),
            _ => null
        };
        if (fixture is null)
        {
            return BlockedWithPrimitive(request, "debug_setup_joja_development",
                "joja_fixture=ready", "fixture_case=" + request.JojaFixtureCase,
                "joja_fixture_case_invalid");
        }
        if (Game1.getLocationFromName("JojaMart") is not JojaMart mart)
        {
            return BlockedWithPrimitive(request, "debug_setup_joja_development",
                "joja_fixture=ready", "location=missing", "joja_fixture_location_missing");
        }

        var actionTile = FindJojaActionTile(mart);
        var standTile = actionTile.HasValue ? FindJojaStandTile(mart, actionTile.Value) : null;
        if (!actionTile.HasValue || !standTile.HasValue)
        {
            return BlockedWithPrimitive(request, "debug_setup_joja_development",
                "joja_fixture=ready", "join_action_or_stand=missing",
                "joja_fixture_join_action_or_stand_missing");
        }

        foreach (var farmer in Game1.getAllFarmers())
        {
            foreach (var mailId in JojaFixtureMailIds)
            {
                RemoveJojaFixtureMail(farmer, mailId);
            }
            farmer.eventsSeen.Remove("502261");
        }

        var player = Game1.player;
        Game1.currentLocation = mart;
        player.currentLocation = mart;
        mart.resetForPlayerEntry();
        player.eventsSeen.Add("611439");
        if (fixture.MembershipReceived)
        {
            Game1.MasterPlayer.mailReceived.Add("JojaMember");
            player.mailReceived.Add("JojaMember");
        }
        if (fixture.GreetingReceived)
        {
            player.mailReceived.Add("JojaGreeting");
        }

        player.Money = 100000;
        player.activeDialogueEvents.Remove("joja_Begin");
        Game1.activeClickableMenu = null;
        Game1.dialogueUp = false;
        Game1.eventUp = false;
        Game1.eventOver = false;
        Game1.timeOfDay = 1200;
        player.UsingTool = false;
        player.canMove = true;
        player.Position = standTile.Value.ToVector2() * Game1.tileSize;
        player.faceDirection(DirectionTo(standTile.Value, actionTile.Value));

        var route = Game1.MasterPlayer.hasOrWillReceiveMail("JojaMember")
            ? "joja_locked"
            : "undecided";
        var verified = ReferenceEquals(Game1.currentLocation, mart) &&
            JojaMart.Morris is not null &&
            player.TilePoint == standTile.Value &&
            AreAdjacent(standTile.Value, actionTile.Value) &&
            mart.doesTileHaveProperty(actionTile.Value.X, actionTile.Value.Y, "Action", "Buildings") == "JoinJoja" &&
            player.Money == 100000 &&
            player.eventsSeen.Contains("611439") &&
            player.mailReceived.Contains("JojaGreeting") == fixture.GreetingReceived &&
            player.mailReceived.Contains("JojaMember") == fixture.MembershipReceived &&
            route == (fixture.MembershipReceived ? "joja_locked" : "undecided") &&
            !JojaProjectOrderPending();
        var observed = "case=" + request.JojaFixtureCase +
            ";route=" + route +
            ";location=" + mart.NameOrUniqueName +
            ";action_tile=" + actionTile.Value.X + "," + actionTile.Value.Y +
            ";stand_tile=" + standTile.Value.X + "," + standTile.Value.Y +
            ";money=" + player.Money +
            ";greeting=" + fixture.GreetingReceived.ToString().ToLowerInvariant() +
            ";membership=" + fixture.MembershipReceived.ToString().ToLowerInvariant() +
            ";morris_ready=" + (JojaMart.Morris is not null).ToString().ToLowerInvariant() +
            ";project=" + fixture.ProjectId;

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
            PrimitiveKind = "debug_setup_joja_development",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[] { "isolated_joja_fixture_ready", "exact_JoinJoja_action_and_stand_ready" }
                : new[] { "joja_fixture_postcondition_mismatch" },
            RequestedEffect = "joja_fixture=ready",
            ObservedEffect = observed,
            BlockReasons = verified ? Array.Empty<string>() : new[] { "joja_fixture_postcondition_mismatch" }
        };
    }

    private TrainingExecutionResult ExecutePrepareJojaSettlementSleep(TrainingExecutionRequest request)
    {
        return ExecutePrepareNativeSleepFixture(
            request,
            "debug_prepare_joja_settlement_sleep",
            "isolated_joja_fixture_farmer_moved_to_native_sleep_stand");
    }

    private static Point? FindJojaActionTile(JojaMart mart)
    {
        var layer = mart.Map?.Layers.FirstOrDefault();
        if (layer is null)
        {
            return null;
        }
        for (var y = 0; y < layer.LayerHeight; y++)
        {
            for (var x = 0; x < layer.LayerWidth; x++)
            {
                if (mart.doesTileHaveProperty(x, y, "Action", "Buildings") == "JoinJoja")
                {
                    return new Point(x, y);
                }
            }
        }
        return null;
    }

    private static Point? FindJojaStandTile(JojaMart mart, Point actionTile)
    {
        foreach (var tile in Neighbors(actionTile))
        {
            if (IsTileOnMap(mart, tile) &&
                IsTileWalkable(mart, tile) &&
                !IsTileOccupiedByCharacter(mart, tile))
            {
                return tile;
            }
        }
        return null;
    }

    private static void RemoveJojaFixtureMail(Farmer farmer, string mailId)
    {
        farmer.mailReceived.Remove(mailId);
        farmer.mailForTomorrow.Remove(mailId);
        farmer.mailForTomorrow.Remove(mailId + "%&NL&%");
    }

    private sealed record JojaFixture(bool MembershipReceived, bool GreetingReceived, string ProjectId);
}
