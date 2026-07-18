using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed partial class FullShipmentContributionTests
{
    [Fact]
    public void AdapterUsesStaticIsPotentialBasicShippedAndNoItemRegistryCreateOrInstanceCall()
    {
        var adapterPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "StardewAI.TransparentBridge", "Adapters", "ProgressReadAdapter.cs"));
        var source = File.ReadAllText(adapterPath);

        Assert.Contains("Object.isPotentialBasicShipped(itemId, category, objectType)", source);

        Assert.DoesNotContain("ItemRegistry.Create", source);
        Assert.DoesNotContain(".isPotentialBasicShipped()", source);
        Assert.DoesNotContain("Utility.getFarmerItemsShippedPercent", source);
        Assert.DoesNotContain("GetAllData", source);
    }

    [Fact]
    public void AdapterScopeUsesUseSeparateWalletsNotReferenceEquals()
    {
        var source = FarmReadAdapterSources.All;

        Assert.Contains("useSeparateWallets", source);
        Assert.DoesNotContain("ReferenceEquals", source);
        Assert.Contains("\"personal\"", source);
        Assert.Contains("\"shared\"", source);
    }

    [Fact]
    public void AdapterEmitsStandTilesArrayAndContentsWithSignature()
    {
        var source = FarmReadAdapterSources.All;

        Assert.Contains("stand_tiles", source);
        Assert.Contains("contents_signature", source);
        Assert.Contains("contents_total_count", source);
        Assert.Contains("contents_distinct_item_count", source);
        Assert.Contains("contents_truncated", source);
        Assert.Contains("SHA256", source);
        Assert.Contains("ComputeContentsSignature", source);
    }

    // === Catalog and binding tests ===

    [Fact]
    public void FullShipmentDirectionBindsOnlyExactContributingCandidate()
    {
        var snapshot = GrandpaSnapshot();
        var binding = new GrandpaDirectionDailyCandidateBinding();
        var result = binding.Bind(new GrandpaDirectionBindingRequest
        {
            StateHash = snapshot.StateHash,
            DirectionId = "complete_full_shipment",
            RankedCandidates = new[]
            {
                new PolicyEventCandidatePrediction
                {
                    CandidateId = "ship:parsnip:0",
                    OptionId = "economy.ship_items",
                    Kind = "ship_inventory_item_to_bin",
                    Available = true,
                    AllowedNow = true,
                    AllowedToday = true,
                    TimelineStatus = "ready_now",
                    CanShip = true,
                    FullShipmentKnown = true,
                    FullShipmentEligible = true,
                    FullShipmentCurrentShippedCount = 0,
                    FullShipmentAlreadyShipped = false,
                    FullShipmentContributes = true
                }
            }
        }, snapshot);

        Assert.Equal("ready", result.BindingStatus);
        Assert.Equal("ready", result.BindingCoverageStatus);
        var bound = Assert.Single(result.BoundCandidates);
        Assert.Equal("ship:parsnip:0", bound.CandidateId);
        Assert.Contains(bound.Parameters,
            p => p.Name == "grandpa_direction_id" && p.Value == "complete_full_shipment");
        Assert.Empty(result.MissingTransparentFields);
        Assert.Empty(result.MissingCapabilities);
        Assert.Contains("world_progress.shipping_collection", result.CoveredTransparentFields);
        Assert.Contains("world_progress.full_shipment_progress", result.CoveredTransparentFields);
    }

    [Fact]
    public void FullShipmentDirectionRejectsAlreadyShippedCandidateEvenWhenContributionFlagConflicts()
    {
        var snapshot = GrandpaSnapshot();
        var result = new GrandpaDirectionDailyCandidateBinding().Bind(new GrandpaDirectionBindingRequest
        {
            StateHash = snapshot.StateHash,
            DirectionId = "complete_full_shipment",
            RankedCandidates = new[]
            {
                new PolicyEventCandidatePrediction
                {
                    CandidateId = "ship:parsnip:already-shipped",
                    OptionId = "economy.ship_items",
                    Kind = "ship_inventory_item_to_bin",
                    Available = true,
                    AllowedNow = true,
                    AllowedToday = true,
                    TimelineStatus = "ready_now",
                    CanShip = true,
                    FullShipmentKnown = true,
                    FullShipmentEligible = true,
                    FullShipmentCurrentShippedCount = 1,
                    FullShipmentAlreadyShipped = true,
                    FullShipmentContributes = true
                }
            }
        }, snapshot);

        Assert.Equal("blocked", result.BindingStatus);
        Assert.Empty(result.BoundCandidates);
        Assert.Contains(result.BlockReasons,
            reason => reason.Contains("full_shipment_current_shipped_count_not_zero", StringComparison.Ordinal));
    }

    [Fact]
    public void CatalogCompleteFullShipmentEntryIsDirectAndKeepsTransparentCoverage()
    {
        var entry = GrandpaDirectionCatalog.Entries
            .First(e => e.DirectionId == "complete_full_shipment");

        Assert.True(entry.DirectBindingEnabled);
        Assert.Equal("grandpa.direct.complete_full_shipment", entry.BindingRuleId);
        Assert.Equal(new[] { "economy.ship_items" }, entry.PermittedOptionIds);
        Assert.Equal(new[] { "ship_inventory_item_to_bin" }, entry.PermittedCandidateKinds);
        Assert.Empty(entry.RequiredTransparentFields);
        Assert.Contains("world_progress.shipping_collection", entry.CoveredTransparentFields);
        Assert.Contains("world_progress.full_shipment_progress", entry.CoveredTransparentFields);
        Assert.Equal(2, entry.CoveredTransparentFields.Length);
        Assert.Empty(entry.RequiredCapabilities);
        Assert.Contains("exact transparent contribution evidence", entry.BlockReasonTemplate);
    }

    [Fact]
    public void CatalogHas11EntriesAndFullShipmentIsDirect()
    {
        var entries = GrandpaDirectionCatalog.Entries;
        Assert.Equal(11, entries.Length);

        var fullShipment = entries.Single(e => e.DirectionId == "complete_full_shipment");
        Assert.True(fullShipment.DirectBindingEnabled);
        Assert.Empty(fullShipment.RequiredTransparentFields);
        Assert.NotEmpty(fullShipment.CoveredTransparentFields);
        Assert.Empty(fullShipment.RequiredCapabilities);
    }

    [Fact]
    public void CoveredTransparentFieldsResultIsNotAliasedToCatalog()
    {
        var snapshot = GrandpaSnapshot();
        var binding = new GrandpaDirectionDailyCandidateBinding();
        var result = binding.Bind(new GrandpaDirectionBindingRequest
        {
            StateHash = snapshot.StateHash,
            DirectionId = "complete_full_shipment"
        }, snapshot);

        var catalogEntry = GrandpaDirectionCatalog.Entries
            .First(e => e.DirectionId == "complete_full_shipment");

        var catalogFields = catalogEntry.CoveredTransparentFields;
        var resultFields = result.CoveredTransparentFields;

        Assert.Same(catalogFields, catalogEntry.CoveredTransparentFields);
        Assert.NotSame(catalogFields, resultFields);

        resultFields[0] = "mutated";
        Assert.NotEqual("mutated", catalogFields[0]);
    }

    // === Contract arithmetic / DTO sorting tests ===

    [Fact]
    public void FullShipmentItemsSortedByItemIdThenQualifiedItemId()
    {
        var progress = CreateProgress(
            eligible: new[]
            {
                ("(O)80", "80", "Quartz", -75, "Basic", 0),
                ("(O)60", "60", "Emerald", -75, "Basic", 0),
                ("(O)24", "24", "Parsnip", -75, "Basic", 1),
                ("(O)80", "80", "Quartz", -75, "Basic", 0)
            },
            shippedItemIds: new[] { "24" });

        var ids = progress.Items.Select(i => i.QualifiedItemId).ToArray();
        Assert.Equal(new[] { "(O)24", "(O)60", "(O)80", "(O)80" }, ids);

        var itemIds = progress.Items.Select(i => i.ItemId).ToArray();
        Assert.Equal(new[] { "24", "60", "80", "80" }, itemIds);
    }

    [Fact]
    public void FullShipmentProgressCompleteWhenAllEligibleItemsShipped()
    {
        var progress = CreateProgress(
            eligible: new[] { ("(O)24", "24", "Parsnip", -75, "Basic", 1), ("(O)80", "80", "Quartz", -75, "Basic", 1) },
            shippedItemIds: new[] { "24", "80" });

        Assert.Equal(2, progress.EligibleItemCount);
        Assert.Equal(2, progress.ShippedEligibleItemCount);
        Assert.Equal(0, progress.MissingItemCount);
        Assert.Equal(1.0, progress.CompletionRatio);
        Assert.True(progress.Complete);
        Assert.Empty(progress.MissingItemIds);
    }

    [Fact]
    public void FullShipmentProgressEmptyWhenNoEligibleItems()
    {
        var progress = CreateProgress(
            eligible: Array.Empty<(string, string, string, int, string, int)>(),
            shippedItemIds: new[] { "24" });

        Assert.Equal(0, progress.EligibleItemCount);
        Assert.Equal(0, progress.ShippedEligibleItemCount);
        Assert.Equal(0, progress.MissingItemCount);
        Assert.Equal(0, progress.CompletionRatio);
        Assert.False(progress.Complete);
    }

    [Fact]
    public void MissingItemIdsSortedOrdinally()
    {
        var progress = CreateProgress(
            eligible: new[]
            {
                ("(O)80", "80", "Quartz", -75, "Basic", 0),
                ("(O)60", "60", "Emerald", -75, "Basic", 0),
                ("(O)24", "24", "Parsnip", -75, "Basic", 1)
            },
            shippedItemIds: new[] { "24" });

        var missingIds = progress.MissingItemIds;
        Assert.Equal(new[] { "60", "80" }, missingIds);
    }

    // === E2E pipeline test ===

}
