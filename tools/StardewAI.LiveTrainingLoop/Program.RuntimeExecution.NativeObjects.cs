using System.Text.Json.Nodes;
using StardewAI.Contracts.Training;

static partial class Program
{
    private static void BindNativeObjectExecutionRequest(
        TrainingExecutionRequest request,
        JsonObject? item)
    {
        var kind = request.OptionId switch
        {
            "world.rotate_house_plant" => "house_plant",
            "world.play_singing_stone" => "singing_stone",
            "farming.collect_slime_ball" => "slime_ball",
            "animals.withdraw_feed_hopper_hay" => "feed_hopper",
            "animals.collect_auto_grabber_contents" => "auto_grabber",
            "movement.use_mini_obelisk" => "mini_obelisk",
            _ => string.Empty
        };
        if (kind.Length == 0)
            return;

        request.HousePlantCurrentSpriteIndex = ReadQueueParameterInt(item, "house_plant_current_sprite_index");
        request.HousePlantExpectedSpriteIndex = ReadQueueParameterInt(item, "house_plant_expected_sprite_index");
        request.HousePlantExpectedObjectActionCalls = ReadQueueParameterInt(item, "house_plant_expected_object_action_calls");
        request.HousePlantExpectedLocationActionReturn = ReadNullableBoolQueueParameter(item, "house_plant_expected_location_action_return");
        request.SingingStoneSafeSlotKind = ReadQueueParameterString(item, "safe_slot_kind");
        request.SingingStoneSoundName = ReadQueueParameterString(item, "singing_stone_sound_name");
        request.SingingStonePitchRngSource = ReadQueueParameterString(item, "singing_stone_pitch_rng_source");
        request.SingingStoneExactNextPitchStatus = ReadQueueParameterString(item, "singing_stone_exact_next_pitch_status");
        request.SingingStonePitchMin = ReadQueueParameterInt(item, "singing_stone_pitch_min");
        request.SingingStonePitchMax = ReadQueueParameterInt(item, "singing_stone_pitch_max");
        request.SingingStonePitchStep = ReadQueueParameterInt(item, "singing_stone_pitch_step");
        request.SingingStonePitchOutcomeCount = ReadQueueParameterInt(item, "singing_stone_pitch_outcome_count");
        request.SingingStoneExpectedShakeTimer = ReadQueueParameterInt(item, "singing_stone_expected_shake_timer");
        request.SingingStoneExpectedLocationActionReturn = ReadNullableBoolQueueParameter(item, "singing_stone_expected_location_action_return");
        request.RequiredFragility = ReadQueueParameterInt(item, "required_fragility");
        request.SlimeBallSeedDaysPlayed = ReadQueueParameterInt(item, "slime_ball_seed_days_played");
        request.SlimeBallSeedUniqueGameId = ReadQueueParameterLong(item, "slime_ball_seed_unique_game_id");
        request.SlimeBallExpectedSlimeQuantity = ReadQueueParameterInt(item, "slime_ball_expected_slime_quantity");
        request.SlimeBallExpectedPetrifiedSlimeQuantity = ReadQueueParameterInt(item, "slime_ball_expected_petrified_slime_quantity");
        request.SlimeBallExpectedLocationActionReturn = ReadNullableBoolQueueParameter(item, "slime_ball_expected_location_action_return");
        request.FeedHopperSafeSlotKind = ReadQueueParameterString(item, "safe_slot_kind");
        request.FeedHopperHayQualifiedItemId = ReadQueueParameterString(item, "feed_hopper_hay_qualified_item_id");
        request.FeedHopperRootLocationId = ReadQueueParameterString(item, "feed_hopper_root_location_id");
        request.FeedHopperSiloHayBefore = ReadQueueParameterInt(item, "feed_hopper_silo_hay_before");
        request.FeedHopperAnimalCount = ReadQueueParameterInt(item, "feed_hopper_animal_count");
        request.FeedHopperAnimalLimit = ReadQueueParameterInt(item, "feed_hopper_animal_limit");
        request.FeedHopperPlacedHayCount = ReadQueueParameterInt(item, "feed_hopper_placed_hay_count");
        request.FeedHopperUnfedAnimalCount = ReadQueueParameterInt(item, "feed_hopper_unfed_animal_count");
        request.FeedHopperExpectedWithdrawalQuantity = ReadQueueParameterInt(item, "feed_hopper_expected_withdrawal_quantity");
        request.FeedHopperExpectedSiloHayAfter = ReadQueueParameterInt(item, "feed_hopper_expected_silo_hay_after");
        request.FeedHopperExpectedLocationActionReturn = ReadNullableBoolQueueParameter(item, "feed_hopper_expected_location_action_return");
        request.AutoGrabberSafeSlotKind = ReadQueueParameterString(item, "safe_slot_kind");
        request.AutoGrabberHeldContainerRuntimeType = ReadQueueParameterString(item, "auto_grabber_held_container_runtime_type");
        request.AutoGrabberContentsBeforeJson = ReadQueueParameterString(item, "auto_grabber_contents_before_json");
        request.AutoGrabberTransferableContentsJson = ReadQueueParameterString(item, "auto_grabber_transferable_contents_json");
        request.AutoGrabberRemainingContentsJson = ReadQueueParameterString(item, "auto_grabber_remaining_contents_json");
        request.AutoGrabberContentStackCountBefore = ReadQueueParameterInt(item, "auto_grabber_content_stack_count_before");
        request.AutoGrabberTransferableStackCount = ReadQueueParameterInt(item, "auto_grabber_transferable_stack_count");
        request.AutoGrabberExpectedStackCountAfter = ReadQueueParameterInt(item, "auto_grabber_expected_stack_count_after");
        request.AutoGrabberContentQuantityBefore = ReadQueueParameterInt(item, "auto_grabber_content_quantity_before");
        request.AutoGrabberExpectedTransferQuantity = ReadQueueParameterInt(item, "auto_grabber_expected_transfer_quantity");
        request.AutoGrabberExpectedQuantityAfter = ReadQueueParameterInt(item, "auto_grabber_expected_quantity_after");
        request.AutoGrabberExpectedLocationActionReturn = ReadNullableBoolQueueParameter(item, "auto_grabber_expected_location_action_return");
        request.MiniObeliskSafeSlotKind = ReadQueueParameterString(item, "safe_slot_kind");
        request.MiniObeliskPairMemberIndex = ReadQueueParameterInt(item, "mini_obelisk_pair_member_index");
        request.MiniObeliskPairFirstTileX = ReadQueueParameterInt(item, "mini_obelisk_pair_first_tile_x");
        request.MiniObeliskPairFirstTileY = ReadQueueParameterInt(item, "mini_obelisk_pair_first_tile_y");
        request.MiniObeliskPairSecondTileX = ReadQueueParameterInt(item, "mini_obelisk_pair_second_tile_x");
        request.MiniObeliskPairSecondTileY = ReadQueueParameterInt(item, "mini_obelisk_pair_second_tile_y");
        request.MiniObeliskDestinationTileX = ReadQueueParameterInt(item, "mini_obelisk_destination_tile_x");
        request.MiniObeliskDestinationTileY = ReadQueueParameterInt(item, "mini_obelisk_destination_tile_y");
        request.MiniObeliskLandingTileX = ReadQueueParameterInt(item, "mini_obelisk_landing_tile_x");
        request.MiniObeliskLandingTileY = ReadQueueParameterInt(item, "mini_obelisk_landing_tile_y");
        request.MiniObeliskExpectedDelayMilliseconds = ReadQueueParameterInt(item, "mini_obelisk_expected_delay_milliseconds");
        request.MiniObeliskExpectedLocationActionReturn = ReadNullableBoolQueueParameter(item, "mini_obelisk_expected_location_action_return");

        request.NativeObjectPayload = new NativeObjectExecutionPayload
        {
            Kind = kind,
            TargetTileX = ReadQueueParameterInt(item, "target_tile_x"),
            TargetTileY = ReadQueueParameterInt(item, "target_tile_y"),
            StandTileX = ReadQueueParameterInt(item, "stand_tile_x"),
            StandTileY = ReadQueueParameterInt(item, "stand_tile_y"),
            SafeSlotIndex = ReadQueueParameterInt(item, "safe_slot_index"),
            SafeSlotKind = ReadQueueParameterString(item, "safe_slot_kind"),
            RestoreSlotIndex = ReadQueueParameterInt(item, "restore_slot_index"),
            TargetRuntimeType = ReadQueueParameterString(item, "target_runtime_type"),
            ItemId = ReadQueueParameterString(item, "item_id"),
            QualifiedItemId = ReadQueueParameterString(item, "qualified_item_id"),
            InteractionKind = ReadQueueParameterString(item, "interaction_kind"),
            ExpectedActionType = ReadQueueParameterString(item, "expected_action_type"),
            NativeContract = ReadQueueParameterString(item, "native_contract")
        };
        switch (kind)
        {
            case "house_plant":
                request.NativeObjectPayload.HousePlant = new HousePlantExecutionProjection
                {
                    CurrentSpriteIndex = request.HousePlantCurrentSpriteIndex,
                    ExpectedSpriteIndex = request.HousePlantExpectedSpriteIndex,
                    ExpectedObjectActionCalls = request.HousePlantExpectedObjectActionCalls,
                    ExpectedLocationActionReturn = request.HousePlantExpectedLocationActionReturn
                };
                break;
            case "singing_stone":
                request.NativeObjectPayload.SingingStone = new SingingStoneExecutionProjection
                {
                    SoundName = request.SingingStoneSoundName,
                    PitchRngSource = request.SingingStonePitchRngSource,
                    ExactNextPitchStatus = request.SingingStoneExactNextPitchStatus,
                    PitchMin = request.SingingStonePitchMin,
                    PitchMax = request.SingingStonePitchMax,
                    PitchStep = request.SingingStonePitchStep,
                    PitchOutcomeCount = request.SingingStonePitchOutcomeCount,
                    ExpectedShakeTimer = request.SingingStoneExpectedShakeTimer,
                    ExpectedLocationActionReturn = request.SingingStoneExpectedLocationActionReturn
                };
                break;
            case "slime_ball":
                request.NativeObjectPayload.SlimeBall = new SlimeBallExecutionProjection
                {
                    RequiredFragility = request.RequiredFragility,
                    SeedDaysPlayed = request.SlimeBallSeedDaysPlayed,
                    SeedUniqueGameId = request.SlimeBallSeedUniqueGameId,
                    ExpectedSlimeQuantity = request.SlimeBallExpectedSlimeQuantity,
                    ExpectedPetrifiedSlimeQuantity = request.SlimeBallExpectedPetrifiedSlimeQuantity,
                    ExpectedLocationActionReturn = request.SlimeBallExpectedLocationActionReturn
                };
                break;
            case "feed_hopper":
                request.NativeObjectPayload.FeedHopper = new FeedHopperExecutionProjection
                {
                    HayQualifiedItemId = request.FeedHopperHayQualifiedItemId,
                    RootLocationId = request.FeedHopperRootLocationId,
                    SiloHayBefore = request.FeedHopperSiloHayBefore,
                    AnimalCount = request.FeedHopperAnimalCount,
                    AnimalLimit = request.FeedHopperAnimalLimit,
                    PlacedHayCount = request.FeedHopperPlacedHayCount,
                    UnfedAnimalCount = request.FeedHopperUnfedAnimalCount,
                    ExpectedWithdrawalQuantity = request.FeedHopperExpectedWithdrawalQuantity,
                    ExpectedSiloHayAfter = request.FeedHopperExpectedSiloHayAfter,
                    ExpectedLocationActionReturn = request.FeedHopperExpectedLocationActionReturn
                };
                break;
            case "auto_grabber":
                request.NativeObjectPayload.AutoGrabber = new AutoGrabberExecutionProjection
                {
                    HeldContainerRuntimeType = request.AutoGrabberHeldContainerRuntimeType,
                    ContentsBeforeJson = request.AutoGrabberContentsBeforeJson,
                    TransferableContentsJson = request.AutoGrabberTransferableContentsJson,
                    RemainingContentsJson = request.AutoGrabberRemainingContentsJson,
                    ContentStackCountBefore = request.AutoGrabberContentStackCountBefore,
                    TransferableStackCount = request.AutoGrabberTransferableStackCount,
                    ExpectedStackCountAfter = request.AutoGrabberExpectedStackCountAfter,
                    ContentQuantityBefore = request.AutoGrabberContentQuantityBefore,
                    ExpectedTransferQuantity = request.AutoGrabberExpectedTransferQuantity,
                    ExpectedQuantityAfter = request.AutoGrabberExpectedQuantityAfter,
                    ExpectedLocationActionReturn = request.AutoGrabberExpectedLocationActionReturn
                };
                break;
            case "mini_obelisk":
                request.NativeObjectPayload.MiniObelisk = new MiniObeliskExecutionProjection
                {
                    PairMemberIndex = request.MiniObeliskPairMemberIndex,
                    PairFirstTileX = request.MiniObeliskPairFirstTileX,
                    PairFirstTileY = request.MiniObeliskPairFirstTileY,
                    PairSecondTileX = request.MiniObeliskPairSecondTileX,
                    PairSecondTileY = request.MiniObeliskPairSecondTileY,
                    DestinationTileX = request.MiniObeliskDestinationTileX,
                    DestinationTileY = request.MiniObeliskDestinationTileY,
                    LandingTileX = request.MiniObeliskLandingTileX,
                    LandingTileY = request.MiniObeliskLandingTileY,
                    ExpectedDelayMilliseconds = request.MiniObeliskExpectedDelayMilliseconds,
                    ExpectedLocationActionReturn = request.MiniObeliskExpectedLocationActionReturn
                };
                break;
        }
    }
}
