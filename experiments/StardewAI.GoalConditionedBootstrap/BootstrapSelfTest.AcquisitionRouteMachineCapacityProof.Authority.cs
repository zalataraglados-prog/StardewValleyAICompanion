using System.Text.Json;
using System.Text.Json.Nodes;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    private const string MachineCapacityProofRequirementId =
        "full_shipment:item:346";
    private const string MachineCapacityProofMachineSourceId =
        "machine:(BC)12:rule:keg_wheat";
    private const string CropPlantingProofRequirementId =
        "full_shipment:item:24";
    private const string CropPlantingProofSourceId = "crop:472";
    private const string CropPlantingProofShopSourcePath =
        "payload.FixtureShop.Items[0]";

    private static MachineCapacityProofAuthority
        BuildMachineCapacityProofAuthority(
            AcquisitionRouteExecutionBindingInputs template,
            string root)
    {
        Directory.CreateDirectory(root);
        var inventoryPath = Path.Combine(root, "requirement-inventory.json");
        var loweringPath = Path.Combine(root, "acquisition-lowering.json");
        var shopsPath = Path.Combine(root, "runtime-data-shops.json");
        var accessPath = Path.Combine(root, "access-constraint-index.json");
        var machinesPath = Path.Combine(root, "runtime-data-machines.json");
        var machineSelectionPath = Path.Combine(
            root,
            "native-machine-output-selection.txt");
        var catalogPath = Path.Combine(root, "master-angler-catalog.json");
        var windowsPath = Path.Combine(root, "master-angler-windows.json");
        var calendarPath = Path.Combine(root, "route-calendar.json");

        var inventory = CurrentTeacherFrontierSupport.Read<
            AuthoritativeRequirementInventoryReport>(
            template.RequirementInventoryPath,
            "Machine-capacity proof requirement inventory template");
        CloneMachineCapacityProofShopAuthority(
            inventory,
            shopsPath,
            accessPath);
        WriteMachineCapacityProofMachineAuthority(machinesPath);
        File.WriteAllText(
            machineSelectionPath,
            "fixture MachineDataUtility.TryGetMachineOutputRule and GetOutputItem source");

        inventory.SourceEvidence = inventory.SourceEvidence
            .Where(row => row.SourceId is not (
                "runtime_data_shops" or
                "access_constraint_index" or
                "runtime_data_machines" or
                "native_machine_output_selection_rule"))
            .Concat(new[]
            {
                new RequirementSourceEvidence(
                    "runtime_data_shops",
                    Path.GetFullPath(shopsPath),
                    HashFile(shopsPath),
                    "runtime DataLoader.Shops machine-capacity fixture"),
                new RequirementSourceEvidence(
                    "access_constraint_index",
                    Path.GetFullPath(accessPath),
                    HashFile(accessPath),
                    "compiled shop access machine-capacity fixture"),
                new RequirementSourceEvidence(
                    "runtime_data_machines",
                    Path.GetFullPath(machinesPath),
                    HashFile(machinesPath),
                    "runtime DataLoader.Machines machine-capacity fixture"),
                new RequirementSourceEvidence(
                    "native_machine_output_selection_rule",
                    Path.GetFullPath(machineSelectionPath),
                    HashFile(machineSelectionPath),
                    "decompiled machine output selection fixture")
            })
            .OrderBy(row => row.SourceId, StringComparer.Ordinal)
            .ToArray();
        var shipment = inventory.RequirementSets.Single(set =>
            set.RequirementSetId == "full_shipment");
        var cropGroup = shipment.Groups.Single(group =>
            group.RequirementId == CropPlantingProofRequirementId);
        var cropAlternative = cropGroup.Alternatives.Single(alternative =>
            alternative.QualifiedItemId == "(O)24");
        cropAlternative.AcquisitionRoutes = cropAlternative.AcquisitionRoutes
            .Append(new RequirementAcquisitionRoute(
                "sells",
                "shop:FixtureShop",
                "Data/Shops",
                CropPlantingProofShopSourcePath))
            .ToArray();
        shipment.Groups = shipment.Groups
            .Append(new GoalRequirementGroup
            {
                RequirementId = MachineCapacityProofRequirementId,
                SelectionRule = "all_required",
                RequiredAlternativeCount = 1,
                RouteCovered = true,
                Alternatives =
                [
                    new GoalRequirementAlternative
                    {
                        ItemId = "346",
                        QualifiedItemId = "(O)346",
                        DisplayName = "Beer",
                        MatchKind = "item_id",
                        Amount = 1,
                        MinimumQuality = 0,
                        AcquisitionRoutes =
                        [
                            new RequirementAcquisitionRoute(
                                "machine_output",
                                MachineCapacityProofMachineSourceId,
                                "Data/Machines",
                                "payload.(BC)12.OutputRules[0].OutputItem[0].ItemId"),
                            new RequirementAcquisitionRoute(
                                "sells",
                                "shop:FixtureShop",
                                "Data/Shops",
                                "payload.FixtureShop.Items[1]")
                        ]
                    }
                ]
            })
            .ToArray();
        shipment.RequiredGroupCount = shipment.Groups.Length;
        shipment.RouteCoveredGroupCount = shipment.Groups.Length;
        Write(inventoryPath, inventory);

        var lowering = CurrentTeacherFrontierSupport.Read<
            AcquisitionRouteOptionLoweringReport>(
            template.AcquisitionLoweringPath,
            "Machine-capacity proof lowering template");
        var loweredShipment = lowering.RequirementSets.Single(set =>
            set.RequirementSetId == "full_shipment");
        var loweredCropGroup = loweredShipment.Groups.Single(group =>
            group.RequirementId == CropPlantingProofRequirementId);
        var loweredCropAlternative = loweredCropGroup.Alternatives.Single(
            alternative => alternative.QualifiedItemId == "(O)24");
        var cropShopRoute = new AcquisitionRequirementRouteLowering(
            "sells",
            "shop:FixtureShop",
            "Data/Shops",
            CropPlantingProofShopSourcePath,
            "deterministic_dependency",
            "deterministic_fresh_receipt",
            StageOneCollectionRouteDependencyAxes.Required.ToArray(),
            ["economy.buy_supplies"],
            Array.Empty<string>(),
            true,
            true);
        var cropGroupWithShop = loweredCropGroup with
        {
            Alternatives = loweredCropGroup.Alternatives.Select(alternative =>
                alternative == loweredCropAlternative
                    ? alternative with
                    {
                        Routes = alternative.Routes
                            .Append(cropShopRoute)
                            .ToArray()
                    }
                    : alternative)
                .ToArray()
        };
        var machineRoute = new AcquisitionRequirementRouteLowering(
            "machine_output",
            MachineCapacityProofMachineSourceId,
            "Data/Machines",
            "payload.(BC)12.OutputRules[0].OutputItem[0].ItemId",
            "policy_option",
            "native_outcome_domain_and_retry_bound",
            StageOneCollectionRouteDependencyAxes.Required.ToArray(),
            ["farm.collect_machine_outputs"],
            [
                "economy.buy_supplies",
                "inventory.transfer_item",
                "farm.establish_supported_machine_capacity",
                "farm.load_supported_machine_input",
                "farm.process_machines"
            ],
            true,
            true);
        var shopRoute = new AcquisitionRequirementRouteLowering(
            "sells",
            "shop:FixtureShop",
            "Data/Shops",
            "payload.FixtureShop.Items[1]",
            "deterministic_dependency",
            "deterministic_fresh_receipt",
            StageOneCollectionRouteDependencyAxes.Required.ToArray(),
            ["economy.buy_supplies"],
            Array.Empty<string>(),
            true,
            true);
        var machineGroup = new AcquisitionRequirementGroupLowering(
            MachineCapacityProofRequirementId,
            1,
            1,
            1,
            true,
            true,
            Array.Empty<string>(),
            [
                new AcquisitionRequirementAlternativeLowering(
                    "346",
                    "(O)346",
                    "Beer",
                    "item_id",
                    1,
                    0,
                    true,
                    true,
                    [machineRoute, shopRoute])
            ]);
        var replacementShipment = new AcquisitionRequirementSetLowering(
            loweredShipment.RequirementSetId,
            loweredShipment.RequiredGroupCount + 1,
            loweredShipment.RuntimeAdmittedGroupCount + 1,
            loweredShipment.TeacherAdmittedGroupCount + 1,
            loweredShipment.Groups
                .Select(group => group == loweredCropGroup
                    ? cropGroupWithShop
                    : group)
                .Append(machineGroup)
                .ToArray());
        lowering.RequirementSets = lowering.RequirementSets
            .Select(set => set.RequirementSetId == "full_shipment"
                ? replacementShipment
                : set)
            .ToArray();
        lowering.RequirementInventorySha256 = HashFile(inventoryPath);
        lowering.RequirementGroupCount = lowering.RequirementSets.Sum(set =>
            set.Groups.Length);
        lowering.RouteOccurrenceCount = lowering.RequirementSets
            .SelectMany(set => set.Groups)
            .SelectMany(group => group.Alternatives)
            .Sum(alternative => alternative.Routes.Length);
        Write(loweringPath, lowering);

        var sourceWindows = CurrentTeacherFrontierSupport.Read<
            MasterAnglerStageOneWindowIndex>(
            template.MasterAnglerWindowsPath,
            "Machine-capacity proof Master Angler windows template");
        var catalog = JsonNode.Parse(
            File.ReadAllText(sourceWindows.CatalogPath))!.AsObject();
        catalog["requirement_inventory_path"] =
            Path.GetFullPath(inventoryPath);
        catalog["requirement_inventory_sha256"] = HashFile(inventoryPath);
        File.WriteAllText(
            catalogPath,
            catalog.ToJsonString(JsonDefaults.Options));
        Write(
            windowsPath,
            MasterAnglerStageOneWindowIndexBuilder.Build(
                catalogPath,
                sourceWindows.DeadlineYear));
        Write(
            calendarPath,
            AcquisitionRouteCalendarResolutionBuilder.Build(
                inventoryPath,
                loweringPath,
                windowsPath));

        var adjustedNode = JsonNode.Parse(JsonSerializer.Serialize(
            template,
            JsonDefaults.Options))!.AsObject();
        adjustedNode["requirement_inventory_path"] = inventoryPath;
        adjustedNode["acquisition_lowering_path"] = loweringPath;
        adjustedNode["master_angler_windows_path"] = windowsPath;
        adjustedNode["calendar_resolution_path"] = calendarPath;
        var adjusted = JsonSerializer.Deserialize<
            AcquisitionRouteExecutionBindingInputs>(
            adjustedNode.ToJsonString(JsonDefaults.Options),
            JsonDefaults.Options) ?? throw new InvalidDataException(
            "Machine-capacity proof execution template clone failed.");
        var calendar = CurrentTeacherFrontierSupport.Read<
            AcquisitionRouteCalendarResolutionReport>(
            calendarPath,
            "Machine-capacity proof calendar");
        var machine = calendar.Routes.Single(route =>
            route.RouteKind == "machine_output" &&
            route.SourceId == MachineCapacityProofMachineSourceId &&
            route.QualifiedItemId == "(O)346");
        var shop = calendar.Routes.Single(route =>
            route.RouteKind == "sells" &&
            route.SourceId == "shop:FixtureShop" &&
            route.QualifiedItemId == "(O)346");
        var crop = calendar.Routes.Single(route =>
            route.RouteOccurrenceId.StartsWith(
                "full_shipment:",
                StringComparison.Ordinal) &&
            route.RouteKind == "harvests_as" &&
            route.SourceId == CropPlantingProofSourceId &&
            route.QualifiedItemId == "(O)24");
        var cropShop = calendar.Routes.Single(route =>
            route.RouteOccurrenceId.StartsWith(
                "full_shipment:",
                StringComparison.Ordinal) &&
            route.RouteKind == "sells" &&
            route.SourceId == "shop:FixtureShop" &&
            route.QualifiedItemId == "(O)24");
        return new MachineCapacityProofAuthority(
            adjusted,
            inventory.GoalId,
            machine.RouteOccurrenceId,
            shop.RouteOccurrenceId,
            crop.RouteOccurrenceId,
            cropShop.RouteOccurrenceId);
    }

    private static void CloneMachineCapacityProofShopAuthority(
        AuthoritativeRequirementInventoryReport inventory,
        string shopsPath,
        string accessPath)
    {
        var sourceShopPath = inventory.SourceEvidence.Single(row =>
            row.SourceId == "runtime_data_shops").Path;
        var shops = JsonNode.Parse(File.ReadAllText(sourceShopPath))!.AsObject();
        var items = shops["payload"]!["FixtureShop"]!["Items"]!.AsArray();
        var parsnip = items[0]!.AsObject();
        parsnip["Price"] = 100;
        parsnip["AvailableStock"] = 2;
        parsnip["AvailableStockLimit"] = 2;
        parsnip["TradeItemId"] = null;
        parsnip["TradeItemAmount"] = 0;
        parsnip["Condition"] = null;
        parsnip["ActionsOnPurchase"] = new JsonArray();
        var beer = JsonNode.Parse(items[0]!.ToJsonString())!.AsObject();
        beer["Id"] = "fixture-beer";
        beer["ItemId"] = "(O)346";
        beer["Price"] = 200;
        beer["AvailableStock"] = 1;
        beer["AvailableStockLimit"] = 1;
        beer["TradeItemId"] = null;
        beer["TradeItemAmount"] = 0;
        beer["Condition"] = null;
        beer["ActionsOnPurchase"] = new JsonArray();
        var wheat = JsonNode.Parse(beer.ToJsonString())!.AsObject();
        wheat["Id"] = "fixture-wheat-seed";
        wheat["ItemId"] = "(O)262";
        wheat["Price"] = 80;
        wheat["AvailableStock"] = 999;
        wheat["AvailableStockLimit"] = 999;
        items.Add(beer);
        items.Add(wheat);
        File.WriteAllText(shopsPath, shops.ToJsonString(JsonDefaults.Options));

        var sourceAccessPath = inventory.SourceEvidence.Single(row =>
            row.SourceId == "access_constraint_index").Path;
        var access = JsonNode.Parse(File.ReadAllText(sourceAccessPath))!
            .AsObject();
        var stock = access["shops"]![0]!["stock"]!.AsArray();
        var parsnipAccess = stock[0]!.AsObject();
        parsnipAccess["condition"] = null;
        parsnipAccess["perItemCondition"] = null;
        parsnipAccess["parsedCondition"] = null;
        parsnipAccess["parsedPerItemCondition"] = null;
        var beerAccess = JsonNode.Parse(stock[0]!.ToJsonString())!.AsObject();
        beerAccess["id"] = "fixture-beer";
        beerAccess["itemId"] = "(O)346";
        beerAccess["condition"] = null;
        beerAccess["perItemCondition"] = null;
        beerAccess["parsedCondition"] = null;
        beerAccess["parsedPerItemCondition"] = null;
        var wheatAccess = JsonNode.Parse(beerAccess.ToJsonString())!.AsObject();
        wheatAccess["id"] = "fixture-wheat-seed";
        wheatAccess["itemId"] = "(O)262";
        stock.Add(beerAccess);
        stock.Add(wheatAccess);
        File.WriteAllText(accessPath, access.ToJsonString(JsonDefaults.Options));
    }

    private static void WriteMachineCapacityProofMachineAuthority(string path)
    {
        var output = new JsonObject
        {
            ["ItemId"] = "(O)346",
            ["OutputMethod"] = null,
            ["Condition"] = null,
            ["PerItemCondition"] = null,
            ["RandomItemId"] = null,
            ["MinStack"] = 1,
            ["MaxStack"] = 1,
            ["Quality"] = 0,
            ["CopyQuality"] = false,
            ["StackModifierMode"] = 0,
            ["StackModifiers"] = null,
            ["QualityModifierMode"] = 0,
            ["QualityModifiers"] = null
        };
        var trigger = new JsonObject
        {
            ["Id"] = "ItemPlacedInMachine",
            ["Trigger"] = 1,
            ["RequiredItemId"] = "(O)262",
            ["RequiredTags"] = null,
            ["RequiredCount"] = 1,
            ["Condition"] = null
        };
        var rule = new JsonObject
        {
            ["Id"] = "keg_wheat",
            ["Condition"] = null,
            ["Triggers"] = new JsonArray(trigger),
            ["UseFirstValidOutput"] = true,
            ["OutputItem"] = new JsonArray(output),
            ["MinutesUntilReady"] = 1750,
            ["DaysUntilReady"] = -1,
            ["RecalculateOnCollect"] = false
        };
        var machine = new JsonObject
        {
            ["OutputRules"] = new JsonArray(rule),
            ["AdditionalConsumedItems"] = null,
            ["ReadyTimeModifiers"] = null,
            ["ReadyTimeModifierMode"] = 0,
            ["OnlyCompleteOvernight"] = false
        };
        var root = new JsonObject
        {
            ["payload"] = new JsonObject
            {
                ["(BC)12"] = machine
            }
        };
        File.WriteAllText(path, root.ToJsonString(JsonDefaults.Options));
    }

    private sealed record MachineCapacityProofAuthority(
        AcquisitionRouteExecutionBindingInputs Template,
        string GoalId,
        string MachineRouteOccurrenceId,
        string ShopRouteOccurrenceId,
        string CropRouteOccurrenceId,
        string CropShopRouteOccurrenceId);
}
