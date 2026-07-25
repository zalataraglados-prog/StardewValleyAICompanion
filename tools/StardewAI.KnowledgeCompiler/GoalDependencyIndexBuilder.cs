using System.Globalization;
using System.Text.Json;

namespace StardewAI.KnowledgeCompiler;

internal sealed class GoalDependencyIndexBuilder
{
    private static readonly string[] GoalStatePaths =
    {
        "player.total_money_earned",
        "player.has_skull_key",
        "player.has_rusty_key",
        "player.married_or_roommate",
        "player.farmhouse_upgrade_level",
        "player.level",
        "world_progress.achievements",
        "world_progress.community_center",
        "npcs.friendships",
        "quests.mail_received"
    };

    public GoalDependencyIndex Build(
        IReadOnlyDictionary<string, PayloadAsset> assets,
        IReadOnlyList<AssemblyEvidenceIndex> assemblies,
        string? snapshotPath)
    {
        var issues = new List<GoalDependencyIssue>();
        var bundles = ParseBundles(assets, issues);
        var recipes = ParseRecipes(assets, issues);
        var criteria = BuildGrandpaCriteria();
        var methodEvidence = BuildMethodEvidence(assemblies, issues);
        var achievementEvidence = VerifyAchievements(assets, issues);
        SnapshotCoverageResult? fieldCoverage = null;

        if (!string.IsNullOrWhiteSpace(snapshotPath))
        {
            fieldCoverage = new SnapshotSchemaJoiner().Join(Path.GetFullPath(snapshotPath), GoalStatePaths);
            foreach (var field in fieldCoverage.Fields.Where(row =>
                         row.Coverage is "missing_from_snapshot_schema" or "not_a_field_envelope" or
                             "readable_missing_provenance" or "adapter_error" or "invalid_status"))
            {
                issues.Add(new(
                    "blocking",
                    "grandpa_score_input_not_transparent",
                    field.Path,
                    $"status={field.Status};coverage={field.Coverage};reason={field.Reason}"));
            }
        }
        else
        {
            issues.Add(new(
                "warning",
                "grandpa_score_snapshot_not_supplied",
                "grandpa.maximum_21",
                "score inputs were indexed but not joined to a full live transparent snapshot"));
        }

        var maximumScore = criteria.Sum(row => row.Points);
        if (maximumScore != 21)
        {
            issues.Add(new(
                "blocking",
                "grandpa_maximum_score_mismatch",
                "grandpa.maximum_21",
                $"expected=21;indexed={maximumScore}"));
        }

        return new(
            bundles,
            recipes,
            new(
                "grandpa.maximum_21",
                21,
                12,
                4,
                "The target is all 21 rule points. Four candles at 12 points are only an intermediate milestone.",
                criteria,
                methodEvidence,
                achievementEvidence,
                fieldCoverage?.Fields ?? Array.Empty<SnapshotFieldCoverage>()),
            issues,
            new(
                bundles.Count,
                bundles.Sum(row => row.Ingredients.Count),
                recipes.Count(row => row.Kind == "cooking_recipe"),
                recipes.Count(row => row.Kind == "crafting_recipe"),
                recipes.Sum(row => row.Ingredients.Count),
                recipes.Sum(row => row.Outputs.Count),
                criteria.Count,
                maximumScore,
                issues.Count(row => row.Severity == "blocking")));
    }

