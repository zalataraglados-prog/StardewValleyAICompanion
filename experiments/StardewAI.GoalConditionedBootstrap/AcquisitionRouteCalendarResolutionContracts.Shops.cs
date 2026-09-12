using System.Text.Json.Serialization;

namespace StardewAI.GoalConditionedBootstrap;

public sealed record AcquisitionShopSourceEvidence(
    [property: JsonPropertyName("shop_id")] string ShopId,
    [property: JsonPropertyName("stock_row_index")] int StockRowIndex,
    [property: JsonPropertyName("stock_id")] string StockId,
    [property: JsonPropertyName("source_row_sha256")] string SourceRowSha256,
    [property: JsonPropertyName("currency")] int Currency,
    [property: JsonPropertyName("data_item_qualified_id")] string DataItemQualifiedId,
    [property: JsonPropertyName("random_item_id")] string? RandomItemId,
    [property: JsonPropertyName("price")] int Price,
    [property: JsonPropertyName("available_stock")] int AvailableStock,
    [property: JsonPropertyName("available_stock_limit")] int AvailableStockLimit,
    [property: JsonPropertyName("trade_item_id")] string? TradeItemId,
    [property: JsonPropertyName("trade_item_amount")] int TradeItemAmount,
    [property: JsonPropertyName("is_recipe")] bool IsRecipe,
    [property: JsonPropertyName("condition")] string? Condition,
    [property: JsonPropertyName("per_item_condition")] string? PerItemCondition,
    [property: JsonPropertyName("avoid_repeat")] bool AvoidRepeat,
    [property: JsonPropertyName("use_object_data_price")] bool UseObjectDataPrice,
    [property: JsonPropertyName("apply_profit_margins")] bool? ApplyProfitMargins,
    [property: JsonPropertyName("ignore_shop_price_modifiers")] bool IgnoreShopPriceModifiers,
    [property: JsonPropertyName("actions_on_purchase")] string[] ActionsOnPurchase,
    [property: JsonPropertyName("native_condition_handlers_complete")] bool NativeConditionHandlersComplete,
    [property: JsonPropertyName("requires_item_query_resolution")] bool RequiresItemQueryResolution,
    [property: JsonPropertyName("requires_price_modifier_resolution")] bool RequiresPriceModifierResolution,
    [property: JsonPropertyName("requires_stock_modifier_resolution")] bool RequiresStockModifierResolution,
    [property: JsonPropertyName("requires_owner_schedule_resolution")] bool RequiresOwnerScheduleResolution,
    [property: JsonPropertyName("requires_location_access_resolution")] bool RequiresLocationAccessResolution,
    [property: JsonPropertyName("requires_live_stock_receipt")] bool RequiresLiveStockReceipt,
    [property: JsonPropertyName("owners")] AcquisitionShopOwnerEvidence[] Owners,
    [property: JsonPropertyName("interaction_endpoints")] AcquisitionShopInteractionEndpointEvidence[] InteractionEndpoints,
    [property: JsonPropertyName("door_windows")] AcquisitionShopDoorWindowEvidence[] DoorWindows);

public sealed record AcquisitionShopOwnerEvidence(
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("type")] int Type,
    [property: JsonPropertyName("condition")] string? Condition);

public sealed record AcquisitionShopInteractionEndpointEvidence(
    [property: JsonPropertyName("map_asset")] string MapAsset,
    [property: JsonPropertyName("layer")] string Layer,
    [property: JsonPropertyName("x")] int X,
    [property: JsonPropertyName("y")] int Y,
    [property: JsonPropertyName("handler_key")] string HandlerKey,
    [property: JsonPropertyName("raw_action")] string RawAction,
    [property: JsonPropertyName("resolution")] string Resolution);

public sealed record AcquisitionShopDoorWindowEvidence(
    [property: JsonPropertyName("map_asset")] string MapAsset,
    [property: JsonPropertyName("x")] int X,
    [property: JsonPropertyName("y")] int Y,
    [property: JsonPropertyName("destination_location")] string DestinationLocation,
    [property: JsonPropertyName("destination_x")] int DestinationX,
    [property: JsonPropertyName("destination_y")] int DestinationY,
    [property: JsonPropertyName("open_time")] int OpenTime,
    [property: JsonPropertyName("close_time")] int CloseTime,
    [property: JsonPropertyName("required_npc")] string? RequiredNpc,
    [property: JsonPropertyName("minimum_friendship")] int MinimumFriendship,
    [property: JsonPropertyName("raw_action")] string RawAction);
