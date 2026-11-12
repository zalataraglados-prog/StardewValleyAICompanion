using StardewAI.Contracts.Training;

namespace StardewAI.GoalConditionedBootstrap;

internal static class CropTeacherNativeStepPaths
{
    internal static string[] Read(MoneyRevenueCropNativeLifecycleTransitionRequest transition,
        QueueExecutionReceiptEnvelope receipt, string[] inputs, string label)
    {
        var comparer = CropTeacherSeedTransferNativeOutcome.PathComparer;
        var paths = new List<string>();
        var root = Path.GetDirectoryName(Path.GetFullPath(transition.ExecutionReceiptPath))! + Path.DirectorySeparatorChar;
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        for (var index = 0; index < receipt.StepResults.Length; index++)
        {
            var path = receipt.StepResults[index].AfterSnapshotPath;
            Require(!string.IsNullOrWhiteSpace(path) && Path.IsPathFullyQualified(path), label + " step snapshot path is missing or relative.");
            path = Path.GetFullPath(path!);
            Require(path.StartsWith(root, comparison) && !paths.Contains(path, comparer) &&
                    (!inputs.Contains(path, comparer) || index == receipt.StepResults.Length - 1 && comparer.Equals(path, Path.GetFullPath(transition.AfterSnapshotPath))),
                label + " step path escaped, repeated or aliased a retained input.");
            paths.Add(path);
        }
        return paths.ToArray();
    }
}