    private static IReadOnlyList<BundleDependencyEntry> ParseBundles(
        IReadOnlyDictionary<string, PayloadAsset> assets,
        ICollection<GoalDependencyIssue> issues)
    {
        if (!TryObject(assets, "Data/Bundles", out var payload))
        {
            issues.Add(new("blocking", "bundle_asset_missing", "Data/Bundles", "runtime payload is absent or not an object"));
            return Array.Empty<BundleDependencyEntry>();
        }

        var result = new List<BundleDependencyEntry>();
        foreach (var row in payload.EnumerateObject())
        {
            if (row.Value.ValueKind != JsonValueKind.String)
            {
                issues.Add(new("blocking", "bundle_definition_not_string", row.Name, row.Value.ValueKind.ToString()));
                continue;
            }

            var raw = row.Value.GetString() ?? string.Empty;
            var segments = raw.Split('/');
            if (segments.Length != 7)
            {
                issues.Add(new("blocking", "bundle_segment_count_invalid", row.Name, $"expected=7;actual={segments.Length}"));
                continue;
            }

            var key = row.Name.Split('/', 2);
            var bundleIndex = 0;
            if (key.Length != 2 || !Int(key[1], out bundleIndex))
                issues.Add(new("blocking", "bundle_key_invalid", row.Name, "expected <area>/<integer index>"));

            var ingredientTokens = Tokens(segments[2]);
            if (ingredientTokens.Length == 0 || ingredientTokens.Length % 3 != 0)
            {
                issues.Add(new("blocking", "bundle_ingredient_grammar_invalid", row.Name,
                    $"token_count={ingredientTokens.Length};expected non-empty triplets"));
                continue;
            }

            var ingredients = new List<BundleIngredientDependency>();
            for (var index = 0; index < ingredientTokens.Length; index += 3)
            {
                var id = ingredientTokens[index];
                if (!Int(ingredientTokens[index + 1], out var amount) || amount <= 0 ||
                    !Int(ingredientTokens[index + 2], out var quality) || quality < 0)
                {
                    issues.Add(new("blocking", "bundle_ingredient_value_invalid", row.Name,
                        string.Join(' ', ingredientTokens.Skip(index).Take(3))));
                    continue;
                }

                var matchKind = id == "-1"
                    ? "money_payment"
                    : Int(id, out var numericId) && numericId < 0
                        ? "item_category"
                        : "item_id";
                ingredients.Add(new(index / 3, id, amount, quality, matchKind));
            }

            if (!Int(segments[3], out var color))
                issues.Add(new("blocking", "bundle_color_invalid", row.Name, segments[3]));

            var requiredSlots = ingredients.Count;
            if (!string.IsNullOrWhiteSpace(segments[4]) &&
                (!Int(segments[4], out requiredSlots) || requiredSlots <= 0 || requiredSlots > ingredients.Count))
            {
                issues.Add(new("blocking", "bundle_required_slots_invalid", row.Name,
                    $"value={segments[4]};ingredient_count={ingredients.Count}"));
            }

            BundleRewardDependency? reward = null;
            if (!string.IsNullOrWhiteSpace(segments[1]))
            {
                var rewardTokens = Tokens(segments[1]);
                if (rewardTokens.Length != 3 || !Int(rewardTokens.ElementAtOrDefault(2) ?? string.Empty, out var amount) || amount <= 0)
                {
                    issues.Add(new("blocking", "bundle_reward_grammar_invalid", row.Name, segments[1]));
                }
                else
                {
                    var qualifiedId = QualifyStandardDescription(rewardTokens[0], rewardTokens[1]);
                    if (qualifiedId is null)
                        issues.Add(new("blocking", "bundle_reward_type_unresolved", row.Name, rewardTokens[0]));
                    reward = new(segments[1], rewardTokens[0], rewardTokens[1], qualifiedId, amount);
                }
            }

            result.Add(new(
                row.Name,
                key[0],
                key.Length == 2 && Int(key[1], out var parsedIndex) ? parsedIndex : bundleIndex,
                segments[0],
                segments[6],
                Int(segments[3], out var parsedColor) ? parsedColor : color,
                requiredSlots,
                segments[5],
                ingredients,
                reward,
                Hashing.Sha256(raw),
                "StardewValley.Menus.Bundle..ctor(int,string,bool[],Point,string,JunimoNoteMenu)"));
        }
        return result;
    }

    private static IReadOnlyList<RecipeDependencyEntry> ParseRecipes(
        IReadOnlyDictionary<string, PayloadAsset> assets,
        ICollection<GoalDependencyIssue> issues)
    {
        var result = new List<RecipeDependencyEntry>();
        ParseRecipeAsset(assets, "Data/CookingRecipes", "cooking_recipe", unlockSegment: 3, minimumSegments: 5, result, issues);
        ParseRecipeAsset(assets, "Data/CraftingRecipes", "crafting_recipe", unlockSegment: 4, minimumSegments: 6, result, issues);
        return result;
    }

