using System.Reflection;
using Microsoft.Xna.Framework;
using Netcode;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.GameData.HomeRenovations;
using StardewValley.Locations;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private TrainingExecutionResult ExecuteSetupHomeRenovationFixture(TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
            return Blocked(request, reasons.ToArray());

        var player = Game1.player;
        var home = Utility.getHomeOfFarmer(player) as FarmHouse;
        var service = Game1.getLocationFromName("ScienceHouse");
        var action = service is null ? null : FarmhouseFixtureActionTile(service);
        var stand = service is null || !action.HasValue ? null : FarmhouseFixtureStandTile(service, action.Value);
        var robinTile = service is null || !action.HasValue || !stand.HasValue
            ? null
            : FarmhouseFixtureRobinTile(service, action.Value, stand.Value);
        var robin = Game1.getCharacterFromName("Robin");
        var data = DataLoader.HomeRenovations(Game1.content);
        if (home is null || service is null || !action.HasValue || !stand.HasValue || !robinTile.HasValue || robin is null ||
            string.IsNullOrWhiteSpace(request.RenovationId) || !data.TryGetValue(request.RenovationId, out var targetData))
        {
            return BlockedWithPrimitive(request, "debug_setup_home_renovation",
                "home_renovation_fixture=ready", "renovation=" + request.RenovationId,
                "home_renovation_fixture_target_or_service_missing");
        }
        if (data.Count != 18 || HomeRenovationPayloadHash(data) != HomeRenovationPayloadSha256)
        {
            return BlockedWithPrimitive(request, "debug_setup_home_renovation",
                "home_renovation_fixture=ready", "catalog_count=" + data.Count,
                "home_renovation_fixture_data_contract_drifted");
        }

        var selectedIndex = request.RenovationSelectedIndex ?? 0;
        var refundEligible = targetData.Price < 0 && request.RenovationRefundEligible != false;
        var firstPurchaseMail = "FirstPurchase_" +
            (string.IsNullOrWhiteSpace(targetData.RoomId) ? request.RenovationId : targetData.RoomId);
        var beforeLevel = player.HouseUpgradeLevel;
        var beforeMoney = player.Money;
        var beforeLocation = Game1.currentLocation?.NameOrUniqueName ?? string.Empty;
        var beforeFirstPurchase = player.mailReceived.Contains(firstPurchaseMail);

        Game1.activeClickableMenu = null;
        Game1.dialogueUp = false;
        Game1.eventUp = false;
        Game1.eventOver = false;
        player.UsingTool = false;
        player.canMove = true;
        player.HouseUpgradeLevel = 2;
        player.daysUntilHouseUpgrade.Value = -1;
        player.Money = Math.Max(1_000_000, Math.Max(0, targetData.Price) + 10_000);

        foreach (var requirement in targetData.Requirements ?? new List<RenovationValue>())
        {
            if (!SetHomeRenovationRequirement(home, player, requirement, out var requirementReason))
            {
                return BlockedWithPrimitive(request, "debug_setup_home_renovation",
                    "home_renovation_fixture=ready", "renovation=" + request.RenovationId,
                    requirementReason);
            }
        }
        SetHomeRenovationMail(player, firstPurchaseMail, refundEligible);

        home.setMapForUpgradeLevel(2);
        home.UpdateForRenovation();

        Game1.currentLocation = service;
        player.currentLocation = service;
        player.Position = stand.Value.ToVector2() * Game1.tileSize;
        robin.currentLocation?.characters.Remove(robin);
        if (!service.characters.Contains(robin))
            service.characters.Add(robin);
        robin.currentLocation = service;
        robin.Position = robinTile.Value.ToVector2() * Game1.tileSize;

        var available = HouseRenovation.GetAvailableRenovations().OfType<HouseRenovation>().ToArray();
        var renovation = available.FirstOrDefault(value => value.Name == request.RenovationId);
        if (renovation is null || selectedIndex < 0 || selectedIndex >= renovation.renovationBounds.Count)
        {
            return BlockedWithPrimitive(request, "debug_setup_home_renovation",
                "home_renovation_fixture=ready", "available=" + string.Join(",", available.Select(value => value.Name)),
                "home_renovation_fixture_native_option_or_region_missing");
        }

        ClearHomeRenovationRegion(home, renovation.renovationBounds[selectedIndex]);
        var nativeValid = renovation.validate is null || renovation.validate(renovation, selectedIndex);
        var verified = player.HouseUpgradeLevel == 2 && home.upgradeLevel == 2 &&
            player.daysUntilHouseUpgrade.Value == -1 && player.Money >= Math.Max(0, targetData.Price) &&
            player.mailReceived.Contains(firstPurchaseMail) == refundEligible &&
            ReferenceEquals(Game1.currentLocation, service) && player.TilePoint == stand.Value &&
            service.characters.Contains(robin) &&
            Vector2.Distance(robin.Tile, action.Value.ToVector2()) <= 3f &&
            HouseRenovation.GetAvailableRenovations().OfType<HouseRenovation>().Any(value => value.Name == request.RenovationId) &&
            nativeValid;

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
            PrimitiveKind = "debug_setup_home_renovation",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[]
                {
                    "isolated_save_fixture_ready",
                    "live_Data_HomeRenovations_requirement_state_ready",
                    "selected_native_region_clear",
                    "carpenter_action_and_robin_ready"
                }
                : new[] { "home_renovation_fixture_post_state_mismatch" },
            RequestedEffect = "home_renovation_fixture=ready;renovation=" + request.RenovationId +
                ";selected_index=" + selectedIndex + ";refund_eligible=" + refundEligible.ToString().ToLowerInvariant(),
            ObservedEffect = "location=" + (Game1.currentLocation?.NameOrUniqueName ?? "none") +
                ";house_level=" + player.HouseUpgradeLevel + ";money=" + player.Money +
                ";first_purchase=" + player.mailReceived.Contains(firstPurchaseMail).ToString().ToLowerInvariant() +
                ";native_valid=" + nativeValid.ToString().ToLowerInvariant() +
                ";available_count=" + available.Length,
            BlockReasons = verified ? Array.Empty<string>() : new[] { "home_renovation_fixture_post_state_mismatch" },
            ChangedFacts = verified
                ? new[]
                {
                    new SimulatedFactChange { Path = "player.location_id", Before = beforeLocation, After = service.NameOrUniqueName },
                    new SimulatedFactChange { Path = "player.farmhouse_upgrade_level", Before = beforeLevel.ToString(), After = player.HouseUpgradeLevel.ToString() },
                    new SimulatedFactChange { Path = "player.money", Before = beforeMoney.ToString(), After = player.Money.ToString() },
                    new SimulatedFactChange { Path = "player.mail_received." + firstPurchaseMail, Before = beforeFirstPurchase.ToString(), After = refundEligible.ToString() }
                }
                : Array.Empty<SimulatedFactChange>()
        };
    }

    private static bool SetHomeRenovationRequirement(
        FarmHouse home,
        Farmer player,
        RenovationValue requirement,
        out string reason)
    {
        reason = string.Empty;
        if (requirement.Type == "Value")
        {
            var field = home.GetType().GetField(requirement.Key, BindingFlags.Instance | BindingFlags.Public)?.GetValue(home) as NetInt;
            var expression = requirement.Value ?? string.Empty;
            var negated = expression.StartsWith('!');
            var valueText = negated ? expression[1..] : expression;
            if (field is null || !int.TryParse(valueText, out var expected))
            {
                reason = "home_renovation_fixture_value_requirement_unsupported:" + requirement.Key;
                return false;
            }
            field.Value = negated ? expected == int.MaxValue ? expected - 1 : expected + 1 : expected;
            return true;
        }
        if (requirement.Type == "Mail")
        {
            SetHomeRenovationMail(player, requirement.Key, requirement.Value == "1");
            return true;
        }
        reason = "home_renovation_fixture_requirement_type_unsupported:" + requirement.Type;
        return false;
    }

    private static void SetHomeRenovationMail(Farmer player, string mailId, bool present)
    {
        while (player.mailReceived.Remove(mailId)) { }
        while (player.mailbox.Remove(mailId)) { }
        foreach (var pending in player.mailForTomorrow.Where(value =>
                     string.Equals(value, mailId, StringComparison.Ordinal) ||
                     value.StartsWith(mailId + "%&NL&%", StringComparison.Ordinal)).ToArray())
        {
            player.mailForTomorrow.Remove(pending);
        }
        if (present)
            player.mailReceived.Add(mailId);
    }

    private static void ClearHomeRenovationRegion(FarmHouse home, IEnumerable<Rectangle> rectangles)
    {
        var regions = rectangles.ToArray();
        foreach (var tile in home.objects.Keys.Where(tile => regions.Any(rectangle =>
                     rectangle.Contains((int)tile.X, (int)tile.Y))).ToArray())
        {
            home.objects.Remove(tile);
        }
        foreach (var furniture in home.furniture.Where(item => regions.Any(rectangle =>
                     item.GetBoundingBox().Intersects(new Rectangle(
                         rectangle.X * Game1.tileSize,
                         rectangle.Y * Game1.tileSize,
                         rectangle.Width * Game1.tileSize,
                         rectangle.Height * Game1.tileSize)))).ToArray())
        {
            home.furniture.Remove(furniture);
        }
    }
}
