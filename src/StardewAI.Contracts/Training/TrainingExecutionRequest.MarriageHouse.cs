using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Training
{
    public sealed partial class TrainingExecutionRequest
    {
        [JsonPropertyName("expected_house_upgrade_level_before")]
        public int? ExpectedHouseUpgradeLevelBefore { get; set; }

        [JsonPropertyName("expected_house_upgrade_level_after_construction")]
        public int? ExpectedHouseUpgradeLevelAfterConstruction { get; set; }

        [JsonPropertyName("expected_days_until_house_upgrade_before")]
        public int? ExpectedDaysUntilHouseUpgradeBefore { get; set; }

        [JsonPropertyName("expected_days_until_house_upgrade_after")]
        public int? ExpectedDaysUntilHouseUpgradeAfter { get; set; }
    }
}
