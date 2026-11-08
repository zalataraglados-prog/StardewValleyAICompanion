using System;
using System.Text.Json.Serialization;

namespace StardewAI.Contracts.State;

public sealed class HayStorageSnapshot
{
    [JsonPropertyName("schema_version")] public string SchemaVersion { get; set; } = "hay_storage_inventory.v1";
    [JsonPropertyName("status")] public string Status { get; set; } = "unavailable";
    [JsonPropertyName("location_scope")] public string LocationScope { get; set; } = "noninterior_locations_and_farm";
    [JsonPropertyName("locations")] public HayStorageLocation[] Locations { get; set; } = Array.Empty<HayStorageLocation>();
    [JsonPropertyName("diagnostics")] public string[] Diagnostics { get; set; } = Array.Empty<string>();
}

public sealed class HayStorageContext
{
    [JsonPropertyName("schema_version")] public string SchemaVersion { get; set; } = "hay_storage_context.v1";
    [JsonPropertyName("status")] public string Status { get; set; } = "unavailable";
    [JsonPropertyName("native_rule_status")] public string NativeRuleStatus { get; set; } = "unavailable";
    [JsonPropertyName("source_storage")] public HayStorageLocation? SourceStorage { get; set; }
    [JsonPropertyName("farm_location_id")] public string FarmLocationId { get; set; } = string.Empty;
    [JsonPropertyName("noninterior_location_ids")] public string[] NoninteriorLocationIds { get; set; } = Array.Empty<string>();
    [JsonPropertyName("diagnostics")] public string[] Diagnostics { get; set; } = Array.Empty<string>();
}

public sealed class HayStorageLocation
{
    [JsonPropertyName("location_id")] public string LocationId { get; set; } = string.Empty;
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("runtime_type")] public string RuntimeType { get; set; } = string.Empty;
    [JsonPropertyName("building_count")] public int BuildingCount { get; set; }
    [JsonPropertyName("stored_hay")] public int StoredHay { get; set; }
    [JsonPropertyName("hay_capacity")] public int? HayCapacity { get; set; }
    [JsonPropertyName("capacity_status")] public string CapacityStatus { get; set; } = "unavailable";
}
