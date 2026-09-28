using System.Text.RegularExpressions;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private static readonly Regex FreshSaveTokenPattern = new(
        "^[A-Za-z][A-Za-z0-9]{0,11}$",
        RegexOptions.CultureInvariant);

    private static string ReadEnvironmentOverride(string name, string fallback)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    private bool TryValidateFreshSaveConfiguration(out string error)
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable("STARDEWAI_TRAINING_MODE"),
                "1",
                StringComparison.Ordinal))
        {
            error = "training_mode_not_enabled";
            return false;
        }

        var requestedSavesPath = Environment.GetEnvironmentVariable(
            "STARDEWAI_TEST_SAVES");
        var isolationPath = Environment.GetEnvironmentVariable(
            "STARDEWAI_SAVE_ISOLATION_PATH");
        if (string.IsNullOrWhiteSpace(requestedSavesPath) ||
            string.IsNullOrWhiteSpace(isolationPath))
        {
            error = "explicit_isolation_paths_required";
            return false;
        }

        if (!PathsEqual(config.SavesPath, requestedSavesPath) ||
            !PathsEqual(config.SavesPath, isolationPath))
        {
            error = "isolation_path_mismatch";
            return false;
        }

        var root = Path.GetPathRoot(config.SavesPath);
        if (string.IsNullOrWhiteSpace(root) || PathsEqual(config.SavesPath, root))
        {
            error = "drive_root_rejected";
            return false;
        }

        var directoryName = Path.GetFileName(
            config.SavesPath.TrimEnd(Path.DirectorySeparatorChar));
        if (!directoryName.StartsWith("fresh-save-", StringComparison.Ordinal))
        {
            error = "dedicated_fresh_save_directory_required";
            return false;
        }

        if ((File.GetAttributes(config.SavesPath) & FileAttributes.ReparsePoint) != 0)
        {
            error = "reparse_point_rejected";
            return false;
        }

        if (Directory.EnumerateFileSystemEntries(config.SavesPath).Any())
        {
            error = "fresh_save_directory_not_empty";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(config.SlotName))
        {
            error = "fresh_save_and_existing_slot_are_mutually_exclusive";
            return false;
        }

        if (!IsFreshSaveToken(config.FreshPlayerName) ||
            !IsFreshSaveToken(config.FreshFarmName) ||
            !IsFreshSaveToken(config.FreshFavoriteThing))
        {
            error = "fresh_save_identity_must_be_ascii_alphanumeric";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private void StartNativeFreshSave()
    {
        if (Game1.activeClickableMenu is not TitleMenu titleMenu)
        {
            Monitor.Log(
                "Fresh-save creation rejected: the active menu is not the native title menu.",
                LogLevel.Error);
            return;
        }

        Game1.resetPlayer();
        Game1.whichFarm = 0;
        Game1.whichModFarm = null;
        Game1.spawnMonstersAtNight = false;
        Game1.startingCabins = 0;
        Game1.cabinsSeparate = false;
        Game1.player.difficultyModifier = 1f;
        Game1.player.team.useSeparateWallets.Value = false;
        Game1.player.Name = config.FreshPlayerName;
        Game1.player.displayName = config.FreshPlayerName;
        Game1.player.farmName.Value = config.FreshFarmName;
        Game1.player.favoriteThing.Value = config.FreshFavoriteThing;
        Game1.player.isCustomized.Value = true;
        Game1.player.ConvertClothingOverrideToClothesItems();

        Monitor.Log(
            $"Creating isolated native fresh save for {config.FreshPlayerName} on {config.FreshFarmName}.",
            LogLevel.Info);
        titleMenu.createdNewCharacter(skipIntro: true);
    }

    private static bool IsFreshSaveToken(string value) =>
        FreshSaveTokenPattern.IsMatch(value);

    private static bool PathsEqual(string left, string right) =>
        string.Equals(
            Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar),
            Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase);
}