    private static void ParseRecipeAsset(
        IReadOnlyDictionary<string, PayloadAsset> assets,
        string assetName,
        string kind,
        int unlockSegment,
        int minimumSegments,
        ICollection<RecipeDependencyEntry> result,
        ICollection<GoalDependencyIssue> issues)
    {
        if (!TryObject(assets, assetName, out var payload))
        {
            issues.Add(new("blocking", "recipe_asset_missing", assetName, "runtime payload is absent or not an object"));
            return;
        }

        foreach (var row in payload.EnumerateObject())
        {
            if (row.Value.ValueKind != JsonValueKind.String)
            {
                issues.Add(new("blocking", "recipe_definition_not_string", $"{assetName}:{row.Name}", row.Value.ValueKind.ToString()));
                continue;
            }

            var raw = row.Value.GetString() ?? string.Empty;
            var segments = raw.Split('/');
            if (segments.Length < minimumSegments)
            {
                issues.Add(new("blocking", "recipe_segment_count_invalid", $"{assetName}:{row.Name}",
                    $"minimum={minimumSegments};actual={segments.Length}"));
                continue;
            }

            var ingredients = ParseRecipePairs(segments[0], assetName, row.Name, "ingredient", allowImplicitCount: false, issues)
                .Select((pair, index) => new RecipeIngredientDependency(
                    index,
                    pair.Id,
                    pair.Count,
                    pair.Id == "-777"
                        ? "special_ingredient_rule"
                        : Int(pair.Id, out var numericId) && numericId < 0
                            ? "item_category"
                            : "item_id",
                    "CraftingRecipe.ItemMatchesForCrafting"))
                .ToArray();
            var outputs = ParseRecipePairs(segments[2], assetName, row.Name, "output", allowImplicitCount: true, issues).ToArray();
            var finalOutputCount = outputs.LastOrDefault().Count;
            if (finalOutputCount <= 0)
                finalOutputCount = 1;

            var unlockTokens = Tokens(segments[unlockSegment]);
            var unlockKind = unlockTokens.FirstOrDefault() ?? string.Empty;
            ValidateRecipeUnlock(assetName, row.Name, unlockTokens, issues);

            var bigCraftable = kind == "crafting_recipe" &&
                               bool.TryParse(segments[3], out var parsedBigCraftable) &&
                               parsedBigCraftable;

            result.Add(new(
                assetName,
                kind,
                row.Name,
                ingredients,
                outputs.Select((pair, index) => new RecipeOutputDependency(
                    index,
                    pair.Id,
                    finalOutputCount,
                    outputs.Length > 1 ? "random_choice_shared_final_pair_count" : "single",
                    bigCraftable ? "(BC)" : "ItemRegistry_resolved")).ToArray(),
                new(segments[unlockSegment], unlockKind, unlockTokens),
                bigCraftable,
                kind == "crafting_recipe" ? segments[3] : string.Empty,
                Hashing.Sha256(raw),
                "StardewValley.CraftingRecipe..ctor(string,bool)"));
        }
    }

    private static IReadOnlyList<(string Id, int Count)> ParseRecipePairs(
        string raw,
        string asset,
        string recipe,
        string role,
        bool allowImplicitCount,
        ICollection<GoalDependencyIssue> issues)
    {
        var tokens = Tokens(raw);
        if (tokens.Length == 0 || (!allowImplicitCount && tokens.Length % 2 != 0))
        {
            issues.Add(new("blocking", $"recipe_{role}_grammar_invalid", $"{asset}:{recipe}",
                $"token_count={tokens.Length};expected non-empty {(allowImplicitCount ? "id/count pairs with an optional final count" : "pairs")}"));
            return Array.Empty<(string, int)>();
        }

        var result = new List<(string, int)>();
        for (var index = 0; index < tokens.Length; index += 2)
        {
            var count = 1;
            if (index + 1 < tokens.Length && (!Int(tokens[index + 1], out count) || count <= 0))
            {
                issues.Add(new("blocking", $"recipe_{role}_count_invalid", $"{asset}:{recipe}",
                    string.Join(' ', tokens.Skip(index).Take(2))));
                continue;
            }
            result.Add((tokens[index], count));
        }
        return result;
    }

