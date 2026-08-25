using System.Security.Cryptography;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace StardewAI.KnowledgeCompiler;

internal sealed record NativeActionBranch(
    string BranchId,
    string SurfaceId,
    string RuntimeType,
    string Member,
    string Signature,
    string RelativeSourcePath,
    string BranchKind,
    string Anchor,
    int StartLine,
    int EndLine,
    string SourceSha256,
    string[] InvokedMembers,
    string[] ConstructedTypes,
    string[] StringLiterals,
    string[] ItemIds,
    string[] MappedActionIds,
    string SemanticDisposition,
    string CoverageStatus,
    string EvidenceBasis);

internal sealed class NativeActionBranchCatalog
{
    public string SourceStatus { get; init; } = "decompile_root_not_supplied";
    public IReadOnlyList<NativeActionBranch> Branches { get; init; } =
        Array.Empty<NativeActionBranch>();
    public IReadOnlyList<string> MissingSurfaceIds { get; init; } =
        Array.Empty<string>();
}

internal sealed class NativeActionBranchCatalogBuilder
{
    public NativeActionBranchCatalog Build(
        string? decompileRoot,
        NativeActionSurfaceCatalog surfaces)
    {
        if (string.IsNullOrWhiteSpace(decompileRoot))
            return new NativeActionBranchCatalog();

        var root = Path.GetFullPath(decompileRoot);
        if (!Directory.Exists(root))
        {
            return new NativeActionBranchCatalog
            {
                SourceStatus = "decompile_root_missing"
            };
        }

        var broadSurfaces = surfaces.Surfaces
            .Where(row => row.SemanticCoverageStatus == "requires_branch_decompilation")
            .ToDictionary(row => row.SurfaceId, StringComparer.Ordinal);
        var sourceIndex = DecompiledActionSourceIndex.Build(root);
        var methodByIdentity = sourceIndex.Methods.ToDictionary(
            row => MethodIdentity(row.RelativeSourcePath, row.Signature),
            StringComparer.Ordinal);
        var branches = new List<NativeActionBranch>();
        var missing = new List<string>();

        foreach (var surface in broadSurfaces.Values)
        {
            if (!methodByIdentity.TryGetValue(
                    MethodIdentity(surface.RelativeSourcePath, surface.Signature),
                    out var method))
            {
                missing.Add(surface.SurfaceId);
                continue;
            }

            var extracted = Extract(surface, method).ToArray();
            if (extracted.Length == 0)
            {
                missing.Add(surface.SurfaceId);
                continue;
            }
            branches.AddRange(extracted);
        }

        return new NativeActionBranchCatalog
        {
            SourceStatus = missing.Count == 0
                ? "native_branch_syntax_scanned"
                : "native_branch_syntax_scan_incomplete",
            Branches = branches
                .OrderBy(row => row.RelativeSourcePath, StringComparer.Ordinal)
                .ThenBy(row => row.StartLine)
                .ThenBy(row => row.BranchId, StringComparer.Ordinal)
                .ToArray(),
            MissingSurfaceIds = missing.OrderBy(value => value, StringComparer.Ordinal).ToArray()
        };
    }

    private static IEnumerable<NativeActionBranch> Extract(
        NativeActionSurface surface,
        DecompiledActionMethod method)
    {
        var methodSyntax = method.Syntax;
        var switchSections = methodSyntax.DescendantNodes()
            .OfType<SwitchSectionSyntax>()
            .ToArray();
        foreach (var section in switchSections)
        {
            var labels = section.Labels.Select(LabelText).ToArray();
            yield return Create(
                surface,
                methodSyntax.SyntaxTree,
                section,
                "switch_section",
                string.Join(" | ", labels));
        }

        foreach (var statement in methodSyntax.DescendantNodes().OfType<IfStatementSyntax>())
        {
            if (statement.Ancestors().OfType<IfStatementSyntax>().Any() ||
                statement.Ancestors().OfType<SwitchSectionSyntax>().Any())
            {
                continue;
            }

            yield return Create(
                surface,
                methodSyntax.SyntaxTree,
                statement.Statement,
                "conditional_true",
                statement.Condition.WithoutTrivia().ToString());
            if (statement.Else is not null)
            {
                yield return Create(
                    surface,
                    methodSyntax.SyntaxTree,
                    statement.Else.Statement,
                    "conditional_false",
                    "!(" + statement.Condition.WithoutTrivia() + ")");
            }
        }

        var directStatements = methodSyntax.Body?.Statements
            .Where(row => row is not IfStatementSyntax and not SwitchStatementSyntax)
            .ToArray() ?? Array.Empty<StatementSyntax>();
        if (directStatements.Length > 0)
        {
            yield return Create(
                surface,
                methodSyntax.SyntaxTree,
                methodSyntax,
                "method_envelope",
                "method-level setup and fallback surrounding extracted branches");
        }
        else if (switchSections.Length == 0 &&
                 !methodSyntax.DescendantNodes().OfType<IfStatementSyntax>().Any())
        {
            yield return Create(
                surface,
                methodSyntax.SyntaxTree,
                methodSyntax.Body ?? (SyntaxNode)methodSyntax,
                "method_root",
                "entire method body");
        }
    }

