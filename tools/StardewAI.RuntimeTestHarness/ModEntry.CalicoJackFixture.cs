using System.Globalization;
using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Locations;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private TrainingExecutionResult ExecuteSetupCalicoJackFixture(TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
            return Blocked(request, reasons.ToArray());

        Game1.exitActiveMenu();
        Game1.currentMinigame = null;
        Game1.dialogueUp = false;
        Game1.eventUp = false;
        var club = Game1.getLocationFromName("Club") as Club;
        var bet = request.CalicoBet == 1000 || request.CalicoFixtureCase == "high_stakes" ? 1000 : 100;
        if (club is null || !TryFindCalicoJackFixtureEndpoint(club, bet, out var interaction, out var stand, out var action))
        {
            return BlockedWithPrimitive(request, "debug_setup_calico_jack",
                "location=Club;calico_jack_bet=" + bet,
                "location=" + (club?.NameOrUniqueName ?? "missing"),
                "calico_jack_fixture_endpoint_unavailable");
        }

        Game1.currentLocation = club;
        Game1.player.currentLocation = club;
        Game1.player.Position = stand.ToVector2() * Game1.tileSize;
        Game1.player.UsingTool = false;
        Game1.player.forceCanMove();
        Game1.player.hasClubCard = true;
        Game1.player.clubCoins = Math.Max(request.CalicoClubCoinsBefore ?? (bet == 1000 ? 2000 : 1000), bet);
        Game1.player.craftingRecipes.Remove("Deluxe Scarecrow");
        Game1.player.mailReceived.Remove("RarecrowSociety");
        Game1.player.mailForTomorrow.Remove("RarecrowSociety");
        if (request.CalicoTimesPlayedSeed is > 0)
            Club.timesPlayedCalicoJack = request.CalicoTimesPlayedSeed.Value - 1;
        for (var slot = 0; slot < Game1.player.Items.Count; slot++)
        {
            if (Game1.player.Items[slot]?.QualifiedItemId == "(BC)126")
                Game1.player.Items[slot] = null;
        }

        var verified = ReferenceEquals(Game1.currentLocation, club) &&
            ReferenceEquals(Game1.player.currentLocation, club) &&
            Game1.player.TilePoint == stand && Game1.player.hasClubCard &&
            Game1.player.clubCoins >= bet && !Game1.player.craftingRecipes.ContainsKey("Deluxe Scarecrow") &&
            !Game1.player.hasOrWillReceiveMail("RarecrowSociety") &&
            !Utility.doesItemExistAnywhere("(BC)126");
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
            PrimitiveKind = "debug_setup_calico_jack",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[]
                {
                    "native_Club_location_and_table_action_present",
                    "club_card_and_seed_coin_demand_ready",
                    "missing_(BC)126_Deluxe_Scarecrow_dependency_ready"
                }
                : new[] { "calico_jack_fixture_post_state_mismatch" },
            RequestedEffect = "location=Club;calico_jack_bet=" + bet + ";missing_target=(BC)126",
            ObservedEffect = "location=" + Game1.currentLocation.NameOrUniqueName +
                ";interaction=" + interaction.X + "," + interaction.Y +
                ";stand=" + stand.X + "," + stand.Y +
                ";action=" + action +
                ";club_coins=" + Game1.player.clubCoins.ToString(CultureInfo.InvariantCulture) +
                ";times_played=" + Club.timesPlayedCalicoJack.ToString(CultureInfo.InvariantCulture),
            BlockReasons = verified ? Array.Empty<string>() : new[] { "calico_jack_fixture_post_state_mismatch" }
        };
    }

    private static bool TryFindCalicoJackFixtureEndpoint(
        Club club,
        int bet,
        out Point interaction,
        out Point stand,
        out string action)
    {
        var layer = club.Map?.GetLayer("Buildings");
        if (layer is not null)
        {
            for (var y = 0; y < layer.LayerHeight; y++)
            for (var x = 0; x < layer.LayerWidth; x++)
            {
                var candidateAction = club.doesTileHaveProperty(x, y, "Action", "Buildings");
                if (string.IsNullOrWhiteSpace(candidateAction))
                    continue;
                var parts = candidateAction.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0 || parts[0] is not ("ClubCards" or "BlackJack"))
                    continue;
                var candidateBet = parts.Length > 1 && parts[1] == "1000" ? 1000 : 100;
                if (candidateBet != bet)
                    continue;
                var target = new Point(x, y);
                foreach (var candidateStand in Neighbors(target))
                {
                    if (!IsTileOnMap(club, candidateStand) || !IsTileWalkable(club, candidateStand) ||
                        IsTileOccupiedByCharacter(club, candidateStand))
                        continue;
                    interaction = target;
                    stand = candidateStand;
                    action = candidateAction;
                    return true;
                }
            }
        }
        interaction = Point.Zero;
        stand = Point.Zero;
        action = string.Empty;
        return false;
    }
}
