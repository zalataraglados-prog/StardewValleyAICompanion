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
            Lifecycle = new CommunityCenterLifecycleRef
            {
                ProjectionStatus = "complete_locked_base_1.6.15",
                Stage = "final_ceremony_ready",
                AllAreasComplete = true,
                AllAreaCompletionMailsReceived = true,
                FinalCeremonyReady = true,
                CompletionAdmitted = false
            },
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
        Assert.Contains("\"stage\":\"final_ceremony_ready\"", json, StringComparison.Ordinal);
        Assert.Contains("\"completion_admitted\":false", json, StringComparison.Ordinal);
        Assert.Contains("\"bundle_data_row_count\":2", json, StringComparison.Ordinal);
        Assert.Contains("\"projected_bundle_row_count\":2", json, StringComparison.Ordinal);
        Assert.Contains("\"unavailable_bundle_row_count\":1", json, StringComparison.Ordinal);
        Assert.Contains("\"projection_status\":\"unavailable\"", json, StringComparison.Ordinal);
        Assert.Contains("bundle_ingredient_shape_or_completion_bits_invalid", json, StringComparison.Ordinal);
    }

    [Fact]
    public void LifecycleProjectionLocksNativeUnlockSettlementAndCeremonyRules()
    {
        var source = File.ReadAllText(FindRepositoryFile(
            "src", "StardewAI.TransparentBridge", "Adapters",
            "ProgressReadAdapter.CommunityCenterLifecycle.cs"));

        Assert.Contains("611439/j 4/t 800 1300/w sunny/a 0 54/H", source, StringComparison.Ordinal);
        Assert.Contains("630a011f708dd6751dd98ab5468c11fcd8cd5ad2fe5910234d6a907b56e692a1", source, StringComparison.Ordinal);
        Assert.Contains("112/n seenJunimoNote", source, StringComparison.Ordinal);
        Assert.Contains("a5890ddeb05228be92acd2b895ee8313eaeb46202f4b03d0b429c7f391fd0dc9", source, StringComparison.Ordinal);
        Assert.Contains("191393/Hn ccFishTank", source, StringComparison.Ordinal);
        Assert.Contains("ed18314c06e19b54f07e3e6fddfb1a1953dcd84d8166232d0b99ea5760083ff8", source, StringComparison.Ordinal);
        Assert.Contains("LocalizedContentManager.LanguageCode.en", source, StringComparison.Ordinal);
        Assert.Contains("communityCenter.missedRewardsChestVisible.Value", source, StringComparison.Ordinal);
        Assert.Contains("master.hasCompletedCommunityCenter()", source, StringComparison.Ordinal);
        Assert.Contains("Game1.isLocationAccessible(\"CommunityCenter\")", source, StringComparison.Ordinal);
        Assert.DoesNotContain("mailReceived.Add", source, StringComparison.Ordinal);
        Assert.DoesNotContain("eventsSeen.Add", source, StringComparison.Ordinal);
    }

    [Fact]
    public void AdapterUsesLiveNativeBundleRulesAndFailsClosedWithoutProgressWrites()
    {
        var source = File.ReadAllText(FindRepositoryFile(
            "src", "StardewAI.TransparentBridge", "Adapters", "ProgressReadAdapter.cs"));

        Assert.Contains("world.BundleData", source, StringComparison.Ordinal);
        Assert.Contains("world.BundleData.Count", source, StringComparison.Ordinal);
        Assert.Contains("world.Bundles.Pairs", source, StringComparison.Ordinal);
        Assert.Contains("CompleteBundleCount = bundleRows.Count", source, StringComparison.Ordinal);
        Assert.Contains("row.Complete", source, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "world.Bundles.Pairs.Count(pair => pair.Value.All",
            source,
            StringComparison.Ordinal);
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
