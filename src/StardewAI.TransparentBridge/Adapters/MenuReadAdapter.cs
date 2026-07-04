using System.Reflection;
using StardewValley;
using StardewValley.Menus;

namespace StardewAI.TransparentBridge.Adapters;

public sealed class MenuReadAdapter : ReadAdapterBase
{
    private const string AdapterId = "vanilla_1_6_menu";

    private static readonly string[] MenuFields =
    {
        "active_menu",
        "identity",
        "screen_bounds",
        "public_state",
        "menu_specific_state"
    };

    public override string Domain => "menus";
    public override int Priority => 35;

    public override StateAdapterResult Collect(long tick)
    {
        var menu = Game1.activeClickableMenu;
        if (menu is null)
        {
            return Section("menus", new Dictionary<string, object>
            {
                ["active_menu"] = Field(new
                {
                    is_open = false,
                    type = "none",
                    full_type = (string?)null
                }, "Game1.activeClickableMenu", tick, AdapterId),
                ["identity"] = Unavailable("no_active_clickable_menu", "Game1.activeClickableMenu", tick, AdapterId),
                ["screen_bounds"] = Unavailable("no_active_clickable_menu", "Game1.activeClickableMenu", tick, AdapterId),
                ["public_state"] = Unavailable("no_active_clickable_menu", "Game1.activeClickableMenu", tick, AdapterId),
                ["menu_specific_state"] = Unavailable("no_active_clickable_menu", "Game1.activeClickableMenu", tick, AdapterId)
            }, MenuFields.Skip(1).Select(field => "menus." + field).ToArray(), "partial");
        }

        return Section("menus", new Dictionary<string, object>
        {
            ["active_menu"] = Field(ReadActiveMenu(menu), "Game1.activeClickableMenu", tick, AdapterId),
            ["identity"] = Field(ReadIdentity(menu), "Game1.activeClickableMenu.GetType()", tick, AdapterId),
            ["screen_bounds"] = ReadScreenBounds(menu, tick),
            ["public_state"] = ReadPublicState(menu, tick),
            ["menu_specific_state"] = Unavailable(
                "menu_specific_fields_not_verified_in_this_slice",
                "Game1.activeClickableMenu concrete menu fields",
                tick,
                AdapterId)
        }, new[] { "menus.menu_specific_state" }, "partial");
    }

    private static object ReadActiveMenu(IClickableMenu menu)
    {
        var type = menu.GetType();
        return new
        {
            is_open = true,
            type = type.Name,
            full_type = type.FullName
        };
    }

    private static object ReadIdentity(IClickableMenu menu)
    {
        var type = menu.GetType();
        return new
        {
            type = type.Name,
            full_type = type.FullName,
            assembly = type.Assembly.GetName().Name,
            is_iclickable_menu = menu is IClickableMenu
        };
    }

    private static object ReadScreenBounds(IClickableMenu menu, long tick)
    {
        var x = TryReadPublicInt(menu, "xPositionOnScreen");
        var y = TryReadPublicInt(menu, "yPositionOnScreen");
        var width = TryReadPublicInt(menu, "width");
        var height = TryReadPublicInt(menu, "height");
        if (x is null || y is null || width is null || height is null)
        {
            return Unavailable(
                "iclickablemenu_public_bounds_fields_not_available",
                "IClickableMenu.xPositionOnScreen/yPositionOnScreen/width/height",
                tick,
                AdapterId);
        }

        return Field(new
        {
            x,
            y,
            width,
            height
        }, "IClickableMenu.xPositionOnScreen/yPositionOnScreen/width/height", tick, AdapterId);
    }

    private static object ReadPublicState(IClickableMenu menu, long tick)
    {
        var destroy = TryReadPublicBool(menu, "destroy");
        var invisible = TryReadPublicBool(menu, "invisible");
        var gameWindowSizeChanged = TryReadPublicBool(menu, "gameWindowSizeChanged");
        var closeButton = TryReadPublicField(menu, "upperRightCloseButton");
        var snappedComponent = TryReadPublicField(menu, "currentlySnappedComponent");
        var state = new Dictionary<string, object?>
        {
            ["destroy"] = destroy,
            ["invisible"] = invisible,
            ["game_window_size_changed"] = gameWindowSizeChanged,
            ["upper_right_close_button_present"] = closeButton is not null,
            ["currently_snapped_component_present"] = snappedComponent is not null
        };

        if (destroy is null &&
            invisible is null &&
            gameWindowSizeChanged is null &&
            !HasPublicField("upperRightCloseButton") &&
            !HasPublicField("currentlySnappedComponent"))
        {
            return Unavailable(
                "iclickablemenu_public_state_fields_not_available",
                "IClickableMenu public state fields",
                tick,
                AdapterId);
        }

        return Field(state, "IClickableMenu public state fields", tick, AdapterId);
    }

    private static int? TryReadPublicInt(IClickableMenu menu, string fieldName)
    {
        return TryReadPublicField(menu, fieldName) is int value ? value : null;
    }

    private static bool? TryReadPublicBool(IClickableMenu menu, string fieldName)
    {
        return TryReadPublicField(menu, fieldName) is bool value ? value : null;
    }

    private static object? TryReadPublicField(IClickableMenu menu, string fieldName)
    {
        var field = typeof(IClickableMenu).GetField(fieldName, BindingFlags.Instance | BindingFlags.Public);
        return field?.GetValue(menu);
    }

    private static bool HasPublicField(string fieldName)
    {
        return typeof(IClickableMenu).GetField(fieldName, BindingFlags.Instance | BindingFlags.Public) is not null;
    }
}