    private static void ValidateRecipeUnlock(
        string asset,
        string recipe,
        IReadOnlyList<string> tokens,
        ICollection<GoalDependencyIssue> issues)
    {
        var valid = tokens.Count switch
        {
            1 when tokens[0] is "default" or "null" => true,
            2 when tokens[0] == "l" && Int(tokens[1], out _) => true,
            3 when tokens[0] is "s" or "f" && Int(tokens[2], out _) => true,
            _ => false
        };
        if (!valid)
            issues.Add(new("blocking", "recipe_unlock_grammar_unclassified", $"{asset}:{recipe}", string.Join(' ', tokens)));
    }

    private static IReadOnlyList<GrandpaCriterion> BuildGrandpaCriteria() =>
        new[]
        {
            Criterion("money_50000", 1, "player.total_money_earned", "gte", "50000", "Game1.player.totalMoneyEarned >= 50000"),
            Criterion("money_100000", 1, "player.total_money_earned", "gte", "100000", "Game1.player.totalMoneyEarned >= 100000"),
            Criterion("money_200000", 1, "player.total_money_earned", "gte", "200000", "Game1.player.totalMoneyEarned >= 200000"),
            Criterion("money_300000", 1, "player.total_money_earned", "gte", "300000", "Game1.player.totalMoneyEarned >= 300000"),
            Criterion("money_500000", 1, "player.total_money_earned", "gte", "500000", "Game1.player.totalMoneyEarned >= 500000"),
            Criterion("money_1000000", 2, "player.total_money_earned", "gte", "1000000", "Game1.player.totalMoneyEarned >= 1000000"),
            Criterion("achievement_complete_collection", 1, "world_progress.achievements", "contains", "5", "Game1.player.achievements.Contains(5)"),
            Criterion("skull_key", 1, "player.has_skull_key", "equals", "true", "Game1.player.hasSkullKey"),
            Criterion("community_center_access_or_completion", 1, "world_progress.community_center", "accessible_or_completed", "true", "Game1.isLocationAccessible(\"CommunityCenter\") || Game1.player.hasCompletedCommunityCenter()"),
            Criterion("community_center_accessible_bonus", 2, "world_progress.community_center", "location_accessible", "true", "Game1.isLocationAccessible(\"CommunityCenter\")"),
            new("married_or_roommate_house_2", 1, new[] { "player.married_or_roommate", "player.farmhouse_upgrade_level" }, "and", "married_or_roommate=true;farmhouse_upgrade_level>=2", "Game1.player.isMarriedOrRoommates() && Utility.getHomeOfFarmer(Game1.player).upgradeLevel >= 2"),
            Criterion("rusty_key", 1, "player.has_rusty_key", "equals", "true", "Game1.player.hasRustyKey"),
            Criterion("achievement_master_angler", 1, "world_progress.achievements", "contains", "26", "Game1.player.achievements.Contains(26)"),
            Criterion("achievement_full_shipment", 1, "world_progress.achievements", "contains", "34", "Game1.player.achievements.Contains(34)"),
            Criterion("friendships_5", 1, "npcs.friendships", "count_points_gte_1975", "5", "Utility.getNumberOfFriendsWithinThisRange(Game1.player, 1975, 999999) >= 5"),
            Criterion("friendships_10", 1, "npcs.friendships", "count_points_gte_1975", "10", "Utility.getNumberOfFriendsWithinThisRange(Game1.player, 1975, 999999) >= 10"),
            Criterion("player_level_15", 1, "player.level", "gte", "15", "Game1.player.Level >= 15"),
            Criterion("player_level_25", 1, "player.level", "gte", "25", "Game1.player.Level >= 25"),
            Criterion("pet_love", 1, "quests.mail_received", "contains", "petLoveMessage", "Game1.player.mailReceived.Contains(\"petLoveMessage\")")
        };

    private static GrandpaCriterion Criterion(
        string id,
        int points,
        string statePath,
        string operation,
        string target,
        string nativeExpression) =>
        new(id, points, new[] { statePath }, operation, target, nativeExpression);

