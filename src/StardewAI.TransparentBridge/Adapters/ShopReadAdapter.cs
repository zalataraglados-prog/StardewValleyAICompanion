namespace StardewAI.TransparentBridge.Adapters;

public sealed class ShopReadAdapter : ReadAdapterBase
{
    public override string Domain => "shops";
    public override int Priority => 50;

    public override StateAdapterResult Collect(long tick)
    {
        return Section("locations", new Dictionary<string, object>
        {
            ["shops"] = Unavailable("shop_catalog_adapter_not_implemented", "StardewValley shop data/menu state", tick),
            ["active_shop"] = Unavailable("active_shop_menu_read_not_implemented", "Game1.activeClickableMenu", tick)
        }, new[]
        {
            "shops.catalogs",
            "shops.prices",
            "shops.open_hours",
            "shops.active_menu"
        }, "unavailable");
    }
}
