using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Training
{
    public sealed partial class TrainingExecutionRequest
    {
        [JsonPropertyName("native_storage_branch")]
        public string NativeStorageBranch { get; set; } =
            string.Empty;

        [JsonPropertyName("special_chest_type")]
        public string SpecialChestType { get; set; } =
            string.Empty;

        [JsonPropertyName("expected_storage_capacity")]
        public int? ExpectedStorageCapacity { get; set; }

        [JsonPropertyName("storage_role")]
        public string StorageRole { get; set; } =
            string.Empty;
    }
}