    private static IReadOnlyList<GrandpaMethodEvidence> BuildMethodEvidence(
        IReadOnlyList<AssemblyEvidenceIndex> assemblies,
        ICollection<GoalDependencyIssue> issues)
    {
        var result = new List<GrandpaMethodEvidence>();
        foreach (var methodName in new[] { "getGrandpaScore", "getGrandpaCandlesFromScore" })
        {
            var matches = assemblies.SelectMany(assembly =>
                    assembly.Types
                        .Where(type => type.FullName == "StardewValley.Utility")
                        .SelectMany(type => type.Methods
                            .Where(method => method.Name == methodName)
                            .Select(method => (Assembly: assembly, Type: type, Method: method))))
                .ToArray();
            if (matches.Length != 1)
            {
                issues.Add(new("blocking", "grandpa_method_identity_unresolved", methodName, $"match_count={matches.Length}"));
                continue;
            }

            var match = matches[0];
            var sourceCandidate = match.Type.SourceCandidates.SingleOrDefault(path =>
                                      path.EndsWith("Utility.cs", StringComparison.OrdinalIgnoreCase))
                                  ?? match.Type.SourceCandidates.FirstOrDefault()
                                  ?? string.Empty;
            var source = match.Assembly.SourceFiles.FirstOrDefault(row =>
                string.Equals(row.RelativePath, sourceCandidate, StringComparison.OrdinalIgnoreCase));
            if (match.Method.IlSha256 is null || match.Method.BodyStatus == "invalid_il_body")
                issues.Add(new("blocking", "grandpa_method_body_not_indexed", methodName, match.Method.BodyStatus));
            if (source is null)
                issues.Add(new("blocking", "grandpa_method_source_unresolved", methodName, sourceCandidate));

            result.Add(new(
                match.Assembly.AssemblyName,
                match.Assembly.AssemblySha256,
                match.Assembly.ModuleVersionId,
                match.Type.FullName,
                match.Method.Name,
                match.Method.MetadataToken,
                match.Method.SignatureSha256,
                match.Method.IlSha256,
                match.Method.BodyStatus,
                sourceCandidate,
                source?.Sha256));
        }
        return result;
    }

    private static IReadOnlyList<GrandpaAchievementEvidence> VerifyAchievements(
        IReadOnlyDictionary<string, PayloadAsset> assets,
        ICollection<GoalDependencyIssue> issues)
    {
        var expected = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["5"] = "A Complete Collection",
            ["26"] = "Master Angler",
            ["34"] = "Full Shipment"
        };
        if (!TryObject(assets, "Data/Achievements", out var payload))
        {
            issues.Add(new("blocking", "achievement_asset_missing", "Data/Achievements", "runtime payload is absent or not an object"));
            return Array.Empty<GrandpaAchievementEvidence>();
        }

        var result = new List<GrandpaAchievementEvidence>();
        foreach (var pair in expected)
        {
            if (!payload.TryGetProperty(pair.Key, out var value) || value.ValueKind != JsonValueKind.String)
            {
                issues.Add(new("blocking", "grandpa_achievement_missing", pair.Key, pair.Value));
                continue;
            }
            var raw = value.GetString() ?? string.Empty;
            var name = raw.Split('^').FirstOrDefault() ?? string.Empty;
            if (!string.Equals(name, pair.Value, StringComparison.Ordinal))
                issues.Add(new("blocking", "grandpa_achievement_identity_mismatch", pair.Key, $"expected={pair.Value};actual={name}"));
            result.Add(new(int.Parse(pair.Key, CultureInfo.InvariantCulture), name, raw, Hashing.Sha256(raw)));
        }
        return result;
    }

    private static bool TryObject(IReadOnlyDictionary<string, PayloadAsset> assets, string name, out JsonElement payload)
    {
        if (assets.TryGetValue(name, out var asset) && asset.Payload.ValueKind == JsonValueKind.Object)
        {
            payload = asset.Payload;
            return true;
        }
        payload = default;
        return false;
    }

    private static bool Int(string value, out int result) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result);

    private static string[] Tokens(string value) =>
        value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string? QualifyStandardDescription(string type, string itemId) =>
        type switch
        {
            "O" or "Object" or "R" or "Ring" or "BL" or "Blueprint" => "(O)" + itemId,
            "BO" or "BigObject" or "BBL" or "BBl" or "BigBlueprint" => "(BC)" + itemId,
            "F" or "Furniture" => "(F)" + itemId,
            "B" or "Boot" => "(B)" + itemId,
            "W" or "Weapon" => "(W)" + itemId,
            "H" or "Hat" => "(H)" + itemId,
            "C" when Int(itemId, out var numericId) => (numericId >= 1000 ? "(S)" : "(P)") + itemId,
            "C" => itemId,
            _ => null
        };
}

