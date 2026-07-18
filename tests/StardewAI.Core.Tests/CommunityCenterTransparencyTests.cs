using System.Text.Json;
using StardewAI.Contracts.State;

namespace StardewAI.Core.Tests;

public sealed class CommunityCenterTransparencyTests
{
    [Fact]
    public void ContractSeparatesRouteEvidenceAndAccountsForEveryBundleDataRow()
    {
        var progress = new CommunityCenterProgressRef
        {
            RouteState = "undecided",
            RouteStateReason = "neither_irreversible_route_flag_present",
            MaxGrandpaScoreRoute = "community_center",
            CommunityCenterCompleteFlagReceivedOrPending = false,
            CommunityCenterCompleteNative = false,
            BundleDataRowCount = 2,
            ProjectedBundleRowCount = 2,
            UnavailableBundleRowCount = 1,
            BundleRows = new[]
            {
                new CommunityCenterBundleProgressRef
                {
                    ProjectionStatus = "exact",
                    BundleDataKey = "Pantry/0"
                },
                new CommunityCenterBundleProgressRef
                {
                    ProjectionStatus = "unavailable",
                    ProjectionFailure = "bundle_ingredient_shape_or_completion_bits_invalid",
                    BundleDataKey = "FishTank/6"
                }
            }
        };

        var json = JsonSerializer.Serialize(progress, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Contains("\"route_state\":\"undecided\"", json, StringComparison.Ordinal);
        Assert.Contains("\"community_center_complete_flag_received_or_pending\":false", json, StringComparison.Ordinal);
        Assert.Contains("\"community_center_complete_native\":false", json, StringComparison.Ordinal);
        Assert.Contains("\"bundle_data_row_count\":2", json, StringComparison.Ordinal);
        Assert.Contains("\"projected_bundle_row_count\":2", json, StringComparison.Ordinal);
        Assert.Contains("\"unavailable_bundle_row_count\":1", json, StringComparison.Ordinal);
        Assert.Contains("\"projection_status\":\"unavailable\"", json, StringComparison.Ordinal);
        Assert.Contains("bundle_ingredient_shape_or_completion_bits_invalid", json, StringComparison.Ordinal);
    }

    [Fact]
    public void AdapterUsesLiveNativeBundleRulesAndFailsClosedWithoutProgressWrites()
    {
        var source = File.ReadAllText(FindRepositoryFile(
            "src", "StardewAI.TransparentBridge", "Adapters", "ProgressReadAdapter.cs"));

        Assert.Contains("world.BundleData", source, StringComparison.Ordinal);
        Assert.Contains("world.BundleData.Count", source, StringComparison.Ordinal);
        Assert.Contains("world.Bundles.Pairs", source, StringComparison.Ordinal);
        Assert.Contains("IsValidItemForThisIngredientDescription", source, StringComparison.Ordinal);
        Assert.Contains("getNotePosition", source, StringComparison.Ordinal);
        Assert.Contains("HasPendingMail(master, \"JojaMember\")", source, StringComparison.Ordinal);
        Assert.Contains("conflicting_irreversible_flags", source, StringComparison.Ordinal);
        Assert.Contains("FailedCommunityCenterBundle", source, StringComparison.Ordinal);
        Assert.Contains("ProjectionStatus = \"unavailable\"", source, StringComparison.Ordinal);
        Assert.DoesNotContain("completedBits[index] =", source, StringComparison.Ordinal);
        Assert.DoesNotContain("BundleRewards.Add", source, StringComparison.Ordinal);
        Assert.DoesNotContain("mailReceived.Add(\"cc", source, StringComparison.Ordinal);
    }

    private static string FindRepositoryFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(new[] { directory.FullName }.Concat(parts).ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }
            directory = directory.Parent;
        }
        throw new FileNotFoundException("Repository file not found.", Path.Combine(parts));
    }
}
