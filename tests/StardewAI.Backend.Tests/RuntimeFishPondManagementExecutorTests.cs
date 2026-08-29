using System.Text.Json;
using StardewAI.Contracts.Training;

namespace StardewAI.Backend.Tests;

public sealed class RuntimeFishPondManagementExecutorTests
{
    [Fact]
    public void FishPondManagementRequestRoundTripsTypedResetAndPreservationReceipt()
    {
        var request = new TrainingExecutionRequest
        {
            OptionId = "fishing.manage_fish_pond",
            ManagementOperation = "empty_pond",
            FishPondManagementReason = "explicit player request",
            ConfirmEmptyPond = true,
            ExpectedFishCount = 3,
            ExpectedFishCountAfter = 0,
            ExpectedMaximumOccupantsBefore = 5,
            ExpectedMaximumOccupantsAfter = 5,
            ExpectedNeededItemQualifiedItemIdBefore = "(O)72",
            ExpectedNeededItemCountBefore = 2,
            ExpectedNeededItemCountAfter = -1,
            ExpectedHasCompletedRequestBefore = 1,
            ExpectedHasCompletedRequestAfter = 1,
            ExpectedGoldenAnimalCrackerBefore = 1,
            ExpectedGoldenAnimalCrackerAfter = 0,
            ExpectedFishDebrisQualifiedItemId = "(O)698",
            ExpectedFishDebrisCount = 3,
            ExpectedNettingStyleBefore = 2,
            ExpectedNettingStyleAfter = 2
        };
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);

        var result = JsonSerializer.Deserialize<TrainingExecutionRequest>(
            JsonSerializer.Serialize(request, options), options)!;

        Assert.Equal("empty_pond", result.ManagementOperation);
        Assert.True(result.ConfirmEmptyPond);
        Assert.Equal(5, result.ExpectedMaximumOccupantsAfter);
        Assert.Equal(1, result.ExpectedHasCompletedRequestAfter);
        Assert.Equal(0, result.ExpectedGoldenAnimalCrackerAfter);
        Assert.Equal("(O)698", result.ExpectedFishDebrisQualifiedItemId);
        Assert.Equal(3, result.ExpectedFishDebrisCount);
    }

    [Fact]
    public void ProductionPathUsesSharedMovementAndNativePondQueryMenuWithoutDirectPondWrites()
    {
        var all = RuntimeHarnessSources.All;
        var source = RuntimeHarnessSources.File("ModEntry.FishPondManagement.cs");
        var productionStart = source.IndexOf("private void StartFishPondManagement", StringComparison.Ordinal);
        Assert.True(productionStart >= 0);
        var production = source[productionStart..];

        Assert.Contains("StartFishPondManagement(pending);", all);
        Assert.Contains("AdvanceNativeObjectInteractionMovement(active, \"fish_pond_management\"", production);
        Assert.Contains("NativeRightClickEdgePatch.Arm();", production);
        Assert.Contains("active.Location.checkAction(", production);
        Assert.Contains("Game1.activeClickableMenu is PondQueryMenu", production);
        Assert.Contains("menu.receiveLeftClick(menu.changeNettingButton.bounds.Center.X", production);
        Assert.Contains("menu.receiveLeftClick(menu.emptyButton.bounds.Center.X", production);
        Assert.Contains("menu.receiveLeftClick(menu.yesButton.bounds.Center.X", production);
        Assert.Contains("PondQueryBoundPondField?.GetValue(openedPondMenu)", production);
        Assert.DoesNotMatch(@"active\.Pond\.[A-Za-z0-9_]+\.Value\s*=(?!=)", production);
        Assert.DoesNotContain("ClearPond()", production, StringComparison.Ordinal);
        Assert.DoesNotContain("new PondQueryMenu", production, StringComparison.Ordinal);
    }

    [Fact]
    public void TransparentBridgePublishesPondManagementAndBoundMenuState()
    {
        var pond = RuntimeHarnessSources.RepositoryFile(
            "src", "StardewAI.TransparentBridge", "Adapters", "FarmReadAdapter.FishPonds.cs");
        var menu = RuntimeHarnessSources.RepositoryFile(
            "src", "StardewAI.TransparentBridge", "Adapters", "MenuReadAdapter.cs");

        Assert.Contains("management_invocation_policy = \"player_command_only\"", pond);
        Assert.Contains("management_empty_expected_fish_debris_count = pond.FishCount", pond);
        Assert.Contains("management_empty_expected_maximum_occupants_after = pond.maxOccupants.Value", pond);
        Assert.Contains("PondQueryMenu pondQueryMenu =>", menu);
        Assert.Contains("ReadPondQueryMenuState(pondQueryMenu)", menu);
        Assert.Contains("fish_count = pond?.FishCount", menu);
    }

    [Fact]
    public void LiveTrainingMappingKeepsFishPondFieldsInItsOwnedPartial()
    {
        var dispatch = RuntimeHarnessSources.RepositoryFile(
            "tools", "StardewAI.LiveTrainingLoop", "Program.RuntimeExecution.cs");
        var mapping = RuntimeHarnessSources.RepositoryFile(
            "tools", "StardewAI.LiveTrainingLoop", "Program.RuntimeExecution.FishPondManagement.cs");

        Assert.Contains("ApplyFishPondManagementRequestFields(", dispatch);
        Assert.Contains("request.OptionId, \"fishing.manage_fish_pond\"", mapping);
        Assert.Contains("request.FishPondManagementReason = ReadQueueParameterString(item, \"management_reason\")", mapping);
        Assert.Contains("request.ExpectedOverrideWaterColorPackedBefore = ReadQueueParameterLong", mapping);
        Assert.DoesNotContain("var managementOperation =", dispatch);
    }
}
