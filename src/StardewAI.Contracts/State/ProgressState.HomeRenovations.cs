using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace StardewAI.Contracts.State;

public sealed class HomeRenovationCatalogRef
{
    [JsonPropertyName("projection_status")]
    public string ProjectionStatus { get; set; } = string.Empty;

    [JsonPropertyName("data_asset_name")]
    public string DataAssetName { get; set; } = "Data/HomeRenovations";

    [JsonPropertyName("data_payload_sha256")]
    public string DataPayloadSha256 { get; set; } = string.Empty;

    [JsonPropertyName("data_contract_status")]
    public string DataContractStatus { get; set; } = string.Empty;

    [JsonPropertyName("home_location_id")]
    public string HomeLocationId { get; set; } = string.Empty;

    [JsonPropertyName("home_runtime_type")]
    public string HomeRuntimeType { get; set; } = string.Empty;

    [JsonPropertyName("house_upgrade_level")]
    public int HouseUpgradeLevel { get; set; }

    [JsonPropertyName("crib_style")]
    public int CribStyle { get; set; }

    [JsonPropertyName("can_modify_crib")]
    public bool CanModifyCrib { get; set; }

    [JsonPropertyName("crib_modification_block_reasons")]
    public string[] CribModificationBlockReasons { get; set; } = System.Array.Empty<string>();

    [JsonPropertyName("service_location_id")]
    public string ServiceLocationId { get; set; } = "ScienceHouse";

    [JsonPropertyName("service_action_raw")]
    public string ServiceActionRaw { get; set; } = string.Empty;

    [JsonPropertyName("service_action_tile_x")]
    public int? ServiceActionTileX { get; set; }

    [JsonPropertyName("service_action_tile_y")]
    public int? ServiceActionTileY { get; set; }

    [JsonPropertyName("robin_present_at_service")]
    public bool RobinPresentAtService { get; set; }

    [JsonPropertyName("service_status")]
    public string ServiceStatus { get; set; } = string.Empty;

    [JsonPropertyName("native_available_renovation_ids")]
    public string[] NativeAvailableRenovationIds { get; set; } = System.Array.Empty<string>();

    [JsonPropertyName("options")]
    public List<HomeRenovationOptionRef> Options { get; set; } = new();

    [JsonPropertyName("native_contract")]
    public string NativeContract { get; set; } = string.Empty;
}

public sealed class HomeRenovationOptionRef
{
    [JsonPropertyName("renovation_id")]
    public string RenovationId { get; set; } = string.Empty;

    [JsonPropertyName("display_name")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("placement_text")]
    public string PlacementText { get; set; } = string.Empty;

    [JsonPropertyName("price")]
    public int Price { get; set; }

    [JsonPropertyName("room_id")]
    public string RoomId { get; set; } = string.Empty;

    [JsonPropertyName("animation_type")]
    public string AnimationType { get; set; } = string.Empty;

    [JsonPropertyName("is_destructive")]
    public bool IsDestructive { get; set; }

    [JsonPropertyName("check_for_obstructions")]
    public bool CheckForObstructions { get; set; }

    [JsonPropertyName("special_rect")]
    public string SpecialRect { get; set; } = string.Empty;

    [JsonPropertyName("requirements")]
    public List<HomeRenovationValueRef> Requirements { get; set; } = new();

    [JsonPropertyName("renovate_actions")]
    public List<HomeRenovationValueRef> RenovateActions { get; set; } = new();

    [JsonPropertyName("regions")]
    public List<HomeRenovationRegionRef> Regions { get; set; } = new();

    [JsonPropertyName("requirements_satisfied")]
    public bool RequirementsSatisfied { get; set; }

    [JsonPropertyName("native_menu_available")]
    public bool NativeMenuAvailable { get; set; }

    [JsonPropertyName("native_shop_index")]
    public int? NativeShopIndex { get; set; }

    [JsonPropertyName("first_purchase_mail_id")]
    public string FirstPurchaseMailId { get; set; } = string.Empty;

    [JsonPropertyName("first_purchase_mail_before")]
    public bool FirstPurchaseMailBefore { get; set; }

    [JsonPropertyName("expected_first_purchase_mail_after")]
    public bool ExpectedFirstPurchaseMailAfter { get; set; }

    [JsonPropertyName("money_before")]
    public int MoneyBefore { get; set; }

    [JsonPropertyName("expected_money_after")]
    public int ExpectedMoneyAfter { get; set; }

    [JsonPropertyName("refund_eligible")]
    public bool RefundEligible { get; set; }

    [JsonPropertyName("availability_status")]
    public string AvailabilityStatus { get; set; } = string.Empty;

    [JsonPropertyName("availability_block_reasons")]
    public string[] AvailabilityBlockReasons { get; set; } = System.Array.Empty<string>();

    [JsonPropertyName("projection_fingerprint")]
    public string ProjectionFingerprint { get; set; } = string.Empty;
}

public sealed class HomeRenovationValueRef
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    [JsonPropertyName("value_expression")]
    public string ValueExpression { get; set; } = string.Empty;

    [JsonPropertyName("current_int_value")]
    public int? CurrentIntValue { get; set; }

    [JsonPropertyName("current_bool_value")]
    public bool? CurrentBoolValue { get; set; }

    [JsonPropertyName("satisfied")]
    public bool? Satisfied { get; set; }

    [JsonPropertyName("projection_status")]
    public string ProjectionStatus { get; set; } = string.Empty;
}

public sealed class HomeRenovationRegionRef
{
    [JsonPropertyName("selected_index")]
    public int SelectedIndex { get; set; }

    [JsonPropertyName("rectangles")]
    public List<HomeRenovationRectRef> Rectangles { get; set; } = new();

    [JsonPropertyName("obstruction_status")]
    public string ObstructionStatus { get; set; } = string.Empty;

    [JsonPropertyName("obstruction_reasons")]
    public string[] ObstructionReasons { get; set; } = System.Array.Empty<string>();

    [JsonPropertyName("blocked_tiles")]
    public string[] BlockedTiles { get; set; } = System.Array.Empty<string>();

    [JsonPropertyName("intersecting_furniture")]
    public string[] IntersectingFurniture { get; set; } = System.Array.Empty<string>();
}

public sealed class HomeRenovationRectRef
{
    [JsonPropertyName("x")]
    public int X { get; set; }

    [JsonPropertyName("y")]
    public int Y { get; set; }

    [JsonPropertyName("width")]
    public int Width { get; set; }

    [JsonPropertyName("height")]
    public int Height { get; set; }
}
