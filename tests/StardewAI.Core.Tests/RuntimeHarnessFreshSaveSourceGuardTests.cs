namespace StardewAI.Core.Tests;

public sealed class RuntimeHarnessFreshSaveSourceGuardTests
{
    [Fact]
    public void FreshSaveModeRequiresExplicitEmptyIsolatedTrainingRoot()
    {
        var source = RuntimeHarnessSources.LoadFile("ModEntry.FreshSave.cs");

        Assert.Contains("STARDEWAI_TRAINING_MODE", source, StringComparison.Ordinal);
        Assert.Contains("STARDEWAI_TEST_SAVES", source, StringComparison.Ordinal);
        Assert.Contains("STARDEWAI_SAVE_ISOLATION_PATH", source, StringComparison.Ordinal);
        Assert.Contains("fresh-save-", source, StringComparison.Ordinal);
        Assert.Contains("FileAttributes.ReparsePoint", source, StringComparison.Ordinal);
        Assert.Contains("Directory.EnumerateFileSystemEntries", source, StringComparison.Ordinal);
        Assert.Contains("fresh_save_and_existing_slot_are_mutually_exclusive", source, StringComparison.Ordinal);
    }

    [Fact]
    public void FreshSaveModeUsesCurrentNativeTitleFlowWithoutFabricatingSaveXml()
    {
        var source = RuntimeHarnessSources.LoadFile("ModEntry.FreshSave.cs");

        Assert.Contains("Game1.resetPlayer()", source, StringComparison.Ordinal);
        Assert.Contains("titleMenu.createdNewCharacter(skipIntro: true)", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SaveGame.Load", source, StringComparison.Ordinal);
        Assert.DoesNotContain("XmlSerializer", source, StringComparison.Ordinal);
        Assert.DoesNotContain("File.Write", source, StringComparison.Ordinal);
    }

    [Fact]
    public void FreshSaveSmokeRunsHiddenAndRequiresNativeSaveFiles()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root,
            "scripts",
            "Invoke-RuntimeFreshSaveSmoke.ps1"));

        Assert.Contains("-WindowStyle Hidden", source, StringComparison.Ordinal);
        Assert.Contains("STARDEWAI_TEST_CREATE_FRESH_SAVE", source, StringComparison.Ordinal);
        Assert.Contains("STARDEWAI_SAVE_ISOLATION_PATH", source, StringComparison.Ordinal);
        Assert.Contains("SaveGameInfo", source, StringComparison.Ordinal);
        Assert.Contains(
            "-TimeoutSeconds $StartupTimeoutSeconds",
            source,
            StringComparison.Ordinal);
        Assert.Contains("full_shipment_shipped_item_count = 0", source, StringComparison.Ordinal);
        Assert.Contains("$env:SMAPI_MODS_PATH = $smokeModsPath", source, StringComparison.Ordinal);
        Assert.Contains("loaded_mod_allowlist = $loadedModAllowlist", source, StringComparison.Ordinal);
        Assert.Contains("$harnessConfig.SlotName = \"\"", source, StringComparison.Ordinal);
        Assert.Contains("StardewAI.TransparentBridge", source, StringComparison.Ordinal);
        Assert.Contains("StardewAI.RuntimeTestHarness", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Remove-Item", source, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        while (directory is not null &&
               !File.Exists(Path.Combine(
                   directory.FullName,
                   "StardewValleyAICompanion.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Cannot find repository root.");
    }
}
