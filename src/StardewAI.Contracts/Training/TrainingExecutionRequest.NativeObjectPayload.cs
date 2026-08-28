using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Training;

public sealed partial class TrainingExecutionRequest
{
    [JsonPropertyName("native_object_payload")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeObjectExecutionPayload? NativeObjectPayload { get; set; }
}

public sealed class NativeObjectExecutionPayload
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } = "native_object_execution_payload.v2";

    [JsonPropertyName("kind")]
    public string Kind { get; set; } = string.Empty;

    [JsonPropertyName("target_tile_x")]
    public int? TargetTileX { get; set; }

    [JsonPropertyName("target_tile_y")]
    public int? TargetTileY { get; set; }

    [JsonPropertyName("stand_tile_x")]
    public int? StandTileX { get; set; }

    [JsonPropertyName("stand_tile_y")]
    public int? StandTileY { get; set; }

    [JsonPropertyName("safe_slot_index")]
    public int? SafeSlotIndex { get; set; }

    [JsonPropertyName("safe_slot_kind")]
    public string SafeSlotKind { get; set; } = string.Empty;

    [JsonPropertyName("restore_slot_index")]
    public int? RestoreSlotIndex { get; set; }

    [JsonPropertyName("target_runtime_type")]
    public string TargetRuntimeType { get; set; } = string.Empty;

    [JsonPropertyName("item_id")]
    public string ItemId { get; set; } = string.Empty;

    [JsonPropertyName("qualified_item_id")]
    public string QualifiedItemId { get; set; } = string.Empty;

    [JsonPropertyName("interaction_kind")]
    public string InteractionKind { get; set; } = string.Empty;

    [JsonPropertyName("expected_action_type")]
    public string ExpectedActionType { get; set; } = string.Empty;

    [JsonPropertyName("native_contract")]
    public string NativeContract { get; set; } = string.Empty;

    [JsonPropertyName("house_plant")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public HousePlantExecutionProjection? HousePlant { get; set; }

    [JsonPropertyName("singing_stone")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SingingStoneExecutionProjection? SingingStone { get; set; }

    [JsonPropertyName("flute_block")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public FluteBlockExecutionProjection? FluteBlock { get; set; }

    [JsonPropertyName("drum_block")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DrumBlockExecutionProjection? DrumBlock { get; set; }

    [JsonPropertyName("slime_ball")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SlimeBallExecutionProjection? SlimeBall { get; set; }

    [JsonPropertyName("feed_hopper")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public FeedHopperExecutionProjection? FeedHopper { get; set; }

    [JsonPropertyName("auto_grabber")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AutoGrabberExecutionProjection? AutoGrabber { get; set; }

    [JsonPropertyName("mini_obelisk")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public MiniObeliskExecutionProjection? MiniObelisk { get; set; }

    [JsonPropertyName("farm_computer")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public FarmComputerExecutionProjection? FarmComputer { get; set; }
}

public sealed class HousePlantExecutionProjection
{
    public int? CurrentSpriteIndex { get; set; }
    public int? ExpectedSpriteIndex { get; set; }
    public int? ExpectedObjectActionCalls { get; set; }
    public bool? ExpectedLocationActionReturn { get; set; }
}

public sealed class SingingStoneExecutionProjection
{
    public string SoundName { get; set; } = string.Empty;
    public string PitchRngSource { get; set; } = string.Empty;
    public string ExactNextPitchStatus { get; set; } = string.Empty;
    public int? PitchMin { get; set; }
    public int? PitchMax { get; set; }
    public int? PitchStep { get; set; }
    public int? PitchOutcomeCount { get; set; }
    public int? ExpectedShakeTimer { get; set; }
    public bool? ExpectedLocationActionReturn { get; set; }
}

public sealed class FluteBlockExecutionProjection
{
    public string CurrentPitchRaw { get; set; } = string.Empty;
    public int? CurrentPitch { get; set; }
    public int? NextPitch { get; set; }
    public int? PitchMin { get; set; }
    public int? PitchMax { get; set; }
    public int? PitchStep { get; set; }
    public int? PitchStateCount { get; set; }
    public string SoundCue { get; set; } = string.Empty;
    public int? ExpectedShakeTimer { get; set; }
    public float? ExpectedScaleY { get; set; }
    public bool? ExpectedLocationActionReturn { get; set; }
}

public sealed class DrumBlockExecutionProjection
{
    public string CurrentToneRaw { get; set; } = string.Empty;
    public int? CurrentTone { get; set; }
    public int? NextTone { get; set; }
    public int? ToneMin { get; set; }
    public int? ToneMax { get; set; }
    public int? ToneStep { get; set; }
    public int? ToneStateCount { get; set; }
    public string SoundCue { get; set; } = string.Empty;
    public int? ExpectedShakeTimer { get; set; }
    public float? ExpectedScaleY { get; set; }
    public bool? ExpectedLocationActionReturn { get; set; }
}

public sealed class SlimeBallExecutionProjection
{
    public int? RequiredFragility { get; set; }
    public int? SeedDaysPlayed { get; set; }
    public long? SeedUniqueGameId { get; set; }
    public int? ExpectedSlimeQuantity { get; set; }
    public int? ExpectedPetrifiedSlimeQuantity { get; set; }
    public bool? ExpectedLocationActionReturn { get; set; }
}

public sealed class FeedHopperExecutionProjection
{
    public string HayQualifiedItemId { get; set; } = string.Empty;
    public string RootLocationId { get; set; } = string.Empty;
    public int? SiloHayBefore { get; set; }
    public int? AnimalCount { get; set; }
    public int? AnimalLimit { get; set; }
    public int? PlacedHayCount { get; set; }
    public int? UnfedAnimalCount { get; set; }
    public int? ExpectedWithdrawalQuantity { get; set; }
    public int? ExpectedSiloHayAfter { get; set; }
    public bool? ExpectedLocationActionReturn { get; set; }
}

public sealed class AutoGrabberExecutionProjection
{
    public string HeldContainerRuntimeType { get; set; } = string.Empty;
    public string ContentsBeforeJson { get; set; } = string.Empty;
    public string TransferableContentsJson { get; set; } = string.Empty;
    public string RemainingContentsJson { get; set; } = string.Empty;
    public int? ContentStackCountBefore { get; set; }
    public int? TransferableStackCount { get; set; }
    public int? ExpectedStackCountAfter { get; set; }
    public int? ContentQuantityBefore { get; set; }
    public int? ExpectedTransferQuantity { get; set; }
    public int? ExpectedQuantityAfter { get; set; }
    public bool? ExpectedLocationActionReturn { get; set; }
}

public sealed class MiniObeliskExecutionProjection
{
    public int? PairMemberIndex { get; set; }
    public int? PairFirstTileX { get; set; }
    public int? PairFirstTileY { get; set; }
    public int? PairSecondTileX { get; set; }
    public int? PairSecondTileY { get; set; }
    public int? DestinationTileX { get; set; }
    public int? DestinationTileY { get; set; }
    public int? LandingTileX { get; set; }
    public int? LandingTileY { get; set; }
    public int? ExpectedDelayMilliseconds { get; set; }
    public bool? ExpectedLocationActionReturn { get; set; }
}

public sealed class FarmComputerExecutionProjection
{
    public string RootLocationId { get; set; } = string.Empty;
    public bool? IncludesHay { get; set; }
    public int? PiecesOfHay { get; set; }
    public int? HayCapacity { get; set; }
    public int? TotalCrops { get; set; }
    public int? CropsReady { get; set; }
    public int? UnwateredCrops { get; set; }
    public int? GreenhouseCropsReady { get; set; }
    public int? OpenHoeDirt { get; set; }
    public int? TotalForage { get; set; }
    public int? MachinesReady { get; set; }
    public bool? FarmCaveReady { get; set; }
    public string ReportSha256 { get; set; } = string.Empty;
    public int? ExpectedDelayMs { get; set; }
    public int? ExpectedShakeTimer { get; set; }
    public int? ExpectedFreezeMs { get; set; }
    public bool? ExpectedLocationActionReturn { get; set; }
}
