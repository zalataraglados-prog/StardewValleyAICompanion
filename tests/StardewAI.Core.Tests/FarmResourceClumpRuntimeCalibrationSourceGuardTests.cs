namespace StardewAI.Core.Tests;

public sealed class FarmResourceClumpRuntimeCalibrationSourceGuardTests
{
    [Fact]
    public void FixtureConstructsBothVanillaFarmClumpFamilies()
    {
        var fixture = ReadRepositoryFile(
            "tools",
            "StardewAI.RuntimeTestHarness",
            "ModEntry.FarmResourceClumpFixture.cs");

        Assert.Contains(
            "ResourceClump.stumpIndex",
            fixture,
            StringComparison.Ordinal);
        Assert.Contains(
            "ResourceClump.hollowLogIndex",
            fixture,
            StringComparison.Ordinal);
        Assert.Contains("new ResourceClump(", fixture, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "performToolAction",
            fixture,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            "gainExperience",
            fixture,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NativeExecutorRequiresAndVerifiesProjectedForagingExperience()
    {
        var runtime = ReadRepositoryFile(
            "tools",
            "StardewAI.RuntimeTestHarness",
            "ModEntry.MiningResources.cs");

        Assert.Contains(
            "request.ExpectedForagingExperienceDelta != 25",
            runtime,
            StringComparison.Ordinal);
        Assert.Contains(
            "farmRequest ? 25 : currentLocationRequest ? 15 : null",
            runtime,
            StringComparison.Ordinal);
        var experienceCheck = runtime.IndexOf(
            "active.ExpectedForagingExperienceDelta.HasValue",
            StringComparison.Ordinal);
        var emptyOutputReturn = runtime.IndexOf(
            "active.ExpectedOutputs.Length == 0",
            experienceCheck,
            StringComparison.Ordinal);
        Assert.True(
            experienceCheck >= 0 &&
            emptyOutputReturn > experienceCheck);
        Assert.Contains(
            "active.OutputCountsBefore[index] <\n                output.Quantity",
            runtime.Replace("\r\n", "\n", StringComparison.Ordinal),
            StringComparison.Ordinal);
    }

    [Fact]
    public void AllResourceClumpReadersIncludeNativeAdditionalToolPower()
    {
        var projection = ReadRepositoryFile(
            "src",
            "StardewAI.TransparentBridge",
            "Adapters",
            "NativeToolPowerProjection.cs");
        var farm = ReadRepositoryFile(
            "src",
            "StardewAI.TransparentBridge",
            "Adapters",
            "FarmReadAdapter.SkillExperience.cs");
        var current = ReadRepositoryFile(
            "src",
            "StardewAI.TransparentBridge",
            "Adapters",
            "CurrentLocationReadAdapter.ResourceClumps.cs");
        var mining = ReadRepositoryFile(
            "src",
            "StardewAI.TransparentBridge",
            "Adapters",
            "MiningReadAdapter.Objects.cs");

        Assert.Contains("axe.additionalPower.Value", projection);
        Assert.Contains("pickaxe.additionalPower.Value", projection);
        Assert.Contains(
            "NativeToolPowerProjection.ResourceClumpDamage",
            farm);
        Assert.Contains(
            "NativeToolPowerProjection.ResourceClumpDamage",
            current);
        Assert.Contains(
            "NativeToolPowerProjection.ResourceClumpDamage",
            mining);
    }

    [Fact]
    public void SmokeCoversStumpAndHollowLogThroughNativeExecutor()
    {
        var smoke = ReadRepositoryFile(
            "scripts",
            "Invoke-RuntimeFarmResourceClumpSmoke.ps1");

        Assert.Contains(
            "clear_kind = \"resource_stump\"",
            smoke,
            StringComparison.Ordinal);
        Assert.Contains(
            "clear_kind = \"hollow_log\"",
            smoke,
            StringComparison.Ordinal);
        Assert.Contains(
            "option_id = \"executor.break_farm_resource_clump\"",
            smoke,
            StringComparison.Ordinal);
        Assert.Contains(
            "expected_foraging_experience_delta",
            smoke,
            StringComparison.Ordinal);
        Assert.Contains(
            "foraging_xp_delta = $xpDelta",
            smoke,
            StringComparison.Ordinal);
        Assert.Contains(
            "-WindowStyle Hidden",
            smoke,
            StringComparison.Ordinal);
        Assert.Contains(
            "$env:SMAPI_MODS_PATH = $smokeModsPath",
            smoke,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"StardewAI.TransparentBridge\"",
            smoke,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"StardewAI.RuntimeTestHarness\"",
            smoke,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "\"JunimoTestClient\"",
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
