using System.Globalization;
using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Locations;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private TrainingExecutionResult ExecuteSetupMultiplayerWalletFixture(TrainingExecutionRequest request)
    {
        var started = DateTimeOffset.UtcNow.ToString("O");
        var claimed = MultiplayerWalletClaimedFarmers();
        if (!Context.IsWorldReady || !Game1.IsMasterGame || claimed.Length != 3 ||
            request.WalletOperation is not ("shared" or "separate"))
        {
            return BlockedWithPrimitive(request, "debug_setup_multiplayer_wallet",
                "wallet_fixture_mode=" + request.WalletOperation, MultiplayerWalletObservedEffect(),
                "multiplayer_wallet_fixture_requires_host_and_exactly_three_claimed_farmers");
        }

        Game1.exitActiveMenu();
        StopAllMovement();
        var manor = Game1.getLocationFromName("ManorHouse") as ManorHouse;
        var ledger = manor is null
            ? (Point?)null
            : Enumerable.Range(0, manor.Map.Layers[0].LayerHeight)
                .SelectMany(y => Enumerable.Range(0, manor.Map.Layers[0].LayerWidth), (y, x) => new Point(x, y))
                .FirstOrDefault(tile => manor.doesTileHaveProperty(tile.X, tile.Y, "Action", "Buildings") == "LedgerBook");
        var stand = ledger.HasValue && manor is not null
            ? Neighbors(ledger.Value).FirstOrDefault(tile => IsTileOnMap(manor, tile) && IsTileWalkable(manor, tile) && !IsTileOccupiedByCharacter(manor, tile))
            : Point.Zero;
        if (manor is null || !ledger.HasValue || ledger.Value == Point.Zero || stand == Point.Zero)
        {
            return BlockedWithPrimitive(request, "debug_setup_multiplayer_wallet",
                "wallet_fixture_mode=" + request.WalletOperation, MultiplayerWalletObservedEffect(),
                "multiplayer_wallet_fixture_ledger_or_stand_missing");
        }
        if (!ReferenceEquals(Game1.currentLocation, manor) || Game1.player.TilePoint != stand)
            Game1.warpFarmer(manor.NameOrUniqueName, stand.X, stand.Y, false);
        var team = Game1.player.team;
        team.useSeparateWallets.Value = true;
        team.SetIndividualMoney(Game1.player, 700);
        foreach (var farmer in claimed.Where(farmer => farmer.UniqueMultiplayerID != Game1.player.UniqueMultiplayerID)
                     .OrderBy(farmer => farmer.UniqueMultiplayerID).Select((farmer, index) => (farmer, index)))
        {
            team.SetIndividualMoney(farmer.farmer, farmer.index == 0 ? 200 : 101);
        }
        team.money.Value = 1001;
        Game1.player.changeWalletTypeTonight.Value = false;
        team.useSeparateWallets.Value = request.WalletOperation == "separate";

        var verified = team.money.Value == 1001 && !Game1.player.changeWalletTypeTonight.Value &&
            team.useSeparateWallets.Value == (request.WalletOperation == "separate") &&
            (!team.useSeparateWallets.Value || claimed.Select(MultiplayerWalletEffectiveBalance)
                .OrderBy(value => value).SequenceEqual(new[] { 101, 200, 700 }));
        return new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            StartedAt = started,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "debug_setup_multiplayer_wallet",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[] { "isolated_three_participant_wallet_fixture_ready" }
                : new[] { "multiplayer_wallet_fixture_setup_mismatch" },
            RequestedEffect = "wallet_fixture_mode=" + request.WalletOperation,
            ObservedEffect = MultiplayerWalletObservedEffect(),
            BlockReasons = verified ? Array.Empty<string>() : new[] { "multiplayer_wallet_fixture_setup_mismatch" }
        };
    }

    private TrainingExecutionResult ExecuteSettleMultiplayerWalletFixture(TrainingExecutionRequest request)
    {
        var started = DateTimeOffset.UtcNow.ToString("O");
        var team = Game1.player.team;
        var claimed = MultiplayerWalletClaimedFarmers();
        var settlingSeparate = request.WalletOperation == "settle_separate";
        var settlingMerge = request.WalletOperation == "settle_merge";
        if (!Context.IsWorldReady || !Game1.IsMasterGame || claimed.Length == 0 ||
            (!settlingSeparate && !settlingMerge) || !Game1.player.changeWalletTypeTonight.Value ||
            settlingSeparate && team.useSeparateWallets.Value || settlingMerge && !team.useSeparateWallets.Value)
        {
            return BlockedWithPrimitive(request, "debug_settle_multiplayer_wallet",
                "wallet_settlement=" + request.WalletOperation, MultiplayerWalletObservedEffect(),
                "multiplayer_wallet_fixture_settlement_preconditions_not_met");
        }

        var sharedBefore = team.money.Value;
        var individualTotalBefore = claimed.Sum(MultiplayerWalletEffectiveBalance);
        if (settlingSeparate)
            ManorHouse.SeparateWallets();
        else
            ManorHouse.MergeWallets();

        var expectedEach = sharedBefore / Math.Max(claimed.Length, 1);
        var verified = !Game1.player.changeWalletTypeTonight.Value &&
            (settlingSeparate
                ? team.useSeparateWallets.Value && claimed.All(farmer => MultiplayerWalletEffectiveBalance(farmer) == expectedEach)
                : !team.useSeparateWallets.Value && team.money.Value == individualTotalBefore);
        var reasons = verified
            ? settlingSeparate
                ? new[] { "native_SeparateWallets_equal_integer_share_and_remainder_discard_verified" }
                : new[] { "native_MergeWallets_claimed_individual_sum_verified" }
            : new[] { "multiplayer_wallet_native_settlement_mismatch" };
        return new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            StartedAt = started,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "debug_settle_multiplayer_wallet",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = reasons,
            RequestedEffect = "wallet_settlement=" + request.WalletOperation,
            ObservedEffect = MultiplayerWalletObservedEffect() + ";claimed_count=" + claimed.Length.ToString(CultureInfo.InvariantCulture),
            BlockReasons = verified ? Array.Empty<string>() : reasons
        };
    }
}
