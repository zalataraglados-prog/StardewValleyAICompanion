namespace StardewAI.Core.Tests;

public sealed class MachineHarvestExperienceRuntimeSourceGuardTests
{
    [Fact]
    public void FixtureCanExerciseNativeAndSyntheticMachineExperience()
    {
        var fixture = ReadRepositoryFile(
            "tools",
            "StardewAI.RuntimeTestHarness",
            "ModEntry.MachinesAndPickup.cs");
        var skillFixture = ReadRepositoryFile(
            "tools",
            "StardewAI.RuntimeTestHarness",
            "MachineHarvestExperienceFixture.cs");

        Assert.Contains(
            "DataLoader.Machines(Game1.content)",
            fixture,
            StringComparison.Ordinal);
        Assert.Contains(
            "ExperienceGainOnHarvest",
            fixture,
            StringComparison.Ordinal);
        Assert.Contains(
            "FixtureMachineHarvestExperienceOverride",
            fixture,
            StringComparison.Ordinal);
        Assert.Contains(
            "mastery_threshold_order",
            skillFixture,
            StringComparison.Ordinal);
        Assert.Contains(
            "MachineHarvestExperienceFixture.ApplySkillProfile",
            fixture,
            StringComparison.Ordinal);
    }

    [Fact]
    public void NativeCollectReprojectsAndVerifiesSkillAndMasteryDeltas()
    {
        var runtime = ReadRepositoryFile(
            "tools",
            "StardewAI.RuntimeTestHarness",
            "ModEntry.MachinesAndPickup.cs");

        Assert.Contains(
            "TryProjectMachineHarvestExperience(machine",
            runtime,
            StringComparison.Ordinal);
        Assert.Contains(
            "actualExperience.SequenceEqual(expectedExperience)",
            runtime,
            StringComparison.Ordinal);
        Assert.Contains(
            "actualMasteryDelta == expectedMasteryDelta",
            runtime,
            StringComparison.Ordinal);
        Assert.Contains(
            "var acted = machine.checkForAction(Game1.player);",
            runtime,
            StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeCoversNativeParsingSinksAndMasteryOrder()
    {
        var smoke = ReadRepositoryFile(
            "scripts",
            "Invoke-RuntimeMachineOutputSmoke.ps1");

        foreach (var caseName in new[]
        {
            "native_configured",
            "no_configured_experience",
            "multi_skill_sink_and_invalid",
            "mastery_threshold_order"
        })
        {
            Assert.Contains(
                "name = \"" + caseName + "\"",
                smoke,
                StringComparison.Ordinal);
        }
        Assert.Contains(
            "expected_skill_experience_deltas_json",
            smoke,
            StringComparison.Ordinal);
        Assert.Contains(
            "expected_mastery_experience_delta",
            smoke,
            StringComparison.Ordinal);
        Assert.Contains("-WindowStyle Hidden", smoke);
        Assert.Contains(
            "$env:SMAPI_MODS_PATH = $smokeModsPath",
            smoke,
            StringComparison.Ordinal);
    }

    private static string ReadRepositoryFile(params string[] segments)
    {
        var directory = new DirectoryInfo(
            AppDomain.CurrentDomain.BaseDirectory);
        while (directory is not null &&
            !File.Exists(
                Path.Combine(
                    directory.FullName,
                    "StardewValleyAICompanion.sln")))
        {
            directory = directory.Parent;
        }

        return File.ReadAllText(
            Path.Combine(
                directory?.FullName ??
                    throw new InvalidOperationException(
                        "Cannot find repository root."),
                Path.Combine(segments)));
    }
}
