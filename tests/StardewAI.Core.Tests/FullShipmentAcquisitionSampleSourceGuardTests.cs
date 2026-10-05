namespace StardewAI.Core.Tests;

public sealed class FullShipmentAcquisitionSampleSourceGuardTests
{
    [Fact]
    public void BerryBushSampleReusesNativeBushAcquisitionChain()
    {
        var source = ReadRepositoryFile(
            "scripts",
            "Invoke-RuntimeFullShipmentSapPrefixSmoke.ps1");

        Assert.Contains(
            "\"berry_bush_harvest_sample\"",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"berry_bush_harvest_sample\" { \"full_shipment:item:296\" }",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"berry_bush_harvest_sample\" { \"(O)296\" }",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"berry_bush_harvest_sample\" { \"foraging.harvest_bushes\" }",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "option_id = \"debug.setup_forage_source_fixture\"",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "Slug = \"berry-bush\"",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "BushProfile = \"berry_standard\"",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"berry_standard\"",
            source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void GingerSampleReusesNativeGingerAcquisitionChain()
    {
        var source = ReadRepositoryFile(
            "scripts",
            "Invoke-RuntimeFullShipmentSapPrefixSmoke.ps1");

        Assert.Contains(
            "\"ginger_harvest_sample\" { \"full_shipment:item:829\" }",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"ginger_harvest_sample\" { \"(O)829\" }",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"ginger_harvest_sample\" { \"foraging.harvest_ginger\" }",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "RuleKey = \"ginger\"",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "GingerProfile = \"dry_standard\"",
            source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void TeaBushSampleReusesNativeBushAcquisitionChain()
    {
        var source = ReadRepositoryFile(
            "scripts",
            "Invoke-RuntimeFullShipmentSapPrefixSmoke.ps1");

        Assert.Contains(
            "\"tea_bush_harvest_sample\" { \"full_shipment:item:815\" }",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"tea_bush_harvest_sample\" { \"(O)815\" }",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"tea_bush_harvest_sample\" { \"foraging.harvest_bushes\" }",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "Slug = \"tea-bush\"",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "BushProfile = \"tea_leaf\"",
            source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void WildTreeSeedDropSampleReusesNativeTreeProductChain()
    {
        var source = ReadRepositoryFile(
            "scripts",
            "Invoke-RuntimeFullShipmentSapPrefixSmoke.ps1");

        Assert.Contains(
            "\"wild_tree_seed_drop_sample\" { \"full_shipment:item:408\" }",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"wild_tree_seed_drop_sample\" { \"(O)408\" }",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"wild_tree_seed_drop_sample\" { \"foraging.harvest_tree_product\" }",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "Slug = \"wild-tree-seed-drop\"",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "RuleKey = \"wild_tree\"",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "WildTreeProfile = \"fall_hazelnut\"",
            source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void IslandWildTreeSeedSampleUsesExactIslandRoot()
    {
        var source = ReadRepositoryFile(
            "scripts",
            "Invoke-RuntimeFullShipmentSapPrefixSmoke.ps1");

        Assert.Contains(
            "\"wild_tree_seed_sample\" { \"full_shipment:item:88\" }",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"wild_tree_seed_sample\" { \"(O)88\" }",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"wild_tree_seed_sample\" { \"foraging.harvest_tree_product\" }",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "WildTreeProfile = \"island_palm\"",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "LocationId = \"IslandSouth\"",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "$acquisitionRootLocationId = if ($null -ne $forageFixture)",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "if (-not $sampleProofOnly -and $initialTotalDay -ne 0)",
            source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void SpringOnionSampleReusesNativeCropHarvestChain()
    {
        var source = ReadRepositoryFile(
            "scripts",
            "Invoke-RuntimeFullShipmentSapPrefixSmoke.ps1");

        Assert.Contains(
            "\"spring_onion_harvest_sample\" { \"full_shipment:item:399\" }",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"spring_onion_harvest_sample\" { \"(O)399\" }",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"spring_onion_harvest_sample\" { \"foraging.harvest_spring_onions\" }",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "Slug = \"spring-onion\"",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "RuleKey = \"spring_onion\"",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "option_id = \"debug.setup_harvest_crop_target\"",
            source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void LocationForageSampleReusesNativeSpawnedObjectChain()
    {
        var source = ReadRepositoryFile(
            "scripts",
            "Invoke-RuntimeFullShipmentSapPrefixSmoke.ps1");

        Assert.Contains(
            "\"location_forage_spawn_sample\" { \"full_shipment:item:16\" }",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"location_forage_spawn_sample\" { \"(O)16\" }",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"location_forage_spawn_sample\" { \"foraging.collect_spawned_objects\" }",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "RuleKey = \"spawned_object\"",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "SpawnedObjectProfile = \"ordinary\"",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "LocationId = \"Forest\"",
            source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void FruitTreeSampleReusesNativeFruitTreeChain()
    {
        var source = ReadRepositoryFile(
            "scripts",
            "Invoke-RuntimeFullShipmentSapPrefixSmoke.ps1");

        Assert.Contains(
            "\"fruit_tree_harvest_sample\" { \"full_shipment:item:638\" }",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"fruit_tree_harvest_sample\" { \"(O)638\" }",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"fruit_tree_harvest_sample\" { \"foraging.harvest_fruit_tree\" }",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "RuleKey = \"fruit_tree\"",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "FruitTreeProfile = \"single_normal\"",
            source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void FarmAnimalProductSampleReusesNativeMilkPailChain()
    {
        var source = ReadRepositoryFile(
            "scripts",
            "Invoke-RuntimeFullShipmentSapPrefixSmoke.ps1");

        Assert.Contains(
            "\"farm_animal_product_sample\" { \"full_shipment:item:184\" }",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"farm_animal_product_sample\" { \"(O)184\" }",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"farm_animal_product_sample\" { \"farm.collect_animal_products\" }",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "option_id = \"debug.setup_animal_product_target\"",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "RequiredToolKind = \"Milk Pail\"",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "QualifiedItemId = \"(O)184\"",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "ExpectedAnimalCrackerMultiplier = 1",
            source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void DeluxeFarmAnimalProductSampleReusesNativeMilkPailChain()
    {
        var source = ReadRepositoryFile(
            "scripts",
            "Invoke-RuntimeFullShipmentSapPrefixSmoke.ps1");

        Assert.Contains(
            "\"farm_animal_deluxe_product_sample\" { \"full_shipment:item:186\" }",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"farm_animal_deluxe_product_sample\" { \"(O)186\" }",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"farm_animal_deluxe_product_sample\" { \"farm.collect_animal_products\" }",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "Slug = \"farm-animal-deluxe-product\"",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "QualifiedItemId = \"(O)186\"",
            source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void FishPondOutputSampleReusesNativePondCollectionChain()
    {
        var source = ReadRepositoryFile(
            "scripts",
            "Invoke-RuntimeFullShipmentSapPrefixSmoke.ps1");

        Assert.Contains(
            "\"fish_pond_output_sample\" { \"full_shipment:item:812\" }",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"fish_pond_output_sample\" { \"(O)812\" }",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"fish_pond_output_sample\" { \"fishing.service_fish_ponds\" }",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "option_id = \"debug.setup_fish_pond_output\"",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "FishTypeItemId = \"(O)698\"",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "QualifiedItemId = \"(O)812\"",
            source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void SampleDateDoesNotRelaxNativeSapRecurrenceRoot()
    {
        var source = ReadRepositoryFile(
            "scripts",
            "Invoke-RuntimeFullShipmentSapPrefixSmoke.ps1");

        Assert.Contains(
            "$sampleProofOnly = $Scenario -ne \"sap_prefix\"",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "if (-not $sampleProofOnly -and $initialTotalDay -ne 0)",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"--target-total-day\", ([string]$initialTotalDay)",
            source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ResumeProofTakesSampleIdentityFromVerifiedBinding()
    {
        var source = ReadRepositoryFile(
            "scripts",
            "Resume-RuntimeFullShipmentAcquisitionProof.ps1");

        Assert.Contains(
            "$requirementId = [string]$binding.requirement_id",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "$qualifiedItemId = [string]$binding.qualified_item_id",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "full_shipment:item:296|(O)296|native_bush_shake",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "full_shipment:item:829|(O)829|native_ginger_harvest",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "full_shipment:item:815|(O)815|native_tea_bush_harvest",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "full_shipment:item:408|(O)408|native_wild_tree_seed_drop",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "full_shipment:item:88|(O)88|native_wild_tree_seed",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "full_shipment:item:399|(O)399|native_spring_onion_harvest",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "full_shipment:item:16|(O)16|native_location_forage_spawn",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "full_shipment:item:638|(O)638|native_fruit_tree_produce",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "full_shipment:item:184|(O)184|native_farm_animal_produce",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "full_shipment:item:186|(O)186|native_farm_animal_deluxe_produce",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "full_shipment:item:812|(O)812|native_fish_pond_output",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "scenario = $scenario",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "requirement_id = $requirementId",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "qualified_item_id = $qualifiedItemId",
            source,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "requirement_id = \"full_shipment:item:24\"",
            source,
            StringComparison.Ordinal);
    }

    private static string ReadRepositoryFile(params string[] segments)
    {
        var directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        while (directory is not null &&
            !File.Exists(Path.Combine(
                directory.FullName,
                "StardewValleyAICompanion.sln")))
        {
            directory = directory.Parent;
        }

        return File.ReadAllText(Path.Combine(
            directory?.FullName ?? throw new InvalidOperationException(
                "Cannot find repository root."),
            Path.Combine(segments)));
    }
}
