using System.Text.Json;
using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Constants;
using StardewValley.GameData.WildTrees;
using StardewValley.TerrainFeatures;
using TileLocation = xTile.Dimensions.Location;
using TileRectangle = xTile.Dimensions.Rectangle;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private const string WildTreeProductNativeContract =
        "GameLocation.checkAction -> Tree.performUseAction -> Tree.shake; exact base Data/WildTrees seed branch; no direct tree, RNG, debris, inventory, or skill mutation";

    private void StartWildTreeProductHarvest(PendingExecution pending)
    {
        var request = pending.Request;
        var genericReasons = ValidateExecutionRequest(request);
        if (genericReasons.Count > 0) { pending.Completion.SetResult(Blocked(request, genericReasons.ToArray())); return; }
        if (!request.TargetTileX.HasValue || !request.TargetTileY.HasValue || !request.InteractionTileX.HasValue || !request.InteractionTileY.HasValue ||
            !request.StandTileX.HasValue || !request.StandTileY.HasValue || !request.SafeSlotIndex.HasValue || !request.RestoreSlotIndex.HasValue ||
            !request.ExpectedTreeHasSeedBefore.HasValue || !request.ExpectedTreeHasSeedAfter.HasValue ||
            !request.ExpectedTreeWasShakenTodayBefore.HasValue || !request.ExpectedTreeWasShakenTodayAfter.HasValue ||
            !request.ExpectedForagingExperienceDelta.HasValue || string.IsNullOrWhiteSpace(request.ExpectedOutputItemsJson) ||
            string.IsNullOrWhiteSpace(request.TreeProductOutputDomainJson))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "harvest_tree_product", "tree.has_seed=false", "request=missing_typed_fields", "harvest_tree_product_typed_target_fields_required"));
            return;
        }
        if (request.TreeProductNativeContract != WildTreeProductNativeContract ||
            request.TreeProductOutputDomainContract != "complete_stochastic_native_branch_domain_no_rng_consumed" ||
            request.TreeProductProjectionStatus != "exact_from_native_tree_performUseAction_shake_and_locked_wild_tree_data")
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "harvest_tree_product", "tree.has_seed=false", "native_contract=drifted", "harvest_tree_product_native_contract_mismatch"));
            return;
        }
        if (Game1.activeClickableMenu is not null || Game1.dialogueUp || Game1.player.UsingTool || !Game1.player.CanMove)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "harvest_tree_product", "tree.has_seed=false", "player=busy_or_menu_open", "harvest_tree_product_player_busy"));
            return;
        }
        if (!TryParseFruitTreeOutputs(request.ExpectedOutputItemsJson, out var requestedOutputs) || requestedOutputs.Count != 1)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "harvest_tree_product", "tree.has_seed=false", "guaranteed_outputs=invalid", "harvest_tree_product_output_projection_invalid"));
            return;
        }

        var location = Game1.currentLocation;
        var target = new Point(request.TargetTileX.Value, request.TargetTileY.Value);
        var interaction = new Point(request.InteractionTileX.Value, request.InteractionTileY.Value);
        var stand = new Point(request.StandTileX.Value, request.StandTileY.Value);
        var tree = location.terrainFeatures.TryGetValue(target.ToVector2(), out var feature) ? feature as Tree : null;
        var projection = tree is null ? null : ProjectRuntimeWildTreeProduct(tree);
        var reasons = ValidateRuntimeWildTreeProduct(location, tree, projection, requestedOutputs, target, interaction, stand, request);
        if (reasons.Length > 0)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "harvest_tree_product", "tree.has_seed=false", WildTreeProductObservedEffect(location, tree, target, null), reasons));
            return;
        }

        var questReason = ValidateQuestResourceSourceTarget(request, requestedOutputs.Select(output => output.QualifiedItemId));
        if (!string.IsNullOrWhiteSpace(questReason))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "harvest_tree_product", "tree.has_seed=false", WildTreeProductObservedEffect(location, tree, target, null), questReason));
            return;
        }
        var maxMovementTiles = Math.Clamp(request.MaxMovementTiles ?? 512, 1, 512);
        var path = TryBuildTilePath(location, Game1.player.TilePoint, stand, maxMovementTiles, out var pathReason, avoidSoftObstacles: true, allowRemovableObstacles: false);
        if (path is null)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "harvest_tree_product", "tree.has_seed=false", WildTreeProductObservedEffect(location, tree, target, null), "harvest_tree_product_path_unavailable:" + pathReason));
            return;
        }

        activeWildTreeProductHarvest = new ActiveWildTreeProductHarvest(pending, location, tree!, target, interaction, stand, path, projection!, maxMovementTiles);
    }

    private static string[] ValidateRuntimeWildTreeProduct(GameLocation location, Tree? tree, RuntimeWildTreeProductProjection? projection,
        IReadOnlyList<FruitTreeOutputExpectation> requestedOutputs, Point target, Point interaction, Point stand, TrainingExecutionRequest request)
    {
        var reasons = new List<string>();
        if (tree is null || tree.GetType() != typeof(Tree) || projection is null) return new[] { "harvest_tree_product_target_not_exact_vanilla_tree" };
        if (projection.Status != "ready") reasons.Add("harvest_tree_product_not_ready:" + projection.Status);
        if (request.TargetRuntimeType != typeof(Tree).FullName || request.TreeProductTreeType != tree.treeType.Value ||
            request.ExpectedTreeHasSeedBefore != tree.hasSeed.Value || request.ExpectedTreeHasSeedBefore != true || request.ExpectedTreeHasSeedAfter != false ||
            request.ExpectedTreeWasShakenTodayBefore != tree.wasShakenToday.Value || request.ExpectedTreeWasShakenTodayAfter != true || request.ExpectedForagingExperienceDelta != 0 ||
            !FruitTreeOutputsEqual(requestedOutputs, projection.GuaranteedOutputs) || !WildTreeJsonEquivalent(request.TreeProductOutputDomainJson, projection.OptionalDomainJson))
            reasons.Add("harvest_tree_product_projection_drifted");
        if (interaction != target || !AreAdjacent(stand, target) || !IsTileOnMap(location, stand) || !IsTileWalkable(location, stand) || IsTileOccupiedByCharacter(location, stand))
            reasons.Add("harvest_tree_product_interaction_geometry_drifted");
        var safe = request.SafeSlotIndex!.Value;
        var restore = request.RestoreSlotIndex!.Value;
        if (safe is < 0 or > 11 || restore is < 0 or > 11 || safe >= Game1.player.Items.Count || restore >= Game1.player.Items.Count ||
            Game1.player.Items[safe] is not null || Game1.player.CurrentToolIndex != restore)
            reasons.Add("harvest_tree_product_safe_slot_drifted");
        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    private void TickWildTreeProductHarvest()
    {
        if (activeWildTreeProductHarvest is null) return;
        var active = activeWildTreeProductHarvest;
        active.ElapsedTicks++;
        if (!Context.IsWorldReady || !ReferenceEquals(Game1.currentLocation, active.Location)) { CompleteWildTreeProductBlocked(active, "harvest_tree_product_location_changed"); return; }
        if (active.ElapsedTicks > active.MaxTicks) { CompleteWildTreeProductBlocked(active, "harvest_tree_product_timeout"); return; }
        if (!active.Location.terrainFeatures.TryGetValue(active.Target.ToVector2(), out var feature) || !ReferenceEquals(feature, active.Tree)) { CompleteWildTreeProductBlocked(active, "harvest_tree_product_target_removed_during_execution"); return; }
        if (active.ActionIssued)
        {
            var result = WildTreeProductPostconditionStatus(active);
            if (result == "verified") CompleteWildTreeProduct(active);
            else if (result != "pending") CompleteWildTreeProductBlocked(active, result);
            return;
        }
        if (Game1.player.UsingTool || Game1.activeClickableMenu is not null || Game1.dialogueUp) { CompleteWildTreeProductBlocked(active, "harvest_tree_product_player_busy_during_execution"); return; }

        var playerTile = Game1.player.TilePoint;
        if (playerTile != active.LastObservedTile)
        {
            active.MovementTiles += ManhattanDistance(active.LastObservedTile, playerTile);
            active.LastObservedTile = playerTile;
            if (active.MovementTiles > active.MaxMovementTiles) { CompleteWildTreeProductBlocked(active, "harvest_tree_product_movement_budget_exceeded"); return; }
        }
        if (playerTile != active.Stand)
        {
            if (active.PathIndex >= active.Path.Count) { CompleteWildTreeProductBlocked(active, "harvest_tree_product_path_exhausted_before_stand"); return; }
            var next = active.Path[active.PathIndex];
            if (playerTile == next) { active.PathIndex++; active.StuckTicks = 0; return; }
            if (!IsTileWalkable(active.Location, next) || IsTileOccupiedByCharacter(active.Location, next)) { CompleteWildTreeProductBlocked(active, "harvest_tree_product_dynamic_path_blocked"); return; }
            var moved = Vector2.DistanceSquared(active.LastPosition, Game1.player.Position) >= 0.01f;
            active.LastPosition = Game1.player.Position;
            StartMoving(DirectionTo(playerTile, next));
            MovePlayerForTick();
            if (Game1.player.TilePoint == next) active.PathIndex++;
            if (!moved && ++active.StuckTicks > 45) CompleteWildTreeProductBlocked(active, "harvest_tree_product_movement_stuck");
            else if (moved) active.StuckTicks = 0;
            return;
        }

        StopAllMovement();
        if (Game1.player.CurrentToolIndex != active.RestoreSlotIndex || Game1.player.Items[active.SafeSlotIndex] is not null)
        {
            CompleteWildTreeProductBlocked(active, "harvest_tree_product_safe_slot_drifted_before_action");
            return;
        }
        active.OutputCountsBefore = CaptureWildTreeProductOutputs(active.Location);
        active.ForagingExperienceBefore = Game1.player.experiencePoints[Farmer.foragingSkill];
        Game1.player.faceDirection(DirectionTo(playerTile, active.Interaction));
        var handled = false;
        try
        {
            Game1.player.CurrentToolIndex = active.SafeSlotIndex;
            handled = active.Location.checkAction(new TileLocation(active.Interaction.X, active.Interaction.Y),
                new TileRectangle(Game1.viewport.X, Game1.viewport.Y, Game1.viewport.Width, Game1.viewport.Height), Game1.player);
        }
        finally
        {
            Game1.player.CurrentToolIndex = active.RestoreSlotIndex;
        }
        active.ActionIssued = true;
        if (!handled) { CompleteWildTreeProductBlocked(active, "harvest_tree_product_native_action_not_handled"); return; }
        var post = WildTreeProductPostconditionStatus(active);
        if (post == "verified") CompleteWildTreeProduct(active);
        else if (post != "pending") CompleteWildTreeProductBlocked(active, post);
    }

    private static string WildTreeProductPostconditionStatus(ActiveWildTreeProductHarvest active)
    {
        if (active.OutputCountsBefore is null || !active.ForagingExperienceBefore.HasValue) return "harvest_tree_product_output_baseline_missing";
        if (active.Tree.hasSeed.Value) return active.ElapsedTicks < active.MaxTicks ? "pending" : "harvest_tree_product_seed_not_consumed";
        if (!active.Tree.wasShakenToday.Value) return "harvest_tree_product_shaken_flag_missing";
        if (Game1.player.experiencePoints[Farmer.foragingSkill] - active.ForagingExperienceBefore.Value != 0) return "harvest_tree_product_foraging_xp_drifted";
        var after = CaptureWildTreeProductOutputs(active.Location);
        var deltas = after.Keys.Union(active.OutputCountsBefore.Keys, StringComparer.Ordinal)
            .ToDictionary(key => key, key => after.GetValueOrDefault(key) - active.OutputCountsBefore.GetValueOrDefault(key), StringComparer.Ordinal);
        if (deltas.Any(row => row.Value < 0)) return "harvest_tree_product_unexpected_output_consumption";
        foreach (var output in active.Projection.GuaranteedOutputs)
        {
            var delta = deltas.GetValueOrDefault(output.Key);
            if (delta < output.Quantity) return "pending";
            if (delta != output.Quantity) return "harvest_tree_product_guaranteed_output_quantity_mismatch";
        }
        foreach (var row in deltas.Where(row => row.Value > 0 && active.Projection.GuaranteedOutputs.All(output => output.Key != row.Key)))
        {
            var separator = row.Key.LastIndexOf('|');
            if (separator <= 0 || !int.TryParse(row.Key[(separator + 1)..], out var quality) ||
                !active.Projection.OptionalRules.Any(rule => rule.Matches(row.Key[..separator], quality, row.Value)))
                return "harvest_tree_product_output_outside_native_domain:" + row.Key;
        }
        return "verified";
    }

    private void CompleteWildTreeProduct(ActiveWildTreeProductHarvest active)
    {
        StopAllMovement();
        Game1.player.CurrentToolIndex = active.RestoreSlotIndex;
        activeWildTreeProductHarvest = null;
        var after = CaptureWildTreeProductOutputs(active.Location);
        var changed = new List<SimulatedFactChange>
        {
            new() { Path = "current_location.terrain_features[" + active.Target.X + "," + active.Target.Y + "].has_seed", Before = "true", After = active.Tree.hasSeed.Value.ToString().ToLowerInvariant() },
            new() { Path = "current_location.terrain_features[" + active.Target.X + "," + active.Target.Y + "].was_shaken_today", Before = active.ExpectedWasShakenBefore.ToString().ToLowerInvariant(), After = active.Tree.wasShakenToday.Value.ToString().ToLowerInvariant() },
            new() { Path = "player.current_tool_index", Before = active.RestoreSlotIndex.ToString(), After = Game1.player.CurrentToolIndex.ToString() }
        };
        var beforeKeys = active.OutputCountsBefore is null
            ? Enumerable.Empty<string>()
            : active.OutputCountsBefore.Keys.AsEnumerable();
        foreach (var key in after.Keys.Union(beforeKeys, StringComparer.Ordinal).Where(key => after.GetValueOrDefault(key) != (active.OutputCountsBefore?.GetValueOrDefault(key) ?? 0)))
            changed.Add(new SimulatedFactChange { Path = "combined_inventory_debris_output[" + key + "]", Before = (active.OutputCountsBefore?.GetValueOrDefault(key) ?? 0).ToString(), After = after.GetValueOrDefault(key).ToString() });
        var request = active.Pending.Request;
        var result = new TrainingExecutionResult
        {
            RunId = request.RunId, QueueId = request.QueueId, QueueItemId = request.QueueItemId, BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId, Status = "applied", FeedbackAvailable = true, ActualTicks = active.ElapsedTicks,
            StartedAt = active.StartedAt, CompletedAt = DateTimeOffset.UtcNow.ToString("O"), TrainingImpactScope = "executor_calibration",
            PrimitiveKind = "harvest_tree_product", PrimitiveVerificationStatus = "verified",
            PrimitiveVerificationReasons = new[] { "native_checkAction_invoked_exact_tree", "native_seed_consumption_verified", "complete_output_domain_verified_without_rng_prediction", "zero_foraging_xp_delta_verified", "safe_empty_slot_restored" },
            RequestedEffect = active.RequestedEffect, ObservedEffect = WildTreeProductObservedEffect(active.Location, active.Tree, active.Target, after), ChangedFacts = changed.ToArray()
        };
        ApplyQuestResourceSourceFeedback(result, request);
        ApplySpecialOrderCollectSourceFeedback(result, request);
        active.Pending.Completion.SetResult(result);
    }

    private void CompleteWildTreeProductBlocked(ActiveWildTreeProductHarvest active, string reason)
    {
        StopAllMovement();
        Game1.player.CurrentToolIndex = active.RestoreSlotIndex;
        activeWildTreeProductHarvest = null;
        active.Pending.Completion.SetResult(BlockedWithPrimitive(active.Pending.Request, "harvest_tree_product", active.RequestedEffect,
            WildTreeProductObservedEffect(active.Location, active.Tree, active.Target, active.OutputCountsBefore), reason));
    }

    private static RuntimeWildTreeProductProjection ProjectRuntimeWildTreeProduct(Tree tree)
    {
        var data = tree.GetData();
        var dataStatus = ValidateRuntimeWildTreeData(tree.treeType.Value, data);
        var primaryId = tree.treeType.Value == "2" && tree.Location?.GetSeason() == Season.Fall && Game1.dayOfMonth >= 14 ? "(O)408" : data?.SeedItemId ?? string.Empty;
        var item = string.IsNullOrWhiteSpace(primaryId) ? null : ItemRegistry.Create(primaryId);
        var guaranteed = item is null ? Array.Empty<FruitTreeOutputExpectation>() : new[] { new FruitTreeOutputExpectation(item.QualifiedItemId, WildTreeRuntimeQuality(item), 1) };
        var rules = ProjectRuntimeWildTreeOptionalRules(tree);
        var status = tree.GetType() != typeof(Tree) ? "blocked_custom_tree_runtime_type" : dataStatus != "exact_locked_base_1.6.15" ? "blocked_wild_tree_data_contract_drift" :
            tree.growthStage.Value < Tree.treeStage ? "blocked_tree_not_mature" : tree.stump.Value ? "blocked_tree_is_stump" : tree.tapped.Value ? "blocked_tree_is_tapped" :
            !tree.hasSeed.Value ? "blocked_tree_has_no_seed" : !(Game1.IsMultiplayer || Game1.player.ForagingLevel >= 1) ? "blocked_foraging_level_below_native_seed_gate" :
            tree.maxShake != 0f ? "blocked_tree_shake_in_progress" : guaranteed.Length != 1 ? "blocked_guaranteed_output_projection_missing" : "ready";
        return new RuntimeWildTreeProductProjection(status, guaranteed, rules, WildTreeOptionalRulesJson(rules));
    }

    private static string ValidateRuntimeWildTreeData(string treeType, WildTreeData? data)
    {
        if (data is null || data.ShakeItems is not null) return "drifted";
        var expected = treeType switch
        {
            "1" => ("(O)309", 0.05f, "none"), "2" => ("(O)310", 0.05f, "hazelnut"), "3" => ("(O)311", 0.05f, "none"),
            "6" => ("(O)88", 0.05f, "golden_coconut"), "7" => ("(O)891", 0f, "none"), "8" => ("(O)292", 0.05f, "none"),
            "9" => ("(O)88", 0.15f, "golden_coconut"), "10" or "11" or "12" => ("MossySeed", 0.05f, "none"), "13" => ("MysticTreeSeed", 0f, "none"),
            _ => (string.Empty, -1f, "unsupported")
        };
        if (expected.Item3 == "unsupported" || data.SeedItemId != expected.Item1 || Math.Abs(data.SeedOnShakeChance - expected.Item2) > 0.000001f) return "drifted";
        var drops = data.SeedDropItems;
        if (expected.Item3 == "none") return drops is null || drops.Count == 0 ? "exact_locked_base_1.6.15" : "drifted";
        if (drops is null || drops.Count != 1) return "drifted";
        var row = drops[0];
        var exact = expected.Item3 == "hazelnut"
            ? row.Id == "Hazelnut" && row.ItemId == "(O)408" && row.Season == Season.Fall && Math.Abs(row.Chance - 1f) < 0.000001f && !row.ContinueOnDrop && row.Condition == "DAY_OF_MONTH 14 15 16 17 18 19 20 21 22 23 24 25 26 27 28"
            : row.Id == "GoldenCoconut" && row.ItemId == "(O)791" && row.Season is null && Math.Abs(row.Chance - 0.1f) < 0.000001f && row.ContinueOnDrop && row.Condition == "LOCATION_CONTEXT Target Island";
        return exact ? "exact_locked_base_1.6.15" : "drifted";
    }

    private static WildTreeOptionalRule[] ProjectRuntimeWildTreeOptionalRules(Tree tree)
    {
        var rows = new List<WildTreeOptionalRule>();
        if (tree.treeType.Value is "6" or "9" && tree.Location?.InIslandContext() == true) rows.Add(WildTreeOptionalRule.Exact("(O)791", WildTreeRuntimeQuality(ItemRegistry.Create("(O)791")), "golden_coconut"));
        if (Game1.MasterPlayer.mailReceived.Contains("sawQiPlane")) rows.Add(WildTreeOptionalRule.Exact(Game1.player.stats.Get(StatKeys.Mastery(2)) != 0 ? "(O)GoldenMysteryBox" : "(O)MysteryBox", 0, "mystery_box"));
        if (Game1.player.stats.Get(StatKeys.Mastery(0)) != 0) rows.Add(WildTreeOptionalRule.Exact("(O)GoldenAnimalCracker", 0, "rare_object"));
        if (Game1.stats.DaysPlayed > 2) { rows.Add(WildTreeOptionalRule.Family("native_cosmetic_item", "rare_object")); rows.Add(WildTreeOptionalRule.Range("(O)SkillBook_", 0, 4, "rare_object")); }
        if (Game1.player.team.SpecialOrderRuleActive("DROP_QI_BEANS")) rows.Add(WildTreeOptionalRule.Exact("(O)890", 0, "qi_bean"));
        return rows.ToArray();
    }

    private static int WildTreeRuntimeQuality(Item item) => Game1.player.professions.Contains(16) && item.HasContextTag("forage_item") ? 4 : 0;

    private static string WildTreeOptionalRulesJson(IEnumerable<WildTreeOptionalRule> rules) => JsonSerializer.Serialize(rules.Select(rule => rule.Kind switch
    {
        "exact" => (object)new { kind = rule.Kind, qualified_item_id = rule.QualifiedItemId, quality = rule.Quality, quantity_max = 1, branch = rule.Branch },
        "family" => new { kind = rule.Kind, family = rule.FamilyName, quality = 0, quantity_max = 1, branch = rule.Branch },
        _ => new { kind = rule.Kind, qualified_item_id_prefix = rule.QualifiedItemId, min_suffix = rule.MinSuffix, max_suffix = rule.MaxSuffix, quality = 0, quantity_max = 1, branch = rule.Branch }
    }));

    private static bool WildTreeJsonEquivalent(string left, string right)
    {
        try { using var a = JsonDocument.Parse(left); using var b = JsonDocument.Parse(right); return JsonSerializer.Serialize(a.RootElement) == JsonSerializer.Serialize(b.RootElement); }
        catch (JsonException) { return false; }
    }

    private static Dictionary<string, int> CaptureWildTreeProductOutputs(GameLocation location)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var item in Game1.player.Items.Where(item => item is not null))
        {
            var key = item!.QualifiedItemId + "|" + item.Quality;
            counts[key] = counts.GetValueOrDefault(key) + Math.Max(1, item.Stack);
        }
        foreach (var debris in location.debris)
        {
            var id = DebrisQualifiedItemId(debris);
            if (string.IsNullOrWhiteSpace(id)) continue;
            var quality = debris.item?.Quality ?? debris.itemQuality;
            var key = id + "|" + quality;
            counts[key] = counts.GetValueOrDefault(key) + (debris.item?.Stack ?? Math.Max(1, debris.Chunks.Count));
        }
        return counts;
    }

    private static string WildTreeProductObservedEffect(GameLocation location, Tree? tree, Point target, IReadOnlyDictionary<string, int>? outputs) =>
        "location=" + location.NameOrUniqueName + ";target=" + target.X + "," + target.Y + ";tree_present=" + (tree is not null && location.terrainFeatures.ContainsKey(target.ToVector2())).ToString().ToLowerInvariant() +
        ";has_seed=" + (tree?.hasSeed.Value.ToString().ToLowerInvariant() ?? "missing") + ";was_shaken_today=" + (tree?.wasShakenToday.Value.ToString().ToLowerInvariant() ?? "missing") +
        ";outputs=" + (outputs is null ? "unobserved" : string.Join(",", outputs.OrderBy(row => row.Key).Select(row => row.Key + "=" + row.Value))) + ";foraging_xp=" + Game1.player.experiencePoints[Farmer.foragingSkill];

    private sealed class ActiveWildTreeProductHarvest
    {
        public ActiveWildTreeProductHarvest(PendingExecution pending, GameLocation location, Tree tree, Point target, Point interaction, Point stand, List<Point> path, RuntimeWildTreeProductProjection projection, int maxMovementTiles)
        {
            Pending = pending; Location = location; Tree = tree; Target = target; Interaction = interaction; Stand = stand; Path = path; Projection = projection; MaxMovementTiles = maxMovementTiles;
            SafeSlotIndex = pending.Request.SafeSlotIndex!.Value; RestoreSlotIndex = pending.Request.RestoreSlotIndex!.Value; ExpectedWasShakenBefore = tree.wasShakenToday.Value;
            LastPosition = Game1.player.Position; LastObservedTile = Game1.player.TilePoint;
            RequestedEffect = "current_location.terrain_features[" + target.X + "," + target.Y + "].has_seed=false;guaranteed_outputs=" + FruitTreeOutputsJson(projection.GuaranteedOutputs) + ";optional_domain=" + projection.OptionalDomainJson;
        }
        public PendingExecution Pending { get; } public GameLocation Location { get; } public Tree Tree { get; } public Point Target { get; } public Point Interaction { get; } public Point Stand { get; }
        public List<Point> Path { get; } public RuntimeWildTreeProductProjection Projection { get; } public int MaxMovementTiles { get; } public int SafeSlotIndex { get; } public int RestoreSlotIndex { get; }
        public bool ExpectedWasShakenBefore { get; } public string RequestedEffect { get; } public string StartedAt { get; } = DateTimeOffset.UtcNow.ToString("O"); public int MaxTicks { get; } = 3600;
        public int ElapsedTicks { get; set; } public int PathIndex { get; set; } public int StuckTicks { get; set; } public int MovementTiles { get; set; } public Vector2 LastPosition { get; set; } public Point LastObservedTile { get; set; }
        public bool ActionIssued { get; set; } public Dictionary<string, int>? OutputCountsBefore { get; set; } public int? ForagingExperienceBefore { get; set; }
    }

    private sealed record RuntimeWildTreeProductProjection(string Status, IReadOnlyList<FruitTreeOutputExpectation> GuaranteedOutputs, IReadOnlyList<WildTreeOptionalRule> OptionalRules, string OptionalDomainJson);

    private sealed record WildTreeOptionalRule(string Kind, string QualifiedItemId, int Quality, string FamilyName, int MinSuffix, int MaxSuffix, string Branch)
    {
        public static WildTreeOptionalRule Exact(string id, int quality, string branch) => new("exact", id, quality, "", 0, 0, branch);
        public static WildTreeOptionalRule Family(string family, string branch) => new("family", "", 0, family, 0, 0, branch);
        public static WildTreeOptionalRule Range(string prefix, int min, int max, string branch) => new("range", prefix, 0, "", min, max, branch);
        public bool Matches(string id, int quality, int quantity) => quantity <= 1 && quality == Quality && (Kind switch
        {
            "exact" => id == QualifiedItemId,
            "family" => FamilyName == "native_cosmetic_item" && IsNativeWildTreeCosmetic(id),
            "range" => id.StartsWith(QualifiedItemId, StringComparison.Ordinal) && int.TryParse(id[QualifiedItemId.Length..], out var suffix) && suffix >= MinSuffix && suffix <= MaxSuffix,
            _ => false
        });
    }

    private static bool IsNativeWildTreeCosmetic(string id)
    {
        if (id.StartsWith("(F)", StringComparison.Ordinal) && int.TryParse(id[3..], out var furniture))
            return furniture is 0 or 3 or 6 or 9 or 12 or 15 or 18 or 21 or 24 or 27 || furniture is >= 1362 and <= 1391 || furniture is 1393 or 1395 or 1397 or 1399 or 1401;
        if (id.StartsWith("(H)", StringComparison.Ordinal) && int.TryParse(id[3..], out var hat))
            return new[] { 45, 46, 47, 49, 52, 53, 54, 55, 57, 58, 59, 62, 63, 68, 69, 70, 84, 85, 87, 88, 89, 90 }.Contains(hat);
        if (id.StartsWith("(S)", StringComparison.Ordinal) && int.TryParse(id[3..], out var shirt))
            return shirt is >= 1112 and <= 1290 && !new[] { 1127, 1129, 1130, 1132, 1133, 1136, 1152, 1176, 1177, 1201, 1202 }.Contains(shirt);
        return false;
    }
}