internal sealed record GoalDependencyIndex(
    IReadOnlyList<BundleDependencyEntry> Bundles,
    IReadOnlyList<RecipeDependencyEntry> Recipes,
    GrandpaGoalDefinition GrandpaGoal,
    IReadOnlyList<GoalDependencyIssue> Issues,
    GoalDependencySummary Summary);

internal sealed record GoalDependencySummary(
    int BundleCount,
    int BundleIngredientCount,
    int CookingRecipeCount,
    int CraftingRecipeCount,
    int RecipeIngredientCount,
    int RecipeOutputCount,
    int GrandpaCriterionCount,
    int GrandpaMaximumScore,
    int BlockingIssueCount);

internal sealed record BundleDependencyEntry(
    string Key,
    string Area,
    int BundleIndex,
    string Name,
    string Label,
    int Color,
    int RequiredSlots,
    string TextureOverride,
    IReadOnlyList<BundleIngredientDependency> Ingredients,
    BundleRewardDependency? Reward,
    string RawSha256,
    string NativeConstructor);

internal sealed record BundleIngredientDependency(
    int Index,
    string ItemIdOrCategory,
    int Amount,
    int MinimumQuality,
    string MatchKind);

internal sealed record BundleRewardDependency(
    string Raw,
    string Type,
    string ItemId,
    string? QualifiedItemId,
    int Amount);

internal sealed record RecipeDependencyEntry(
    string AssetName,
    string Kind,
    string Name,
    IReadOnlyList<RecipeIngredientDependency> Ingredients,
    IReadOnlyList<RecipeOutputDependency> Outputs,
    RecipeUnlockDependency Unlock,
    bool BigCraftable,
    string OutputTypeSegment,
    string RawSha256,
    string NativeConstructor);

internal sealed record RecipeIngredientDependency(
    int Index,
    string MatchKey,
    int Amount,
    string MatchKind,
    string NativeMatcher);

internal sealed record RecipeOutputDependency(
    int Index,
    string ItemId,
    int Amount,
    string Selection,
    string Qualification);

internal sealed record RecipeUnlockDependency(
    string Raw,
    string Kind,
    IReadOnlyList<string> Tokens);

internal sealed record GrandpaGoalDefinition(
    string GoalId,
    int TargetScore,
    int FourCandleThreshold,
    int MaximumCandles,
    string TargetPolicy,
    IReadOnlyList<GrandpaCriterion> Criteria,
    IReadOnlyList<GrandpaMethodEvidence> MethodEvidence,
    IReadOnlyList<GrandpaAchievementEvidence> AchievementEvidence,
    IReadOnlyList<SnapshotFieldCoverage> TransparentScoreInputs);

internal sealed record GrandpaCriterion(
    string Id,
    int Points,
    IReadOnlyList<string> StatePaths,
    string Operation,
    string Target,
    string NativeExpression);

internal sealed record GrandpaMethodEvidence(
    string AssemblyName,
    string AssemblySha256,
    string ModuleVersionId,
    string TypeName,
    string MethodName,
    string MetadataToken,
    string SignatureSha256,
    string? IlSha256,
    string BodyStatus,
    string SourceCandidate,
    string? SourceSha256);

internal sealed record GrandpaAchievementEvidence(
    int Id,
    string Name,
    string RawDefinition,
    string RawSha256);

internal sealed record GoalDependencyIssue(
    string Severity,
    string Code,
    string Subject,
    string Detail);
