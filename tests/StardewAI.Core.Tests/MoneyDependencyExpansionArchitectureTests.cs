using System.Text.Json;
using StardewAI.Contracts.Capabilities;
using StardewAI.Contracts.Options;

namespace StardewAI.Core.Tests;

public sealed class MoneyDependencyExpansionArchitectureTests
{
    private static readonly string DependencyPath = FindRepositoryFile(
        "experiments",
        "goal-method-dependency-expansions.v1.json");

    [Fact]
    public void CropProductionRemainsCompilerOwnedDeterministicSupport()
    {
        using var document = JsonDocument.Parse(
            File.ReadAllText(DependencyPath));
        var direction = document.RootElement.GetProperty("directions")
            .EnumerateArray()
            .Single(row => row.GetProperty("direction_id").GetString() ==
                "earn_money");
        var blockers = direction.GetProperty("blockers")
            .EnumerateArray()
            .Select(row => row.GetString() ?? string.Empty)
            .ToArray();
        var cropNodes = direction.GetProperty("nodes")
            .EnumerateArray()
            .Where(row => row.TryGetProperty("option_id", out var optionId) &&
                optionId.GetString() == "farm.maintain_crops")
            .ToArray();

        var cropNode = Assert.Single(cropNodes);
        Assert.Equal("deterministic_transition",
            cropNode.GetProperty("kind").GetString());
        Assert.Equal(
            new[]
            {
                "water_crop_tile",
                "plant_seed_tile",
                "apply_fertilizer_tile",
                "harvest_crop_tile",
                "harvest_giant_crop_tile"
            },
            cropNode.GetProperty("candidate_kinds")
                .EnumerateArray()
                .Select(row => row.GetString()));
        Assert.DoesNotContain(blockers, blocker =>
            blocker.Contains("training-eligible", StringComparison.Ordinal));

        var capability = OptionCapabilityRegistrySource.GetRequired(
            "farm.maintain_crops");
        Assert.Equal(OptionRuntimeStatus.RuntimeVerified,
            capability.RuntimeEvidenceStatus);
        Assert.False(TrainingEligibilityPolicy.IsEligible(capability));
        Assert.Equal(OptionTrainingEligibility.EvaluationOnly,
            capability.TrainingEligibility);
    }

    [Fact]
    public void CropSupportConnectsRecurrenceToReserveWithoutBecomingPolicy()
    {
        using var document = JsonDocument.Parse(
            File.ReadAllText(DependencyPath));
        var direction = document.RootElement.GetProperty("directions")
            .EnumerateArray()
            .Single(row => row.GetProperty("direction_id").GetString() ==
                "earn_money");
        var edges = direction.GetProperty("edges").EnumerateArray().ToArray();

        Assert.Contains(edges, edge =>
            edge.GetProperty("from").GetString() ==
                "dependency.earn_money.revenue_portfolio_recurrence" &&
            edge.GetProperty("to").GetString() ==
                "dependency.earn_money.maintain_crop_production");
        Assert.Contains(edges, edge =>
            edge.GetProperty("from").GetString() ==
                "dependency.earn_money.maintain_crop_production" &&
            edge.GetProperty("to").GetString() ==
                "dependency.earn_money.reserve_safe_positive_inventory");
        Assert.DoesNotContain(
            direction.GetProperty("nodes").EnumerateArray(),
            node => node.GetProperty("kind").GetString() ==
                    "policy_option_transition" &&
                node.TryGetProperty("option_id", out var optionId) &&
                optionId.GetString() == "farm.maintain_crops");
    }

    private static string FindRepositoryFile(params string[] segments)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var path = Path.Combine(
                new[] { directory.FullName }.Concat(segments).ToArray());
            if (File.Exists(path))
                return path;
            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            "Repository file was not found: " + Path.Combine(segments));
    }
}
