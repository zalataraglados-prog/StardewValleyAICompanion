using System.Security.Cryptography;
using System.Text;
using StardewAI.Contracts.Capabilities;

namespace StardewAI.KnowledgeCompiler;

internal sealed record NativeActionSurface(
    string SurfaceId,
    string Family,
    string RuntimeType,
    string Member,
    string Signature,
    string RelativeSourcePath,
    int StartLine,
    int EndLine,
    string BodySha256,
    string[] MappedOptionIds,
    string SemanticCoverageStatus,
    string ScopeDisposition,
    string EvidenceBasis);

internal sealed class NativeActionSurfaceCatalog
{
    public string SourceStatus { get; init; } = "decompile_root_not_supplied";
    public string DecompileRoot { get; init; } = string.Empty;
    public IReadOnlyList<NativeActionSurface> Surfaces { get; init; } =
        Array.Empty<NativeActionSurface>();
}

internal sealed class NativeActionSurfaceCatalogBuilder
{
    private sealed record SurfaceClassification(
        string[] OptionIds,
        string ScopeDisposition,
        string EvidenceBasis);

    public NativeActionSurfaceCatalog Build(string? decompileRoot)
    {
        if (string.IsNullOrWhiteSpace(decompileRoot))
            return new NativeActionSurfaceCatalog();

        var root = Path.GetFullPath(decompileRoot);
        if (!Directory.Exists(root))
        {
            return new NativeActionSurfaceCatalog
            {
                SourceStatus = "decompile_root_missing",
                DecompileRoot = root
            };
        }

        var sourceIndex = DecompiledActionSourceIndex.Build(root);
        var rows = new List<NativeActionSurface>();
        foreach (var method in sourceIndex.Methods)
        {
            var family = ResolveFamily(method.RelativeSourcePath, method.Member);
            var classification = ClassifySurface(family, method.RuntimeType, method.Member);
            rows.Add(new NativeActionSurface(
                SurfaceId(method.RelativeSourcePath, method.Signature),
                family,
                method.RuntimeType,
                method.Member,
                method.Signature,
                method.RelativeSourcePath,
                method.StartLine,
                method.EndLine,
                method.BodySha256,
                classification.OptionIds,
                ResolveCoverage(classification),
                classification.ScopeDisposition,
                classification.EvidenceBasis));
        }

        foreach (var type in sourceIndex.MinigameTypes)
        {
            var classification = ClassifySurface("minigame", type.RuntimeType, "IMinigame");
            const string signature = "type declaration : IMinigame";
            rows.Add(new NativeActionSurface(
                SurfaceId(type.RelativeSourcePath, signature),
                "minigame",
                type.RuntimeType,
                "IMinigame",
                signature,
                type.RelativeSourcePath,
                type.StartLine,
                type.EndLine,
                string.Empty,
                classification.OptionIds,
                ResolveCoverage(classification),
                classification.ScopeDisposition,
                classification.EvidenceBasis));
        }

        var surfaces = rows
            .OrderBy(row => row.Family, StringComparer.Ordinal)
            .ThenBy(row => row.RuntimeType, StringComparer.Ordinal)
            .ThenBy(row => row.Member, StringComparer.Ordinal)
            .ToArray();
        var requiredFamilies = new[] { "tool", "menu", "location_interaction" };
        var discoveredFamilies = surfaces
            .Select(row => row.Family)
            .ToHashSet(StringComparer.Ordinal);
        var hasUniqueSurfaceIds = surfaces
            .Select(row => row.SurfaceId)
            .Distinct(StringComparer.Ordinal)
            .Count() == surfaces.Length;
        var scanComplete = surfaces.Length >= 50 &&
            requiredFamilies.All(discoveredFamilies.Contains) &&
            hasUniqueSurfaceIds;

        return new NativeActionSurfaceCatalog
        {
            SourceStatus = scanComplete
                ? "native_decompile_scanned"
                : "native_decompile_scan_incomplete",
            DecompileRoot = root,
            Surfaces = surfaces
        };
    }

    private static string ResolveFamily(string relative, string member)
    {
        if (relative.Contains("/Tools/", StringComparison.Ordinal))
            return "tool";
        if (relative.Contains("/Menus/", StringComparison.Ordinal))
            return "menu";
        if (relative.Contains("/Minigames/", StringComparison.Ordinal))
            return "minigame";
        if (relative.Contains("/TerrainFeatures/", StringComparison.Ordinal))
            return "terrain_feature";
        if (member is "performUseAction" or "placementAction")
            return "item_use";
        return "location_interaction";
    }

