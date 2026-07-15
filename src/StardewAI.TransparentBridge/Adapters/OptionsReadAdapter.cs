using StardewValley;

namespace StardewAI.TransparentBridge.Adapters;

public sealed class OptionsReadAdapter : ReadAdapterBase
{
    private readonly bool applyAiControlSettings;

    public OptionsReadAdapter(bool applyAiControlSettings)
    {
        this.applyAiControlSettings = applyAiControlSettings;
    }

    public override string Domain => "options";
    public override int Priority => 25;

    public override StateAdapterResult Collect(long tick)
    {
        var options = Game1.options;
        return Section("options", new Dictionary<string, object>
        {
            ["auto_run"] = Field(options?.autoRun, "Game1.options.autoRun", tick),
            ["stowing_mode"] = Field(options?.stowingMode.ToString(), "Game1.options.stowingMode", tick),
            ["allow_stowing"] = Field(options?.allowStowing, "Game1.options.allowStowing", tick),
            ["gamepad_mode"] = Field(options?.gamepadMode.ToString(), "Game1.options.gamepadMode", tick),
            ["gamepad_controls"] = Field(options?.gamepadControls, "Game1.options.gamepadControls", tick),
            ["snappy_menus"] = Field(options?.snappyMenus, "Game1.options.snappyMenus", tick),
            ["snappy_menus_effective"] = Field(options?.SnappyMenus, "Game1.options.SnappyMenus", tick),
            ["invert_toolbar_scroll_direction"] = Field(options?.invertScrollDirection, "Game1.options.invertScrollDirection", tick),
            ["pause_when_out_of_focus"] = Field(options?.pauseWhenOutOfFocus, "Game1.options.pauseWhenOutOfFocus", tick),
            ["ai_control_settings"] = Field(new
            {
                requested = applyAiControlSettings,
                recommended = new
                {
                    auto_run = true,
                    stowing_mode = "Off",
                    gamepad_mode = "ForceOff",
                    snappy_menus = false,
                    invert_toolbar_scroll_direction = false,
                    pause_when_out_of_focus = false
                },
                rationale = "deterministic_keyboard_mouse_executor_contract"
            }, "StardewAI recommended AI control settings", tick)
        });
    }
}