    private static NativeActionBranch Create(
        NativeActionSurface surface,
        SyntaxTree tree,
        SyntaxNode node,
        string branchKind,
        string anchor)
    {
        var span = tree.GetLineSpan(node.Span);
        var body = node.ToFullString();
        var calls = node.DescendantNodesAndSelf()
            .OfType<InvocationExpressionSyntax>()
            .Select(row => row.Expression.WithoutTrivia().ToString())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        var constructedTypes = node.DescendantNodesAndSelf()
            .OfType<ObjectCreationExpressionSyntax>()
            .Select(row => row.Type.WithoutTrivia().ToString())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        var literals = node.DescendantTokens()
            .Where(row => row.IsKind(SyntaxKind.StringLiteralToken))
            .Select(row => row.ValueText)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        var itemIds = literals
            .Where(value => value.StartsWith("(", StringComparison.Ordinal) &&
                value.Contains(')'))
            .ToArray();
        var sourceHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(body))).ToLowerInvariant();
        var branchId = BranchId(surface.SurfaceId, branchKind, anchor, span.StartLinePosition.Line);
        var classification = NativeBranchSemanticClassifier.Classify(
            surface,
            branchKind,
            anchor,
            calls,
            constructedTypes,
            literals);

        return new(
            branchId,
            surface.SurfaceId,
            surface.RuntimeType,
            surface.Member,
            surface.Signature,
            surface.RelativeSourcePath,
            branchKind,
            anchor,
            span.StartLinePosition.Line + 1,
            span.EndLinePosition.Line + 1,
            sourceHash,
            calls,
            constructedTypes,
            literals,
            itemIds,
            classification.ActionIds,
            classification.Disposition,
            classification.CoverageStatus,
            classification.EvidenceBasis);
    }

    private static string LabelText(SwitchLabelSyntax label) => label switch
    {
        CaseSwitchLabelSyntax value => "case " + value.Value.WithoutTrivia(),
        CasePatternSwitchLabelSyntax value => "case " + value.Pattern.WithoutTrivia(),
        DefaultSwitchLabelSyntax => "default",
        _ => label.WithoutTrivia().ToString()
    };

    private static string BranchId(
        string surfaceId,
        string branchKind,
        string anchor,
        int zeroBasedLine)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(
            surfaceId + "#" + branchKind + "#" + anchor + "#" + zeroBasedLine));
        return "native_branch." + Convert.ToHexString(bytes.AsSpan(0, 8)).ToLowerInvariant();
    }

    private static string MethodIdentity(string relative, string signature) =>
        relative + "#" + signature;
}

internal sealed record NativeBranchClassification(
    string[] ActionIds,
    string Disposition,
    string CoverageStatus,
    string EvidenceBasis);

