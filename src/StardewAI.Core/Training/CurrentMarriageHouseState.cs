using System;
using System.Text.Json;

namespace StardewAI.Core.Training;

public sealed class CurrentMarriageHouseState
{
    public string SchemaVersion { get; set; } = "current_marriage_house_state.v1";
    public string Status { get; set; } = "blocked";
    public string SourceStateHash { get; set; } = string.Empty;
    public long GameTick { get; set; }
    public int? Year { get; set; }
    public string? Season { get; set; }
    public int? Day { get; set; }
    public int? CurrentTime { get; set; }
    public bool? MarriedOrRoommate { get; set; }
    public bool? Engaged { get; set; }
    public bool? HasCurrentOrPendingRoommate { get; set; }
    public string? Spouse { get; set; }
    public int? FarmhouseUpgradeLevel { get; set; }
    public int? DaysUntilFarmhouseUpgrade { get; set; }
    public int? Money { get; set; }
    public bool? CurrentCriterionSatisfied { get; set; }
    public int? HouseLevelsMissing { get; set; }
    public bool? PartnershipStillRequired { get; set; }
    public bool? NativeSettlementPending { get; set; }
    public JsonElement? HouseUpgradeQuote { get; set; }
    public JsonElement? NativeServiceState { get; set; }
    public string[] BlockingReasons { get; set; } = Array.Empty<string>();
    public bool TrainingLabelEligible => false;
    public bool NativeOutcomeVerified => false;
    public bool PriorAcquisitionVerified => false;
}
