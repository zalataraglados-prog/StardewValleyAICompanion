namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    internal static void RunMachineMaterialTransferFixture(string outputRoot) =>
        new StageOneCollectionFrontierFixture(outputRoot).RunMachineMaterialTransferProof();

    private sealed partial class StageOneCollectionFrontierFixture
    {
        public void RunMachineMaterialTransferProof()
        {
            var calendar = BuildStaticInputs();
            _ = VerifyTargetDateAxes(calendar);
            var template = new AcquisitionRouteExecutionBindingInputs
            {
                RequirementInventoryPath = inventoryPath, AcquisitionLoweringPath = loweringPath,
                MasterAnglerWindowsPath = windowsPath, CalendarResolutionPath = routeCalendarPath,
                BeforeSnapshotPath = targetDateUnlockSnapshotPath, RouteTimingCalibrationPath = targetDateRouteCalibrationPath,
                FishingForecastManifestPath = fishingForecastManifestPath
            };
            _ = VerifyMachineMaterialTransferSupportingTransitionProof(template);
            Console.WriteLine("Canonical machine material-transfer terminal proof passed (synthetic, not native gameplay).");
        }
    }
}
