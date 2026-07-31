using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace StardewAI.KnowledgeCompiler;

internal sealed record NativeActionSurface(
    string SurfaceId,
    string Family,
    string RuntimeType,
    string Member,
    string RelativeSourcePath,
    string[] MappedOptionIds,
    string SemanticCoverageStatus);

internal sealed class NativeActionSurfaceCatalog
{
    public string SourceStatus { get; init; } = "decompile_root_not_supplied";
    public string DecompileRoot { get; init; } = string.Empty;
    public IReadOnlyList<NativeActionSurface> Surfaces { get; init; } =
        Array.Empty<NativeActionSurface>();
}

internal sealed partial class NativeActionSurfaceCatalogBuilder
{
    private static readonly string[] PlayerEntryMembers =
    {
        "checkAction",
        "performAction",
        "DoFunction",
        "beginUsing",
        "onRelease",
        "performUseAction",
        "placementAction",
        "receiveLeftClick",
        "receiveRightClick",
        "receiveKeyPress"
    };

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

        var rows = new List<NativeActionSurface>();
        foreach (var path in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(root, path).Replace('\\', '/');
            if (!IsActionBearingPath(relative))
                continue;

            var source = File.ReadAllText(path);
            var runtimeType = Path.GetFileNameWithoutExtension(path);
            foreach (Match match in PlayerEntryMethodRegex().Matches(source))
            {
                var member = match.Groups["member"].Value;
                var family = ResolveFamily(relative, member);
                var mapped = ResolveMappedOptions(runtimeType, member);
                rows.Add(new NativeActionSurface(
                    SurfaceId(relative, member),
                    family,
                    runtimeType,
                    member,
                    relative,
                    mapped,
                    ResolveCoverage(mapped)));
            }

            if (ImplementsMinigameRegex().IsMatch(source))
            {
                var mapped = ResolveMappedOptions(runtimeType, "IMinigame");
                rows.Add(new NativeActionSurface(
                    SurfaceId(relative, "IMinigame"),
                    "minigame",
                    runtimeType,
                    "IMinigame",
                    relative,
                    mapped,
                    ResolveCoverage(mapped)));
            }
        }

        var surfaces = rows
            .GroupBy(row => row.SurfaceId, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(row => row.Family, StringComparer.Ordinal)
            .ThenBy(row => row.RuntimeType, StringComparer.Ordinal)
            .ThenBy(row => row.Member, StringComparer.Ordinal)
            .ToArray();
        var requiredFamilies = new[] { "tool", "menu", "location_interaction" };
        var discoveredFamilies = surfaces
            .Select(row => row.Family)
            .ToHashSet(StringComparer.Ordinal);
        var scanComplete = surfaces.Length >= 50 &&
            requiredFamilies.All(discoveredFamilies.Contains);

        return new NativeActionSurfaceCatalog
        {
            SourceStatus = scanComplete
                ? "native_decompile_scanned"
                : "native_decompile_scan_incomplete",
            DecompileRoot = root,
            Surfaces = surfaces
        };
    }

    private static bool IsActionBearingPath(string relative)
    {
        return relative.Contains("/Tools/", StringComparison.Ordinal) ||
            relative.Contains("/Menus/", StringComparison.Ordinal) ||
            relative.Contains("/Minigames/", StringComparison.Ordinal) ||
            relative.Contains("/Locations/", StringComparison.Ordinal) ||
            relative.Contains("/TerrainFeatures/", StringComparison.Ordinal) ||
            relative.EndsWith("/GameLocation.cs", StringComparison.Ordinal) ||
            relative.EndsWith("/Object.cs", StringComparison.Ordinal) ||
            relative.EndsWith("/Item.cs", StringComparison.Ordinal);
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

    private static string[] ResolveMappedOptions(string runtimeType, string member)
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
            _ when member is "checkAction" or "performAction" => new[] { "executor.interact" },
            _ => Array.Empty<string>()
        };

        return options.OrderBy(value => value, StringComparer.Ordinal).ToArray();
    }

    private static string ResolveCoverage(IReadOnlyCollection<string> mapped)
    {
        if (mapped.Count == 0)
            return "unclassified";
        return mapped.Count == 1 && mapped.Contains("executor.interact", StringComparer.Ordinal)
            ? "generic_interaction_only"
            : "mapped_to_registered_option";
    }

    private static string SurfaceId(string relative, string member)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(relative + "#" + member));
        return "native." + Convert.ToHexString(bytes.AsSpan(0, 8)).ToLowerInvariant();
    }

    [GeneratedRegex(
        @"\b(?:public|protected|internal)\s+(?:(?:override|virtual|static|sealed|new)\s+)*(?:[\w<>,?.\[\]]+\s+)+(?<member>checkAction|performAction|DoFunction|beginUsing|onRelease|performUseAction|placementAction|receiveLeftClick|receiveRightClick|receiveKeyPress)\s*\(",
        RegexOptions.CultureInvariant)]
    private static partial Regex PlayerEntryMethodRegex();

    [GeneratedRegex(@":\s*[^{\r\n]*\bIMinigame\b", RegexOptions.CultureInvariant)]
    private static partial Regex ImplementsMinigameRegex();
}