internal static class NativeBranchSemanticClassifier
{
    public static string[] ClassifyActionToken(string token)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        AddActionToken(result, token);
        return result.OrderBy(value => value, StringComparer.Ordinal).ToArray();
    }

    public static NativeBranchClassification Classify(
        NativeActionSurface surface,
        string branchKind,
        string anchor,
        IReadOnlyList<string> calls,
        IReadOnlyList<string> constructedTypes,
        IReadOnlyList<string> literals)
    {
        if (branchKind == "method_envelope")
        {
            return new(
                Array.Empty<string>(),
                "method_control_flow_envelope",
                "classified_non_semantic_branch",
                "method envelope preserves source identity; nested action branches are catalogued separately");
        }

        var actionIds = ResolveActionIds(surface, anchor, calls, constructedTypes, literals);
        if (actionIds.Length > 0)
        {
            var known = actionIds.All(id =>
                StardewAI.Contracts.Capabilities.OptionCapabilityRegistrySource.TryGet(id, out _) ||
                StardewAI.Contracts.Capabilities.PendingSemanticActionCatalog.TryGet(id, out _));
            return new(
                actionIds,
                "player_semantic_action",
                known ? "mapped_to_semantic_action" : "semantic_action_missing_registration",
                "native branch syntax plus explicit menu/call/action-token mapping");
        }

        if (calls.Any(value => value.StartsWith("base.", StringComparison.Ordinal)))
        {
            return new(
                Array.Empty<string>(),
                "delegates_to_parent_surface",
                "classified_non_semantic_branch",
                "native branch delegates to the inherited action surface");
        }

        if (IsGuardBranch(anchor, calls, constructedTypes))
        {
            return new(
                Array.Empty<string>(),
                "guard_or_error_branch",
                "classified_non_semantic_branch",
                "branch only rejects, logs, or reports an unavailable native action");
        }

        if (calls.Count == 0 && constructedTypes.Count == 0)
        {
            return new(
                Array.Empty<string>(),
                "guard_or_local_state_outcome",
                "classified_non_semantic_branch",
                "branch has no invocation or constructed gameplay/menu surface");
        }

        if (branchKind == "method_root" &&
            calls.All(value => value is "ArgUtility.SplitBySpace" or "performAction"))
        {
            return new(
                Array.Empty<string>(),
                "argument_adapter",
                "classified_non_semantic_branch",
                "native overload only parses arguments and delegates to the typed overload");
        }

        if (surface.RuntimeType == "Object" &&
            branchKind == "switch_section" &&
            (anchor.StartsWith("case ", StringComparison.Ordinal) &&
             !anchor.Contains('"')))
        {
            return new(
                Array.Empty<string>(),
                "implementation_outcome_branch",
                "classified_non_semantic_branch",
                "numeric/character switch selects the native implementation of an enclosing item action");
        }

        if (IsDispatcherOrImplementationBranch(surface, branchKind, anchor, calls))
        {
            return new(
                Array.Empty<string>(),
                "delegates_to_child_or_selects_implementation",
                "classified_non_semantic_branch",
                "explicit native dispatcher/implementation branch; the child semantic action is catalogued at its owning branch");
        }

        return new(
            Array.Empty<string>(),
            "branch_semantics_unresolved",
            "requires_semantic_review",
            "branch extracted with source evidence but no explicit semantic mapping exists");
    }

    private static bool IsGuardBranch(
        string anchor,
        IReadOnlyList<string> calls,
        IReadOnlyList<string> constructedTypes)
    {
        if (constructedTypes.Count > 0)
            return false;
        if (anchor.StartsWith("!ArgUtility.TryGet(", StringComparison.Ordinal) ||
            anchor.StartsWith("ShouldIgnoreAction(", StringComparison.Ordinal) ||
            anchor.StartsWith("!Game1.player.canMove", StringComparison.Ordinal))
        {
            return true;
        }

        return calls.Count > 0 && calls.All(value =>
            value.EndsWith("LogError", StringComparison.Ordinal) ||
            value.EndsWith("showRedMessage", StringComparison.Ordinal) ||
            value.EndsWith("showRedMessageUsingLoadString", StringComparison.Ordinal) ||
            value.EndsWith("drawObjectDialogue", StringComparison.Ordinal) ||
            value.EndsWith("LoadString", StringComparison.Ordinal) ||
            value.EndsWith("Contains", StringComparison.Ordinal));
    }

    private static bool IsDispatcherOrImplementationBranch(
        NativeActionSurface surface,
        string branchKind,
        string anchor,
        IReadOnlyList<string> calls)
    {
        if (surface.RuntimeType == "GameLocation" && surface.Member == "checkAction")
        {
            return anchor is "farmer != Game1.player" or
                "currentEvent != null && currentEvent.isFestival" or
                "val4 == null || !((Component)val4).Properties.TryGetValue(\"Action\", out var value4)" ||
                anchor.Contains("character.checkAction(", StringComparison.Ordinal) ||
                calls.Any(value => value is
                    "character.checkAction" or
                    "largeTerrainFeature.performUseAction" or
                    "nPC.checkAction");
        }

        if (surface.RuntimeType == "GameLocation" &&
            surface.Member == "performTouchAction" &&
            branchKind == "switch_section" &&
            anchor.StartsWith("case ", StringComparison.Ordinal) &&
            !anchor.Contains('\''))
        {
            return true;
        }

        if (surface.RuntimeType == "IslandWest" &&
            surface.Member == "performAction" &&
            calls.Count == 1 &&
            calls[0] == "((Point)(ref parsed))._002Ector")
        {
            return true;
        }

        return surface.RuntimeType == "MovieTheater" &&
            surface.Member == "checkAction" &&
            calls.Count == 1 &&
            calls[0] == "performAction";
    }

    private static string[] ResolveActionIds(
        NativeActionSurface surface,
        string anchor,
        IReadOnlyList<string> calls,
        IReadOnlyList<string> constructedTypes,
        IReadOnlyList<string> literals)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        AddMenuActions(result, constructedTypes);
        AddCallActions(result, calls, constructedTypes);
        AddRuntimeSpecificActions(result, surface, anchor, calls, literals);

        if (calls.Any(value => value.EndsWith("TryOpenShopMenu", StringComparison.Ordinal)))
        {
            result.Add("executor.buy_shop_item");
            result.Add("executor.sell_shop_item");
        }
        if (calls.Any(value => value.EndsWith("warpFarmer", StringComparison.Ordinal) ||
            value.EndsWith("MinecartWarp", StringComparison.Ordinal)))
        {
            result.Add("exploration.visit_location");
        }
        if (calls.Any(value => value.EndsWith("createQuestionDialogue", StringComparison.Ordinal)))
            result.Add("executor.choose_dialogue_response");
        if (calls.Any(value => value.EndsWith("addItemByMenuIfNecessary", StringComparison.Ordinal) ||
            value.EndsWith("addItemByMenuIfNecessaryElseHoldUp", StringComparison.Ordinal)))
        {
            result.Add("executor.interact");
        }

        if (surface.RuntimeType == "Object" && surface.Member == "performUseAction")
        {
            if (anchor.Contains("name.Contains(\"Totem\")", StringComparison.Ordinal))
            {
                result.Add("executor.use_treasure_totem");
                result.Add("executor.use_rain_totem");
                result.Add("executor.use_warp_totem");
            }
            if (anchor.Contains("(O)TreasureTotem", StringComparison.Ordinal))
                result.Add("executor.use_treasure_totem");
            if (anchor.Contains("(O)681", StringComparison.Ordinal))
                result.Add("executor.use_rain_totem");
            if (anchor.Contains("(O)261", StringComparison.Ordinal) ||
                anchor.Contains("(O)688", StringComparison.Ordinal) ||
                anchor.Contains("(O)689", StringComparison.Ordinal) ||
                anchor.Contains("(O)690", StringComparison.Ordinal) ||
                anchor.Contains("(O)886", StringComparison.Ordinal))
            {
                result.Add("executor.use_warp_totem");
            }
            if (literals.Any(value => value is "(O)79" or "(O)842"))
                result.Add("executor.read_secret_note");
            if (anchor.Contains("(O)911", StringComparison.Ordinal) ||
                literals.Contains("(O)911", StringComparer.Ordinal))
                result.Add("executor.use_horse_flute");
            if (anchor.Contains("(O)879", StringComparison.Ordinal) ||
                literals.Contains("(O)879", StringComparer.Ordinal))
                result.Add("executor.use_monster_musk");
            if (calls.Any(value => value.EndsWith("readBook", StringComparison.Ordinal)))
                result.Add("executor.read_book");
        }

        if (surface.RuntimeType == "Object" && surface.Member == "placementAction")
            AddObjectPlacementActions(result, anchor, calls, literals);

        foreach (var actionToken in SwitchStringValues(anchor))
            AddActionToken(result, actionToken);

        return result.OrderBy(value => value, StringComparer.Ordinal).ToArray();
    }

    private static void AddCallActions(
        ISet<string> result,
        IReadOnlyList<string> calls,
        IReadOnlyList<string> constructedTypes)
    {
        if (calls.Contains("Game1.player.eatObject", StringComparer.Ordinal))
            result.Add("executor.consume_food");
        if (calls.Any(value => value.Contains("grangeDisplay", StringComparison.Ordinal) ||
            value.Contains("grangeMutex", StringComparison.Ordinal)))
        {
            result.Add("festival.manage_grange_display");
        }
        if (constructedTypes.Contains("StrengthGame", StringComparer.Ordinal))
            result.Add("festival.play_strength_game");
        if (calls.Any(value => value is "Game1.PlayEvent" or "startEvent"))
            result.Add("story.advance_event");
        if (calls.Any(value => value is "ShowMineCartMenu" or "Game1.enterMine"))
            result.Add("exploration.visit_location");
        if (calls.Any(value => value is "checkBundle" or "numberOfCompleteBundles"))
            result.Add("executor.donate_community_center_item");
        if (calls.Contains("OnDesertTrader", StringComparer.Ordinal))
        {
            result.Add("executor.buy_shop_item");
            result.Add("executor.sell_shop_item");
        }
        if (calls.Contains("OnCamel", StringComparer.Ordinal))
            result.Add("executor.interact");
        if (calls.Any(value => value is "openFarmhandInventory" or "CheckLostAndFound" ||
            value.Contains("fridge.Value.checkForAction", StringComparison.Ordinal)))
        {
            result.Add("inventory.transfer_item");
        }
        if (calls.Contains("readLedgerBook", StringComparer.Ordinal))
            result.Add("multiplayer.manage_wallet");
        if (calls.Contains("gil", StringComparer.Ordinal))
            result.Add("rewards.claim_adventure_guild_reward");
        if (calls.Contains("mailbox", StringComparer.Ordinal))
            result.Add("mail.process_letter");
        if (calls.Contains("readNote", StringComparer.Ordinal))
            result.Add("executor.read_secret_note");
        if (calls.Contains("HandleBuyAction", StringComparer.Ordinal))
        {
            result.Add("executor.buy_shop_item");
            result.Add("executor.sell_shop_item");
        }
        if (calls.Contains("child.toss", StringComparer.Ordinal))
            result.Add("executor.social_interact");
        if (calls.Contains("Game1.player.changeIntoSwimsuit", StringComparer.Ordinal) ||
            calls.Contains("Game1.player.changeOutOfSwimSuit", StringComparer.Ordinal) ||
            calls.Contains("openDoor", StringComparer.Ordinal))
        {
            result.Add("exploration.visit_location");
        }
        if (calls.Contains("Game1.player.team.movieMutex.RequestLock", StringComparer.Ordinal) ||
            calls.Contains("_ShowMovieStartReady", StringComparer.Ordinal))
        {
            result.Add("social.watch_movie");
        }
        if (calls.Contains("character.grantConversationFriendship", StringComparer.Ordinal))
            result.Add("social.talk_npc");
        if (calls.Contains("Game1.enterMine", StringComparer.Ordinal))
            result.Add("executor.descend_ladder");
        if (calls.Contains("Game1.createRadialDebris", StringComparer.Ordinal) &&
            calls.Contains("updateMineLevelData", StringComparer.Ordinal))
        {
            result.Add("executor.break_container");
        }
        if (calls.Any(value => value is
            "Game1.drawObjectDialogue" or
            "Game1.drawLetterMessage" or
            "Game1.multipleDialogues" or
            "Game1.drawDialogueNoTyping" or
            "showMonsterKillList" or
            "farmerFile" or
            "ShowQiCat" or
            "ShowNutHint"))
        {
            result.Add("executor.interact");
        }
    }

    private static void AddRuntimeSpecificActions(
        ISet<string> result,
        NativeActionSurface surface,
        string anchor,
        IReadOnlyList<string> calls,
        IReadOnlyList<string> literals)
    {
        if (surface.RuntimeType == "GameLocation" && surface.Member == "checkAction")
        {
            if (anchor == "who.IsSitting()" ||
                calls.Any(value => value is "who.BeginSitting" or "item.checkForAction" or
                    "furniture.checkForAction" or "who.mount.checkAction"))
            {
                result.Add("executor.interact");
            }
            if (anchor.StartsWith("objects.TryGetValue(", StringComparison.Ordinal))
            {
                result.Add("executor.collect_spawned_object");
                result.Add("executor.collect_machine_output");
                result.Add("executor.load_machine_input");
                result.Add("executor.load_crab_pot_bait");
                result.Add("executor.interact");
            }
        }

        if (surface.RuntimeType == "GameLocation" && surface.Member == "performTouchAction")
        {
            if (anchor == "case 'P'")
                result.Add("story.advance_event");
            if (anchor == "case 'D'" || anchor == "case 'W'")
                result.Add("exploration.visit_location");
            if (anchor == "case 'E'")
                result.Add("social.emote");
        }

        if (surface.RuntimeType == "GameLocation" && surface.Member == "performAction")
        {
            if (anchor is "case 0" or "case 1" or "case 2")
                result.Add("executor.social_interact");
            if (anchor.Contains("\"PlayEvent\"", StringComparison.Ordinal))
                result.Add("story.advance_event");
            if (anchor.Contains("\"LeoParrot\"", StringComparison.Ordinal) ||
                anchor.Contains("\"Starpoint\"", StringComparison.Ordinal) ||
                anchor.Contains("\"SpiritAltar\"", StringComparison.Ordinal))
            {
                result.Add("quest.advance");
            }
            if (anchor.Contains("\"BuildingSilo\"", StringComparison.Ordinal))
                result.Add("executor.transfer_material");
            if (anchor.Contains("\"AnimalShop.20\"", StringComparison.Ordinal))
                result.Add("executor.interact");
        }

        if (surface.RuntimeType == "Sign" && surface.Member == "checkForAction")
        {
            result.Add("executor.set_sign_display_item");
        }
        if (surface.RuntimeType == "Object" && surface.Member == "checkForAction" &&
            (anchor.Contains("IsTextSign()", StringComparison.Ordinal) ||
             calls.Any(value => value.EndsWith("CheckForActionOnTextSign", StringComparison.Ordinal))))
        {
            result.Add("executor.edit_text_sign");
        }
        if (surface.RuntimeType == "Object" && surface.Member == "checkForAction")
        {
            switch (anchor)
            {
                case "!justCheckingForActivity && who != null":
                    result.Add("recovery.escape_object_trap");
                    break;
                case "case \"(O)PotOfGold\"":
                    result.Add("rewards.claim_pot_of_gold");
                    break;
                case "case \"(BC)StatueOfTheDwarfKing\"":
                    result.Add("mining.choose_dwarf_statue_power");
                    break;
                case "case \"(BC)StatueOfBlessings\"":
                    result.Add("rewards.claim_statue_blessing");
                    break;
                case "case \"(BC)0\" | case \"(BC)1\" | case \"(BC)2\" | case \"(BC)3\" | case \"(BC)4\" | case \"(BC)5\" | case \"(BC)6\" | case \"(BC)7\"":
                    result.Add("world.rotate_house_plant");
                    break;
                case "case \"(BC)56\"":
                    result.Add("farming.collect_slime_ball");
                    break;
                case "case \"(BC)71\"":
                    result.Add("executor.descend_ladder");
                    break;
                case "case \"(BC)94\"":
                    result.Add("world.play_singing_stone");
                    break;
                case "case \"(BC)99\"":
                    result.Add("animals.withdraw_feed_hopper_hay");
                    break;
                case "case \"(BC)141\"":
                    result.Add("minigame.play_prairie_king");
                    break;
                case "case \"(BC)159\"":
                    result.Add("minigame.play_junimo_kart");
                    break;
                case "case \"(BC)165\"":
                    result.Add("animals.collect_auto_grabber_contents");
                    break;
                case "case \"(BC)238\"":
                    result.Add("movement.use_mini_obelisk");
                    break;
                case "case \"(BC)239\"":
                    result.Add("farming.read_farm_computer_report");
                    break;
                case "case \"(BC)247\"":
                    result.Add("tailoring.sew_item");
                    break;
                case "case \"(O)464\"":
                    result.Add("world.tune_flute_block");
                    break;
                case "case \"(O)463\"":
                    result.Add("world.tune_drum_block");
                    break;
            }
        }

        if (surface.RuntimeType is "IslandEast" or "IslandFarmCave" or "IslandSecret" or
            "IslandWest" or "IslandWestCave1" or "Railroad" or "VolcanoDungeon")
        {
            result.Add("quest.advance");
        }
        if (surface.RuntimeType == "IslandFieldOffice")
            result.Add("island.field_office_donate");
        if (surface.RuntimeType == "JojaMart")
        {
            result.Add("executor.purchase_joja_membership");
            result.Add("executor.purchase_joja_project");
        }
        if (surface.RuntimeType == "MermaidHouse")
            result.Add("quest.advance");
        if (surface.RuntimeType == "MineShaft" && anchor == "case 284")
            result.Add("mining.activate_calico_statue");
        if (surface.RuntimeType == "MineShaft" && anchor == "case 173")
            result.Add("executor.descend_ladder");
        if (surface.RuntimeType == "ManorHouse" &&
            anchor.Contains("\"MayorFridge\"", StringComparison.Ordinal))
        {
            result.Add("quest.advance");
        }
        if (surface.RuntimeType == "ManorHouse" &&
            anchor.Contains("\"LostAndFound\"", StringComparison.Ordinal))
        {
            result.Add("inventory.transfer_item");
        }
        if (surface.RuntimeType == "DesertFestival" &&
            literals.Contains("DesertMakeover", StringComparer.Ordinal))
        {
            result.Add("player.customize");
        }
        if ((surface.RuntimeType == "Event" || surface.RuntimeType == "DesertFestival") &&
            literals.Contains("pig", StringComparer.Ordinal))
        {
            result.Add("executor.interact");
        }
        if (surface.RuntimeType == "CommunityCenter")
            result.Add("executor.donate_community_center_item");
        if (surface.RuntimeType == "LibraryMuseum")
            result.Add("executor.interact");
        if (surface.RuntimeType is "FarmHouse" or "IslandFarmHouse")
            result.Add("inventory.transfer_item");
        if (surface.RuntimeType == "MovieTheater" &&
            anchor.Contains("\"Theater_Doors\"", StringComparison.Ordinal))
        {
            result.Add("social.watch_movie");
        }
    }

    private static void AddObjectPlacementActions(
        ISet<string> result,
        string anchor,
        IReadOnlyList<string> calls,
        IReadOnlyList<string> literals)
    {
        if (anchor.Contains("(O)TentKit", StringComparison.Ordinal))
            result.Add("executor.place_tent");
        if (anchor.Contains("(O)926", StringComparison.Ordinal))
            result.Add("executor.place_cookout_kit");
        if (anchor.Contains("(O)286", StringComparison.Ordinal) ||
            anchor.Contains("(O)287", StringComparison.Ordinal) ||
            anchor.Contains("(O)288", StringComparison.Ordinal))
        {
            result.Add("executor.place_bomb");
        }
        if (anchor.Contains("(O)893", StringComparison.Ordinal) ||
            anchor.Contains("(O)894", StringComparison.Ordinal) ||
            anchor.Contains("(O)895", StringComparison.Ordinal))
        {
            result.Add("executor.use_firework");
        }
        if (anchor.Contains("(O)297", StringComparison.Ordinal) ||
            anchor.Contains("(O)BlueGrassStarter", StringComparison.Ordinal))
        {
            result.Add("executor.plant_grass");
        }
        if (anchor.Contains("(O)710", StringComparison.Ordinal))
            result.Add("executor.place_crab_pot");
        if (anchor.Contains("(O)805", StringComparison.Ordinal) ||
            anchor.Contains("base.Category == -19", StringComparison.Ordinal))
        {
            result.Add("executor.apply_fertilizer");
        }
        if (anchor.Contains("(O)419", StringComparison.Ordinal))
            result.Add("executor.apply_tree_treatment");
        if (anchor.Contains("isSapling()", StringComparison.Ordinal) ||
            anchor.Contains("base.Category == -74", StringComparison.Ordinal))
        {
            result.Add("executor.plant_seed");
        }
        if (anchor.Contains("!bigCraftable.Value", StringComparison.Ordinal))
        {
            result.Add("executor.place_fence");
            result.Add("executor.place_flooring");
            result.Add("executor.place_tent");
            result.Add("executor.plant_grass");
            result.Add("executor.place_crab_pot");
            result.Add("executor.apply_fertilizer");
            result.Add("executor.apply_tree_treatment");
        }
        if (anchor.Contains("!performDropDownAction", StringComparison.Ordinal))
        {
            result.Add("executor.place_furniture");
            result.Add("executor.place_sign");
        }
        if (calls.Any(value => value.EndsWith("IsFenceItem", StringComparison.Ordinal)))
            result.Add("executor.place_fence");
        if (calls.Any(value => value.EndsWith("IsFloorPathItem", StringComparison.Ordinal)))
            result.Add("executor.place_flooring");
        if (calls.Any(value => value.EndsWith("HasContextTag", StringComparison.Ordinal)) &&
            literals.Contains("sign_item", StringComparer.Ordinal))
        {
            result.Add("executor.place_sign");
        }
        if (literals.Contains("(BC)BigChest", StringComparer.Ordinal))
            result.Add("executor.place_storage");
        if (anchor.Contains("(BC)", StringComparison.Ordinal))
            result.Add("executor.place_machine");
        if (anchor == "unconditional statements outside top-level if/switch")
        {
            result.Add("executor.place_machine");
            result.Add("executor.place_storage");
        }
    }

    private static void AddMenuActions(ISet<string> result, IEnumerable<string> types)
    {
        foreach (var type in types)
        {
            switch (type)
            {
                case "ShopMenu":
                    result.Add("executor.buy_shop_item");
                    result.Add("executor.sell_shop_item");
                    break;
                case "LetterViewerMenu":
                    result.Add("mail.process_letter");
                    break;
                case "MasteryTrackerMenu":
                    result.Add("skills.claim_mastery");
                    break;
                case "PrizeTicketMenu":
                    result.Add("rewards.claim_prize_ticket");
                    break;
                case "ForgeMenu":
                    result.Add("crafting.forge_item");
                    break;
                case "SpecialOrdersBoard":
                    result.Add("quest.accept_special_order");
                    break;
                case "TailoringMenu":
                    result.Add("tailoring.sew_item");
                    break;
                case "DyeMenu":
                    result.Add("tailoring.dye_item");
                    break;
                case "MineElevatorMenu":
                    result.Add("mining.use_elevator");
                    break;
                case "JunimoNoteMenu":
                    result.Add("executor.donate_community_center_item");
                    break;
                case "MuseumMenu":
                    result.Add("executor.donate_museum_item");
                    break;
                case "JojaCDMenu":
                    result.Add("executor.purchase_joja_project");
                    break;
                case "ItemGrabMenu":
                    result.Add("inventory.transfer_item");
                    break;
            }
        }
    }

    private static void AddActionToken(ISet<string> result, string token)
    {
        if (token is "Warp" or "LockedDoorWarp" or "ConditionalDoor" or "Door" or
            "MinecartTransport" or "EnterSewer" or "WarpCommunityCenter" or
            "WarpWomensLocker" or "WarpMensLocker" or "WarpGreenhouse" or
            "Warp_Sunroom_Door" or "ObeliskWarp")
        {
            result.Add("exploration.visit_location");
        }
        if (token is "Shop" or "OpenShop" or "Bookseller" or "JojaShop" or
            "AdventureShop" or "HospitalShop" or "Saloon" or "Carpenter" or
            "AnimalShop" or "Blacksmith" or "ClubShop" or "QiGemShop")
        {
            result.Add("executor.buy_shop_item");
            result.Add("executor.sell_shop_item");
        }
        if (token is "Sleep" or "Sleep2")
            result.Add("executor.sleep");
        if (token == "MineElevator")
            result.Add("mining.use_elevator");
        if (token is "Mine" or "NextMineLevel")
            result.Add("executor.descend_ladder");
        if (token == "ExitMine")
            result.Add("executor.exit_mine");
        if (token == "GoldenScythe")
            result.Add("mining.acquire_golden_scythe");
        if (token == "DropBox")
            result.Add("executor.quest_drop_box_donate");
        if (token == "Billboard")
            result.Add("quest.accept_daily");
        if (token == "SpecialOrders")
            result.Add("quest.accept_special_order");
        if (token == "PrizeMachine")
            result.Add("rewards.claim_prize_ticket");
        if (token == "Forge")
            result.Add("crafting.forge_item");
        if (token == "Tailoring")
            result.Add("tailoring.sew_item");
        if (token == "DyePot")
            result.Add("tailoring.dye_item");
        if (token == "Arcade_Prairie")
            result.Add("minigame.play_prairie_king");
        if (token == "Arcade_Minecart")
            result.Add("minigame.play_junimo_kart");
        if (token is "ClubSlots" or "QiCoins")
            result.Add("minigame.play_slots");
        if (token is "BlackJack" or "ClubCards")
            result.Add("minigame.play_calico_jack");
        if (token == "Bobbers")
            result.Add("player.choose_bobber");
        if (token == "Garbage")
            result.Add("foraging.rummage_garbage");
        if (token is "kitchen" or "Kitchen")
            result.Add("crafting.cook_recipe");
        if (token == "SpecialOrdersPrizeTickets")
            result.Add("rewards.claim_prize_ticket");
        if (token == "WizardBook")
            result.Add("buildings.construct");
        if (token is "ForestPylon" or "HMTGF" or "SandDragon" or
            "RailroadBox" or "TunnelSafe")
        {
            result.Add("quest.advance");
        }
        if (token == "SkullDoor")
            result.Add("mining.obtain_skull_key");
        if (token == "Crib")
            result.Add("housing.renovate");
        if (token == "Jukebox")
            result.Add("player.choose_jukebox_track");
        if (token == "Craft")
            result.Add("executor.craft_machine_item");
        if (token == "BuildingChest")
            result.Add("inventory.transfer_item");
        if (token == "BuildingToggleAnimalDoor")
            result.Add("animals.manage_animal");
        if (token is "QiCat" or "MonsterGrave" or "EmilyRoomObject" or "Tutorial" or
            "Message" or "MessageSpeech" or "Dialogue" or "NPCSpeechMessageNoRadius" or
            "NPCMessage" or "ElliottPiano" or "playSound" or "Letter" or "MessageOnce" or
            "Lamp" or "MineSign" or "FarmerFile" or "ClubComputer" or "DwarfGrave" or
            "Theater_Poster" or "Theater_PosterComingSoon" or
            "DesertFestivalMineExplanation")
        {
            result.Add("executor.interact");
        }
        if (token == "PlayEvent")
            result.Add("story.advance_event");
        if (token is "LeoParrot" or "Starpoint" or "SpiritAltar" or "MayorFridge")
            result.Add("quest.advance");
        if (token == "Mailbox")
            result.Add("mail.process_letter");
        if (token == "Notes")
            result.Add("executor.read_secret_note");
        if (token == "BuildingSilo")
            result.Add("executor.transfer_material");
        if (token == "LostAndFound")
            result.Add("inventory.transfer_item");
        if (token == "LedgerBook")
            result.Add("multiplayer.manage_wallet");
        if (token == "Theater_Doors")
            result.Add("social.watch_movie");
        if (token == "legendarySword")
            result.Add("quest.advance");

    }

    private static IEnumerable<string> SwitchStringValues(string anchor)
    {
        const string prefix = "case \"";
        foreach (var label in anchor.Split(" | ", StringSplitOptions.RemoveEmptyEntries))
        {
            if (label.StartsWith(prefix, StringComparison.Ordinal) &&
                label.EndsWith('"'))
            {
                yield return label[prefix.Length..^1];
            }
        }
    }
}