    private static SurfaceClassification ClassifySurface(
        string family,
        string runtimeType,
        string member)
    {
        var options = runtimeType switch
        {
            "Axe" => new[] { "executor.clear_obstacle", "executor.break_farm_resource_clump" },
            "Pickaxe" => new[] { "executor.clear_obstacle", "executor.mine_stone", "executor.remove_machine" },
            "Hoe" => new[] { "executor.till_soil", "executor.harvest_ginger" },
            "WateringCan" => new[] { "farm.maintain_crops", "executor.fill_pet_bowl", "executor.cool_volcano_lava" },
            "FishingRod" => new[] { "executor.catch_fish" },
            "MeleeWeapon" => new[] { "executor.combat_monster", "executor.combat_volcano_monster" },
            "Slingshot" => new[] { "executor.shoot_monster" },
            "Pan" => new[] { "executor.pan_ore_spot" },
            "MilkPail" or "Shears" => new[] { "executor.collect_animal_product" },
            "ShopMenu" => new[] { "executor.buy_shop_item", "executor.sell_shop_item" },
            "DialogueBox" => new[] { "executor.choose_dialogue_response" },
            "CraftingPage" => new[] { "executor.craft_machine_item", "executor.craft_storage_item" },
            "MuseumMenu" => new[] { "executor.donate_museum_item" },
            "JunimoNoteMenu" => new[] { "executor.donate_community_center_item" },
            "JojaCDMenu" => new[] { "executor.purchase_joja_project" },
            "BobberBar" => new[] { "executor.catch_fish" },
            "AnimalQueryMenu" => new[] { "animals.manage_animal" },
            "BuildingPaintMenu" => new[] { "buildings.paint" },
            "BuildingSkinMenu" => new[] { "buildings.change_skin" },
            "CarpenterMenu" => new[] { "buildings.construct" },
            "CharacterCustomization" => new[] { "player.customize" },
            "ChatBox" => new[] { "multiplayer.send_chat" },
            "DyeMenu" => new[] { "tailoring.dye_item" },
            "EmoteMenu" or "EmoteSelector" => new[] { "social.emote" },
            "FieldOfficeMenu" => new[] { "island.field_office_donate" },
            "ForgeMenu" => new[] { "crafting.forge_item" },
            "GeodeMenu" => new[] { "processing.crack_geode" },
            "ItemGrabMenu" or "StorageContainer" => new[] { "inventory.transfer_item" },
            "LetterViewerMenu" => new[] { "mail.process_letter" },
            "LevelUpMenu" => new[] { "skills.choose_profession" },
            "MasteryTrackerMenu" => new[] { "skills.claim_mastery" },
            "MineElevatorMenu" => new[] { "mining.use_elevator" },
            "PondQueryMenu" => new[] { "fishing.manage_fish_pond" },
            "PrizeTicketMenu" => new[] { "rewards.claim_prize_ticket" },
            "PurchaseAnimalsMenu" => new[] { "animals.purchase" },
            "Billboard" => new[] { "quest.accept_daily" },
            "QuestLog" => new[] { "quest.claim_reward", "quest.cancel" },
            "RenovateMenu" => new[] { "housing.renovate" },
            "SpecialOrdersBoard" => new[] { "quest.accept_special_order" },
            "TailoringMenu" => new[] { "tailoring.sew_item" },
            "StrengthGame" => new[] { "festival.play_strength_game" },
            "WheelSpinGame" => new[] { "festival.spin_wheel" },
            "Wand" => new[] { "executor.use_return_scepter" },
            "Lantern" => new[] { "executor.toggle_lantern" },
            "Raft" => new[] { "executor.use_raft" },
            "Bush" => new[] { "executor.harvest_bush" },
            "FruitTree" => new[] { "foraging.harvest_fruit_tree", "executor.clear_obstacle" },
            "HoeDirt" => new[] { "farm.maintain_crops", "executor.plant_seed", "executor.harvest_crop" },
            "ResourceClump" => new[] { "executor.break_current_location_resource_clump" },
            "Tent" => new[] { "recovery.sleep_in_tent" },
            "Tree" => new[] { "foraging.harvest_tree_product", "executor.clear_obstacle" },
            "Sign" => new[] { "executor.set_sign_display_item" },
            "AbigailGame" => new[] { "minigame.play_prairie_king" },
            "CalicoJack" => new[] { "minigame.play_calico_jack" },
            "CraneGame" => new[] { "minigame.play_crane_game" },
            "Darts" => new[] { "minigame.play_darts" },
            "FantasyBoardGame" => new[] { "story.advance_event_minigame" },
            "FishingGame" => new[] { "festival.play_fishing_game" },
            "MineCart" => new[] { "minigame.play_junimo_kart" },
            "Slots" => new[] { "minigame.play_slots" },
            "TargetGame" => new[] { "festival.play_slingshot_game" },
            "BoatJourney" or "GrandpaStory" or "HaleyCowPictures" or "Intro" or
                "MaruComet" or "PlaneFlyBy" or "RobotBlastoff" or "TelescopeScene" =>
                new[] { "story.advance_event_minigame" },
            _ => Array.Empty<string>()
        };

        if (options.Length > 0)
        {
            return new SurfaceClassification(
                options.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                "target_actor_action",
                "decompiled runtime type and native input member");
        }

        if (family == "location_interaction" || family == "item_use")
        {
            return new SurfaceClassification(
                Array.Empty<string>(),
                "source_branch_required",
                "method entry is too broad; native branches and data-driven item identities must be enumerated");
        }

        if (runtimeType is "IClickableMenu" or "MenuWithInventory" or "InventoryMenu" or
            "Toolbar" or "ChooseFromIconsMenu" or "ChooseFromListMenu" or
            "ConfirmationDialog" or "DigitEntryMenu" or "DiscreteColorPicker" or
            "NamingMenu" or "NumberSelectionMenu" or "TextEntryMenu" or
            "QuestContainerMenu" or "FarmersBox" or "ReadyCheckDialog")
        {
            return new SurfaceClassification(
                Array.Empty<string>(),
                "parent_action_component",
                "decompiled input handler delegates or binds parameters for its owning semantic action");
        }

        if (runtimeType is "AboutMenu" or "AdvancedGameOptions" or "ExitPage" or
            "LanguageSelectionMenu" or "LoadGameMenu" or "OptionsButton" or
            "OptionsCheckbox" or "OptionsDropDown" or "OptionsElement" or
            "OptionsInputListener" or "OptionsPage" or "OptionsPlusMinus" or
            "OptionsPlusMinusButton" or "OptionsSlider" or "OptionsTextEntry" or
            "TitleMenu" or "TitleTextInputMenu" or "TooManyFarmsMenu")
        {
            return new SurfaceClassification(
                Array.Empty<string>(),
                "outside_in_save_actor_scope",
                "decompiled type belongs to title, load, language, or client configuration flow");
        }

        if (runtimeType is "CoopMenu" or "FarmhandMenu" or "LocalCoopJoinMenu" or
            "CharacterCustomization")
        {
            return new SurfaceClassification(
                Array.Empty<string>(),
                "product_bootstrap_scope",
                "decompiled type belongs to save or multiplayer actor bootstrap, not in-save policy");
        }

        if (runtimeType is "AnimalPage" or "CollectionsPage" or "DayTimeMoneyBox" or
            "GameMenu" or "InventoryPage" or "ItemListMenu" or "MapPage" or
            "PowersTab" or "ProfileMenu" or "ShippingMenu" or "SkillsPage" or
            "SocialPage" or "TailorRecipeListTool" or "TutorialMenu")
        {
            return new SurfaceClassification(
                Array.Empty<string>(),
                "observation_or_navigation_only",
                "decompiled handler changes view, page, selection, or closes display without a gameplay commitment");
        }

        if (runtimeType is "AnimationPreviewTool" or "Test")
        {
            return new SurfaceClassification(
                Array.Empty<string>(),
                "debug_only",
                "decompiled runtime type is an internal preview or test surface");
        }

        if (runtimeType is "TerrainFeature" or "IMinigame")
        {
            return new SurfaceClassification(
                Array.Empty<string>(),
                "abstract_parent_surface",
                "abstract native input contract is specialized by concrete runtime types");
        }

        return new SurfaceClassification(
            Array.Empty<string>(),
            "unclassified",
            "native input member requires explicit source classification");
    }

    private static string ResolveCoverage(SurfaceClassification classification)
    {
        if (classification.OptionIds.Length > 0)
        {
            if (classification.OptionIds.All(id =>
                    OptionCapabilityRegistrySource.TryGet(id, out _)))
            {
                return "mapped_to_registered_option";
            }

            return classification.OptionIds.All(id =>
                    OptionCapabilityRegistrySource.TryGet(id, out _) ||
                    PendingSemanticActionCatalog.TryGet(id, out _))
                ? "mapped_to_catalogued_blocked_action"
                : "semantic_action_missing_registration";
        }

        return classification.ScopeDisposition switch
        {
            "source_branch_required" => "requires_branch_decompilation",
            "unclassified" => "unclassified",
            _ => "classified_non_semantic_surface"
        };
    }

    private static string SurfaceId(string relative, string signature)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(relative + "#" + signature));
        return "native." + Convert.ToHexString(bytes.AsSpan(0, 8)).ToLowerInvariant();
    }
}
