namespace StardewAI.Core.Tests;

public sealed class PetCareRuntimeSourceGuardTests
{
    private static readonly string RuntimeSource = RuntimeHarnessSources.All;
    private static readonly string SmokeSource = File.ReadAllText(
        FindRepositoryFile("scripts", "Invoke-RuntimePetCareSmoke.ps1"));

    [Fact]
    public void ProductionInteractionUsesNativeCheckActionAndBoundingBoxReachWithoutDirectOutcomeMutation()
    {
        var source = Slice(RuntimeSource, "private void StartPetInteraction", "private void StartFillPetBowl");

        Assert.Contains("active.Pet.checkAction(Game1.player, active.Location)", source, StringComparison.Ordinal);
        Assert.Contains("PetInteractionStandTiles", source, StringComparison.Ordinal);
        Assert.Contains("TryFacePetForInteraction", source, StringComparison.Ordinal);
        Assert.Contains("pet.GetBoundingBox()", source, StringComparison.Ordinal);
        Assert.Empty(System.Text.RegularExpressions.Regex.Matches(source, @"friendshipTowardFarmer\.Value\s*=(?!=)"));
        Assert.Empty(System.Text.RegularExpressions.Regex.Matches(source, @"timesPet\.Value\s*(?:\+\+|=(?!=))"));
        Assert.DoesNotContain("mailReceived.Add", source, StringComparison.Ordinal);
        Assert.DoesNotContain("createMultipleItemDebris", source, StringComparison.Ordinal);
    }

    [Fact]
    public void BowlUsesNativeWateringAndDurableDayStartedSettlementReceipt()
    {
        var bowlSource = Slice(RuntimeSource, "private void StartFillPetBowl", "private static PetBowl? FindPetBowlAtActionTile");

        Assert.Contains("ActiveNativeTool.WaterPetBowl", bowlSource, StringComparison.Ordinal);
        Assert.Contains("WritePetBowlPendingReceipt", bowlSource, StringComparison.Ordinal);
        Assert.Contains("pending_Pet.dayUpdate", bowlSource, StringComparison.Ordinal);
        Assert.DoesNotContain("friendshipTowardFarmer.Value =", bowlSource, StringComparison.Ordinal);
        Assert.DoesNotContain("bowl.watered.Value = true", bowlSource, StringComparison.Ordinal);
        Assert.Contains("OnDayStartedForPetBowlReceipts", RuntimeSource, StringComparison.Ordinal);
        Assert.Contains("native_Pet.dayUpdate_exact_friendship_bowl_and_mail_settlement", RuntimeSource, StringComparison.Ordinal);
        Assert.Contains("delayed_pet_bowl_feedback.jsonl", RuntimeSource, StringComparison.Ordinal);
    }

    [Fact]
    public void TransparentProjectionKeepsMaximumFriendshipGiftOpportunityAndExactBaseTypes()
    {
        var allSource = File.ReadAllText(FindRepositoryFile(
            "src", "StardewAI.TransparentBridge", "Adapters", "FarmReadAdapter.Pets.cs"));
        var source = Slice(allSource, "private static object[] ReadPets", "private static object[] ReadPetBowls");

        Assert.Contains("Utility.CreateDaySaveRandom", source, StringComparison.Ordinal);
        Assert.Contains("pet.GetType() == typeof(Pet)", source, StringComparison.Ordinal);
        Assert.Contains("entry.Bowl.GetType() != typeof(PetBowl)", allSource, StringComparison.Ordinal);
        Assert.Contains("can.GetType() != typeof(WateringCan)", allSource, StringComparison.Ordinal);
        Assert.DoesNotContain("pet_love_already_maximum", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeCoversBothInteractionBranchesAndNativeNextDayBowlSettlement()
    {
        Assert.Contains("Invoke-PetInteractionCase \"pet-interaction-normal\" 500 $false", SmokeSource, StringComparison.Ordinal);
        Assert.Contains("Invoke-PetInteractionCase \"pet-interaction-max-gift\" 1000 $true", SmokeSource, StringComparison.Ordinal);
        Assert.Contains("Invoke-PetBowlSettlementCase", SmokeSource, StringComparison.Ordinal);
        Assert.Contains("executor.sleep", SmokeSource, StringComparison.Ordinal);
        Assert.Contains("receipt.status -ne \"completed\"", SmokeSource, StringComparison.Ordinal);
        Assert.Contains("EVD-223", SmokeSource, StringComparison.Ordinal);
        Assert.Contains("Start-Process -FilePath $smapiExe -WorkingDirectory $gameDir -WindowStyle Hidden", SmokeSource, StringComparison.Ordinal);
        Assert.Contains("Refusing to attach", SmokeSource, StringComparison.Ordinal);
    }

    private static string Slice(string source, string start, string end)
    {
        var startIndex = source.IndexOf(start, StringComparison.Ordinal);
        var endIndex = source.IndexOf(end, startIndex + start.Length, StringComparison.Ordinal);
        Assert.True(startIndex >= 0 && endIndex > startIndex);
        return source.Substring(startIndex, endIndex - startIndex);
    }

    private static string FindRepositoryFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "StardewValleyAICompanion.sln")))
        {
            directory = directory.Parent;
        }
        Assert.NotNull(directory);
        return Path.Combine(new[] { directory!.FullName }.Concat(parts).ToArray());
    }
}
