using StardewAI.Contracts.Training;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private static bool TryNormalizeNativeObjectPayload(
        TrainingExecutionRequest request,
        out string reason)
    {
        reason = string.Empty;
        var payload = request.NativeObjectPayload;
        if (payload is null)
            return true;
        if (!string.Equals(payload.SchemaVersion, "native_object_execution_payload.v2", StringComparison.Ordinal))
        {
            reason = "native_object_payload_schema_unsupported";
            return false;
        }

        var expectedKind = request.OptionId switch
        {
            "world.rotate_house_plant" => "house_plant",
            "world.play_singing_stone" => "singing_stone",
            "farming.collect_slime_ball" => "slime_ball",
            "animals.withdraw_feed_hopper_hay" => "feed_hopper",
            "animals.collect_auto_grabber_contents" => "auto_grabber",
            "movement.use_mini_obelisk" => "mini_obelisk",
            "farming.read_farm_computer_report" => "farm_computer",
            _ => string.Empty
        };
        if (expectedKind.Length == 0 || !string.Equals(payload.Kind, expectedKind, StringComparison.Ordinal))
        {
            reason = "native_object_payload_kind_mismatch";
            return false;
        }
        var projectionCount = new object?[]
        {
            payload.HousePlant, payload.SingingStone, payload.SlimeBall,
            payload.FeedHopper, payload.AutoGrabber, payload.MiniObelisk,
            payload.FarmComputer
        }.Count(value => value is not null);
        if (projectionCount != 1)
        {
            reason = "native_object_payload_requires_exactly_one_projection";
            return false;
        }
        var projectionMatchesKind = expectedKind switch
        {
            "house_plant" => payload.HousePlant is not null,
            "singing_stone" => payload.SingingStone is not null,
            "slime_ball" => payload.SlimeBall is not null,
            "feed_hopper" => payload.FeedHopper is not null,
            "auto_grabber" => payload.AutoGrabber is not null,
            "mini_obelisk" => payload.MiniObelisk is not null,
            "farm_computer" => payload.FarmComputer is not null,
            _ => false
        };
        if (!projectionMatchesKind)
        {
            reason = "native_object_payload_projection_kind_mismatch";
            return false;
        }

        request.TargetTileX = payload.TargetTileX;
        request.TargetTileY = payload.TargetTileY;
        request.StandTileX = payload.StandTileX;
        request.StandTileY = payload.StandTileY;
        request.SafeSlotIndex = payload.SafeSlotIndex;
        request.RestoreSlotIndex = payload.RestoreSlotIndex;
        request.TargetRuntimeType = payload.TargetRuntimeType;
        request.ItemId = payload.ItemId;
        request.QualifiedItemId = payload.QualifiedItemId;
        request.InteractionKind = payload.InteractionKind;
        request.ExpectedActionType = payload.ExpectedActionType;
        request.NativeContract = payload.NativeContract;

        if (payload.HousePlant is { } housePlant)
        {
            request.HousePlantCurrentSpriteIndex = housePlant.CurrentSpriteIndex;
            request.HousePlantExpectedSpriteIndex = housePlant.ExpectedSpriteIndex;
            request.HousePlantExpectedObjectActionCalls = housePlant.ExpectedObjectActionCalls;
            request.HousePlantExpectedLocationActionReturn = housePlant.ExpectedLocationActionReturn;
        }
        if (payload.SingingStone is { } singingStone)
        {
            request.SingingStoneSafeSlotKind = payload.SafeSlotKind;
            request.SingingStoneSoundName = singingStone.SoundName;
            request.SingingStonePitchRngSource = singingStone.PitchRngSource;
            request.SingingStoneExactNextPitchStatus = singingStone.ExactNextPitchStatus;
            request.SingingStonePitchMin = singingStone.PitchMin;
            request.SingingStonePitchMax = singingStone.PitchMax;
            request.SingingStonePitchStep = singingStone.PitchStep;
            request.SingingStonePitchOutcomeCount = singingStone.PitchOutcomeCount;
            request.SingingStoneExpectedShakeTimer = singingStone.ExpectedShakeTimer;
            request.SingingStoneExpectedLocationActionReturn = singingStone.ExpectedLocationActionReturn;
        }
        if (payload.SlimeBall is { } slimeBall)
        {
            request.RequiredFragility = slimeBall.RequiredFragility;
            request.SlimeBallSeedDaysPlayed = slimeBall.SeedDaysPlayed;
            request.SlimeBallSeedUniqueGameId = slimeBall.SeedUniqueGameId;
            request.SlimeBallExpectedSlimeQuantity = slimeBall.ExpectedSlimeQuantity;
            request.SlimeBallExpectedPetrifiedSlimeQuantity = slimeBall.ExpectedPetrifiedSlimeQuantity;
            request.SlimeBallExpectedLocationActionReturn = slimeBall.ExpectedLocationActionReturn;
        }
        if (payload.FeedHopper is { } feedHopper)
        {
            request.FeedHopperSafeSlotKind = payload.SafeSlotKind;
            request.FeedHopperHayQualifiedItemId = feedHopper.HayQualifiedItemId;
            request.FeedHopperRootLocationId = feedHopper.RootLocationId;
            request.FeedHopperSiloHayBefore = feedHopper.SiloHayBefore;
            request.FeedHopperAnimalCount = feedHopper.AnimalCount;
            request.FeedHopperAnimalLimit = feedHopper.AnimalLimit;
            request.FeedHopperPlacedHayCount = feedHopper.PlacedHayCount;
            request.FeedHopperUnfedAnimalCount = feedHopper.UnfedAnimalCount;
            request.FeedHopperExpectedWithdrawalQuantity = feedHopper.ExpectedWithdrawalQuantity;
            request.FeedHopperExpectedSiloHayAfter = feedHopper.ExpectedSiloHayAfter;
            request.FeedHopperExpectedLocationActionReturn = feedHopper.ExpectedLocationActionReturn;
        }
        if (payload.AutoGrabber is { } autoGrabber)
        {
            request.AutoGrabberSafeSlotKind = payload.SafeSlotKind;
            request.AutoGrabberHeldContainerRuntimeType = autoGrabber.HeldContainerRuntimeType;
            request.AutoGrabberContentsBeforeJson = autoGrabber.ContentsBeforeJson;
            request.AutoGrabberTransferableContentsJson = autoGrabber.TransferableContentsJson;
            request.AutoGrabberRemainingContentsJson = autoGrabber.RemainingContentsJson;
            request.AutoGrabberContentStackCountBefore = autoGrabber.ContentStackCountBefore;
            request.AutoGrabberTransferableStackCount = autoGrabber.TransferableStackCount;
            request.AutoGrabberExpectedStackCountAfter = autoGrabber.ExpectedStackCountAfter;
            request.AutoGrabberContentQuantityBefore = autoGrabber.ContentQuantityBefore;
            request.AutoGrabberExpectedTransferQuantity = autoGrabber.ExpectedTransferQuantity;
            request.AutoGrabberExpectedQuantityAfter = autoGrabber.ExpectedQuantityAfter;
            request.AutoGrabberExpectedLocationActionReturn = autoGrabber.ExpectedLocationActionReturn;
        }
        if (payload.MiniObelisk is { } miniObelisk)
        {
            request.MiniObeliskSafeSlotKind = payload.SafeSlotKind;
            request.MiniObeliskPairMemberIndex = miniObelisk.PairMemberIndex;
            request.MiniObeliskPairFirstTileX = miniObelisk.PairFirstTileX;
            request.MiniObeliskPairFirstTileY = miniObelisk.PairFirstTileY;
            request.MiniObeliskPairSecondTileX = miniObelisk.PairSecondTileX;
            request.MiniObeliskPairSecondTileY = miniObelisk.PairSecondTileY;
            request.MiniObeliskDestinationTileX = miniObelisk.DestinationTileX;
            request.MiniObeliskDestinationTileY = miniObelisk.DestinationTileY;
            request.MiniObeliskLandingTileX = miniObelisk.LandingTileX;
            request.MiniObeliskLandingTileY = miniObelisk.LandingTileY;
            request.MiniObeliskExpectedDelayMilliseconds = miniObelisk.ExpectedDelayMilliseconds;
            request.MiniObeliskExpectedLocationActionReturn = miniObelisk.ExpectedLocationActionReturn;
        }
        if (payload.FarmComputer is { } farmComputer)
        {
            request.FarmComputerSafeSlotKind = payload.SafeSlotKind;
            request.FarmComputerRootLocationId = farmComputer.RootLocationId;
            request.FarmComputerIncludesHay = farmComputer.IncludesHay;
            request.FarmComputerPiecesOfHay = farmComputer.PiecesOfHay;
            request.FarmComputerHayCapacity = farmComputer.HayCapacity;
            request.FarmComputerTotalCrops = farmComputer.TotalCrops;
            request.FarmComputerCropsReady = farmComputer.CropsReady;
            request.FarmComputerUnwateredCrops = farmComputer.UnwateredCrops;
            request.FarmComputerGreenhouseCropsReady = farmComputer.GreenhouseCropsReady;
            request.FarmComputerOpenHoeDirt = farmComputer.OpenHoeDirt;
            request.FarmComputerTotalForage = farmComputer.TotalForage;
            request.FarmComputerMachinesReady = farmComputer.MachinesReady;
            request.FarmComputerFarmCaveReady = farmComputer.FarmCaveReady;
            request.FarmComputerReportSha256 = farmComputer.ReportSha256;
            request.FarmComputerExpectedDelayMs = farmComputer.ExpectedDelayMs;
            request.FarmComputerExpectedShakeTimer = farmComputer.ExpectedShakeTimer;
            request.FarmComputerExpectedFreezeMs = farmComputer.ExpectedFreezeMs;
            request.FarmComputerExpectedLocationActionReturn = farmComputer.ExpectedLocationActionReturn;
        }
        return true;
    }
}
