namespace StardewAI.GoalConditionedBootstrap;

public sealed partial class CropTeacherSiteTillCompilation
{
    private static T Read<T>(string path) => CurrentTeacherFrontierSupport.Read<T>(path, "selected original till input");
    private static bool SamePath(string left, string right) => CropTeacherSeedTransferNativeOutcome.PathComparer.Equals(
        Path.GetFullPath(left), Path.GetFullPath(right));
    private static void Check(bool valid, string message)
    {
        if (!valid) throw new InvalidDataException(message);
    }

    private static CropTeacherRetainedInputs RetainDependencies(string[] explicitPaths, string productionPath)
    {
        // Pin each discovery document before following it, then validate discovery again.
        var planPath = Path.GetFullPath(productionPath);
        var planPin = new CropTeacherRetainedInputs(new[] { planPath });
        var plan = Read<MoneyRevenueCropProductionObligationPlan>(planPath);
        var requestPath = Path.GetFullPath(plan.RequestPath);
        var requestPin = new CropTeacherRetainedInputs(new[] { requestPath });
        var request = Read<MoneyRevenueCropProductionObligationPlanRequest>(requestPath);
        var paths = explicitPaths.Concat(new[] { planPath, requestPath, plan.SnapshotPath, plan.StrategyLedgerPath,
            request.SnapshotPath, request.StrategyLedgerPath }).Select(Path.GetFullPath)
            .Distinct(CropTeacherSeedTransferNativeOutcome.PathComparer).ToArray();
        var retained = new CropTeacherRetainedInputs(paths);
        planPin.Validate(); requestPin.Validate();
        return retained;
    }
}
