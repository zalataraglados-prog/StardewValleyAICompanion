using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using StardewValley;
using StardewValley.Constants;
using StardewValley.Extensions;
using StardewValley.GameData.Objects;
using StardewValley.Internal;
using StardewValley.Objects;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class PlayerReadAdapter
{
    internal const string GeodeProcessingNativeContract =
        "shared_route->Blacksmith_checkAction->answerDialogue(Process)->GeodeMenu_inventory_click->GeodeMenu_geodeSpot_click->2700ms_native_animation->inventory_receipt";

    private static readonly HashSet<string> LockedBaseGeodeIds = new(StringComparer.Ordinal)
    {
        "(O)275", "(O)535", "(O)536", "(O)537", "(O)749", "(O)791",
        "(O)MysteryBox", "(O)GoldenMysteryBox"
    };

    private static object ReadGeodeProcessing(Farmer? player)
    {
        if (player is null || !StardewModdingAPI.Context.IsWorldReady)
            return new { schema_version = "geode_processing.v1", projection_status = "unavailable_world_or_player" };

        var blacksmith = Game1.getLocationFromName("Blacksmith");
        var counterTiles = ReadGeodeCounterTiles(blacksmith);
        var clint = Game1.getCharacterFromName("Clint", mustBeVillager: false);
        var clintAtBlacksmith = clint?.currentLocation?.NameOrUniqueName == "Blacksmith" &&
            blacksmith?.characters.Contains(clint) == true;
        var toolClaimIntercepts = player.toolBeingUpgraded.Value is not null &&
            player.daysLeftForToolUpgrade.Value <= 0;
        var freeSlots = player.freeSpotsInInventory();
        var geodesBefore = Game1.stats.GeodesCracked;
        var mysteryBefore = Game1.stats.Get("MysteryBoxesOpened");
        var goldenCoconutBefore = Game1.netWorldState.Value.GoldenCoconutCracked;

        var inputs = player.Items.Select((item, slot) => new { item, slot })
            .Where(row => row.item is not null && Utility.IsGeode(row.item))
            .Select(row => ReadInventoryGeodeProjection(row.item!, row.slot, player, freeSlots,
                geodesBefore, mysteryBefore, goldenCoconutBefore))
            .ToArray();
        var catalog = Game1.objectData.OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Where(pair => IsGeodeData(pair.Key, pair.Value))
            .Select(pair =>
            {
                var qid = "(O)" + pair.Key;
                return new
                {
                    qualified_item_id = qid,
                    item_id = pair.Key,
                    locked_base_1_6_15 = LockedBaseGeodeIds.Contains(qid),
                    geode_drops_default_items = pair.Value.GeodeDropsDefaultItems,
                    geode_drop_count = pair.Value.GeodeDrops?.Count ?? 0,
                    geode_crusher_ignored = ItemRegistry.Create(qid).HasContextTag("geode_crusher_ignored")
                };
            }).ToArray();

        var baseServiceStatus = blacksmith is null || counterTiles.Length == 0
            ? "blocked_blacksmith_endpoint_missing"
            : !clintAtBlacksmith
                ? "blocked_clint_not_at_blacksmith"
                : toolClaimIntercepts
                    ? "blocked_completed_tool_upgrade_claim_intercepts_counter"
                    : Game1.activeClickableMenu is not null || Game1.dialogueUp || Game1.eventUp
                        ? "blocked_menu_dialogue_or_event_active"
                        : !player.CanMove || player.UsingTool
                            ? "blocked_actor_control_not_ready"
                        : ReferenceEquals(Game1.currentLocation, blacksmith) ? "ready" : "route_to_blacksmith_required";

        var body = new
        {
            location_id = "Blacksmith",
            counterTiles,
            clint = new
            {
                present_at_blacksmith = clintAtBlacksmith,
                current_location = clint?.currentLocation?.NameOrUniqueName ?? string.Empty,
                tile_x = clint?.TilePoint.X,
                tile_y = clint?.TilePoint.Y
            },
            tool_upgrade = new
            {
                tool_qualified_item_id = player.toolBeingUpgraded.Value?.QualifiedItemId ?? string.Empty,
                days_left = player.daysLeftForToolUpgrade.Value,
                completed_claim_intercepts_counter = toolClaimIntercepts
            },
            money_before = player.Money,
            price_gold = 25,
            free_inventory_slots = freeSlots,
            geodes_cracked_before = geodesBefore,
            mystery_boxes_opened_before = mysteryBefore,
            golden_coconut_cracked_before = goldenCoconutBefore,
            golden_walnuts_before = Game1.netWorldState.Value.GoldenWalnuts,
            golden_walnuts_found_before = Game1.netWorldState.Value.GoldenWalnutsFound,
            archaeology_found_count = player.archaeologyFound.Length,
            actor_control_ready = player.CanMove && !player.UsingTool,
            predictor_context = new
            {
                save_id_half = (long)(Game1.uniqueIDForThisGame / 2),
                player_id_half = player.UniqueMultiplayerID / 2,
                season = Game1.season.ToString().ToLowerInvariant(),
                deepest_mine_level = player.deepestMineLevel,
                skill_1_unmodified_level = player.GetUnmodifiedSkillLevel(1),
                farming_mastery_unlocked = player.stats.Get(StatKeys.Mastery(0)) != 0,
                qi_beans_rule_active = player.team.SpecialOrderRuleActive("DROP_QI_BEANS"),
                got_mystery_book_mail = player.mailReceived.Contains("GotMysteryBook"),
                artifact_found_mail = player.hasOrWillReceiveMail("artifactFound")
            },
            base_service_status = baseServiceStatus,
            inputs,
            supported_input_catalog = catalog
        };
        return new
        {
            schema_version = "geode_processing.v1",
            projection_status = catalog.Count(row => row.locked_base_1_6_15) == 8
                ? "complete_locked_base_1.6.15" : "blocked_locked_base_domain_mismatch",
            projection_fingerprint = GeodeProcessingSha256(JsonSerializer.Serialize(body)),
            body.location_id,
            counter_action_tiles = body.counterTiles,
            body.clint,
            body.tool_upgrade,
            body.money_before,
            body.price_gold,
            body.free_inventory_slots,
            body.geodes_cracked_before,
            body.mystery_boxes_opened_before,
            body.golden_coconut_cracked_before,
            body.golden_walnuts_before,
            body.golden_walnuts_found_before,
            body.archaeology_found_count,
            body.actor_control_ready,
            body.predictor_context,
            body.base_service_status,
            inventory_inputs = body.inputs,
            body.supported_input_catalog,
            output_projection_policy = "exact_local_rng_replay_except_complete_shared_rng_crop_family_and_first_golden_coconut_mutex_contingency",
            unit_of_work = "one_native_geode_crack_per_action",
            native_contract = GeodeProcessingNativeContract,
            direct_mutation_policy = "production_executor_must_not_write_money_inventory_stats_mail_or_team_golden_coconut_state"
        };
    }

    private static object ReadInventoryGeodeProjection(Item geode, int slot, Farmer player, int freeSlots,
        uint geodesBefore, uint mysteryBefore, bool goldenCoconutBefore)
    {
        var supported = LockedBaseGeodeIds.Contains(geode.QualifiedItemId);
        var capacity = freeSlots >= 1 || geode.Stack == 1;
        var prediction = supported
            ? PredictClintGeodeOutput(geode, player, geodesBefore + 1,
                mysteryBefore + (geode.QualifiedItemId.Contains("MysteryBox", StringComparison.Ordinal) ? 1u : 0u),
                goldenCoconutBefore)
            : GeodeOutputProjection.Blocked("unsupported_non_locked_base_geode");
        return new
        {
            slot_index = slot,
            qualified_item_id = geode.QualifiedItemId,
            item_id = geode.ItemId,
            display_name = geode.DisplayName,
            stack_before = geode.Stack,
            quality = geode.Quality,
            locked_base_1_6_15 = supported,
            output_capacity_allowed = capacity,
            capacity_rule = "free_slots_before>=1 || selected_stack_before==1",
            prediction.kind,
            prediction.status,
            expected_output = prediction.Primary,
            accepted_outputs = prediction.Accepted,
            expected_mail_additions = prediction.MailAdditions,
            prediction.reason,
            rng_seed_inputs = new
            {
                counter_at_native_generation = geode.QualifiedItemId.Contains("MysteryBox", StringComparison.Ordinal)
                    ? mysteryBefore + 1 : geodesBefore + 1,
                save_id_half = Game1.uniqueIDForThisGame / 2,
                player_id_half = (int)player.UniqueMultiplayerID / 2
            },
            stats_after_success = new
            {
                geodes_cracked = geodesBefore + 1,
                mystery_boxes_opened = mysteryBefore +
                    (geode.QualifiedItemId.Contains("MysteryBox", StringComparison.Ordinal) ? 1u : 0u)
            }
        };
    }

    private static GeodeOutputProjection PredictClintGeodeOutput(Item geode, Farmer player,
        uint geodesAtGeneration, uint mysteryAtGeneration, bool goldenCoconutBefore)
    {
        try
        {
            var ordinary = ApplyClintArtifactGuard(
                geode,
                ReplayGeodeTreasure(geode, player, geodesAtGeneration, mysteryAtGeneration),
                player);
            GeodeOutputProjection result;
            if (geode.QualifiedItemId == "(O)791" && !goldenCoconutBefore)
            {
                var coconut = GeodeProjectedItem.From(ItemRegistry.Create("(O)73"));
                result = new GeodeOutputProjection("first_golden_coconut_mutex_contingent", "available",
                    coconut, new[] { coconut }.Concat(ordinary.Accepted).Distinct().ToArray(),
                    Array.Empty<string>(), "mutex_success_yields_(O)73;lock_failure_uses_seeded_ordinary_result");
            }
            else result = ordinary;
            return ApplyPickupEffects(result, player);
        }
        catch (Exception ex)
        {
            return GeodeOutputProjection.Blocked("pure_replay_failed:" + ex.GetType().Name);
        }
    }

    private static GeodeOutputProjection ReplayGeodeTreasure(Item geode, Farmer player,
        uint geodesAtGeneration, uint mysteryAtGeneration)
    {
        var qid = geode.QualifiedItemId;
        var random = Utility.CreateRandom(qid.Contains("MysteryBox", StringComparison.Ordinal)
            ? mysteryAtGeneration : geodesAtGeneration, Game1.uniqueIDForThisGame / 2,
            (int)player.UniqueMultiplayerID / 2);
        BurnGeodeRandom(random);
        return qid.Contains("MysteryBox", StringComparison.Ordinal)
            ? ReplayMysteryBox(qid, player, mysteryAtGeneration, random)
            : ReplayOrdinaryGeode(geode, player, random);
    }

    private static void BurnGeodeRandom(Random random)
    {
        var count = random.Next(1, 10);
        for (var i = 0; i < count; i++) random.NextDouble();
        count = random.Next(1, 10);
        for (var i = 0; i < count; i++) random.NextDouble();
    }

    private static GeodeOutputProjection ReplayMysteryBox(string qid, Farmer player,
        uint mysteryAtGeneration, Random random)
    {
        if (mysteryAtGeneration > 10 || qid == "(O)GoldenMysteryBox")
        {
            var multiplier = qid == "(O)GoldenMysteryBox" ? 2d : 1d;
            if (qid == "(O)GoldenMysteryBox")
            {
                if (player.stats.Get(StatKeys.Mastery(0)) != 0 && random.NextBool(0.005)) return Exact("(O)GoldenAnimalCracker");
                if (random.NextBool(0.005)) return Exact("(BC)272");
            }
            if (random.NextBool(0.002 * multiplier)) return Exact("(O)279");
            if (random.NextBool(0.004 * multiplier)) return Exact("(O)74");
            if (random.NextBool(0.008 * multiplier)) return Exact("(O)166");
            if (random.NextBool(0.01 * multiplier + (player.mailReceived.Contains("GotMysteryBook") ? 0 : 0.0004 * mysteryAtGeneration)))
            {
                if (!player.mailReceived.Contains("GotMysteryBook"))
                    return Exact("(O)Book_Mystery", mail: new[] { "GotMysteryBook" });
                return Exact(random.Choose("(O)PurpleBook", "(O)Book_Mystery"));
            }
            if (random.NextBool(0.01 * multiplier)) return Exact(random.Choose("(O)797", "(O)373"));
            if (random.NextBool(0.01 * multiplier)) return Exact("(H)MysteryHat");
            if (random.NextBool(0.01 * multiplier)) return Exact("(S)MysteryShirt");
            if (random.NextBool(0.01 * multiplier)) return Exact("(WP)MoreWalls:11");
            if (random.NextBool(0.1) || qid == "(O)GoldenMysteryBox")
            {
                switch (random.Next(15))
                {
                    case 0: return Exact("(O)288", 5);
                    case 1: return Exact("(O)253", 3);
                    case 2:
                        return player.GetUnmodifiedSkillLevel(1) >= 6 && random.NextBool()
                            ? Exact(random.Choose("(O)687", "(O)695")) : Exact("(O)242", 2);
                    case 3: return Exact("(O)204", 2);
                    case 4: return Exact("(O)369", 20);
                    case 5: return Exact("(O)466", 20);
                    case 6: return Exact("(O)773", 2);
                    case 7: return Exact("(O)688", 3);
                    case 8: return Exact("(O)" + random.Next(628, 634));
                    case 9: return CropFamily(20);
                    case 10: return random.NextBool() ? Exact("(W)60") : Exact(random.Choose("(O)533", "(O)534"));
                    case 11: return Exact("(O)621");
                    case 12: return Exact("(O)MysteryBox", random.Next(3, 5));
                    case 13: return Exact("(O)SkillBook_" + random.Next(5));
                    case 14: return GeodeOutputProjection.FromItem(Utility.getRaccoonSeedForCurrentTimeOfYear(player, random, 8));
                }
            }
        }
        return random.Next(14) switch
        {
            0 => Exact("(O)395", 3), 1 => Exact("(O)287", 5), 2 => CropFamily(8),
            3 => Exact("(O)" + random.Next(727, 734)),
            4 => Exact("(O)" + Utility.getRandomIntWithExceptions(random, 194, 240, new List<int> { 217 })),
            5 => Exact("(O)709", 10), 6 => Exact("(O)369", 10), 7 => Exact("(O)466", 10),
            8 => Exact("(O)688"), 9 => Exact("(O)689"), 10 => Exact("(O)770", 10),
            11 => Exact("(O)MixedFlowerSeeds", 10), 12 => random.NextBool(0.4)
                ? Exact(random.Next(4) switch { 0 => "(O)525", 1 => "(O)529", 2 => "(O)888", _ => "(O)" + random.Next(531, 533) })
                : Exact("(O)MysteryBox", 2),
            13 => Exact("(O)690"), _ => Exact("(O)382")
        };
    }

    private static GeodeOutputProjection ReplayOrdinaryGeode(Item geode, Farmer player, Random random)
    {
        if (random.NextBool(0.1) && player.team.SpecialOrderRuleActive("DROP_QI_BEANS"))
            return Exact("(O)890", random.NextBool(0.25) ? 5 : 1);
        if (Game1.objectData.TryGetValue(geode.ItemId, out var data) && data.GeodeDrops is { Count: > 0 } &&
            (!data.GeodeDropsDefaultItems || random.NextBool()))
        {
            var errors = new List<string>();
            foreach (var drop in data.GeodeDrops.OrderBy(row => row.Precedence))
            {
                if (!random.NextBool(drop.Chance) || drop.Condition is not null &&
                    !GameStateQuery.CheckConditions(drop.Condition, null, null, null, null, random)) continue;
                var item = ItemQueryResolver.TryResolveRandomItem(drop,
                    new ItemQueryContext(null, null, random, $"object '{geode.ItemId}' > geode transparent replay '{drop.Id}'"),
                    avoidRepeat: false, null, null, null, (query, error) => errors.Add(query + ":" + error));
                if (item is null) continue;
                if (drop.SetFlagOnPickup is not null) item.SetFlagOnPickup = drop.SetFlagOnPickup;
                return GeodeOutputProjection.FromItem(item, errors.Count == 0 ? string.Empty : string.Join("|", errors));
            }
        }
        var count = random.Next(3) * 2 + 1;
        if (random.NextBool(0.1)) count = 10;
        if (random.NextBool(0.01)) count = 20;
        if (random.NextBool())
        {
            return random.Next(4) switch
            {
                0 or 1 => Exact("(O)390", count), 2 => Exact("(O)330"),
                _ => Exact(geode.QualifiedItemId switch
                {
                    "(O)749" => "(O)" + (82 + random.Next(3) * 2), "(O)535" => "(O)86",
                    "(O)536" => "(O)84", _ => "(O)82"
                })
            };
        }
        if (geode.QualifiedItemId == "(O)535")
            return random.Next(3) switch { 0 => Exact("(O)378", count), 1 => Exact(player.deepestMineLevel > 25 ? "(O)380" : "(O)378", count), _ => Exact("(O)382", count) };
        if (geode.QualifiedItemId == "(O)536")
            return random.Next(4) switch { 0 => Exact("(O)378", count), 1 => Exact("(O)380", count), 2 => Exact("(O)382", count), _ => Exact(player.deepestMineLevel > 75 ? "(O)384" : "(O)380", count) };
        return random.Next(5) switch { 0 => Exact("(O)378", count), 1 => Exact("(O)380", count), 2 => Exact("(O)382", count), 3 => Exact("(O)384", count), _ => Exact("(O)386", count / 2 + 1) };
    }

    private static GeodeOutputProjection ApplyClintArtifactGuard(Item geode, GeodeOutputProjection projection, Farmer player)
    {
        if (geode.QualifiedItemId == "(O)275" || projection.Primary is null || projection.Accepted.Length != 1)
            return projection;
        var item = ItemRegistry.Create(projection.Primary.QualifiedItemId, projection.Primary.Stack, projection.Primary.Quality);
        return item is StardewValley.Object { Type: "Arch" } && item is not StardewValley.Object { Type: "Minerals" } &&
            !player.hasOrWillReceiveMail("artifactFound") ? Exact("(O)390", 5) : projection;
    }

    private static GeodeOutputProjection ApplyPickupEffects(GeodeOutputProjection projection, Farmer player)
    {
        GeodeProjectedItem Enrich(GeodeProjectedItem row)
        {
            var item = ItemRegistry.Create(row.QualifiedItemId, row.Stack, row.Quality);
            var mail = row.ExpectedMailAdditions.ToHashSet(StringComparer.Ordinal);
            if (!string.IsNullOrWhiteSpace(row.SetFlagOnPickup) && !player.hasOrWillReceiveMail(row.SetFlagOnPickup))
                mail.Add(row.SetFlagOnPickup);
            var effect = "inventory_item";
            var persists = row.QualifiedItemId != "(O)73";
            if (row.QualifiedItemId == "(O)73") effect = "team_golden_walnut";
            else if (item is StardewValley.Object { Category: -2 } || item is StardewValley.Object { Type: "Minerals" })
            {
                effect = "mineral_discovery";
                if (!player.hasOrWillReceiveMail("artifactFound")) mail.Add("artifactFound");
            }
            else if (item is StardewValley.Object { Type: "Arch" })
            {
                effect = "artifact_discovery";
                if (player.archaeologyFound.Length == 0) mail.Add("artifactFound");
            }
            else effect = row.QualifiedItemId switch
            {
                "(O)390" => "stone_gathered_counter",
                "(O)378" => "copper_found_counter",
                "(O)380" => "iron_found_counter",
                "(O)384" => "gold_found_counter",
                "(O)386" => "iridium_found_counter",
                _ => effect
            };
            return row with { InventoryPersists = persists, PickupEffectKind = effect,
                ExpectedMailAdditions = mail.OrderBy(value => value, StringComparer.Ordinal).ToArray() };
        }
        var primary = projection.Primary is null ? null : Enrich(projection.Primary);
        var accepted = projection.Accepted.Select(Enrich).ToArray();
        return projection with { Primary = primary, Accepted = accepted,
            MailAdditions = primary?.ExpectedMailAdditions ?? Array.Empty<string>() };
    }

    private static GeodeOutputProjection Exact(string qid, int stack = 1, int quality = 0, string[]? mail = null) =>
        GeodeOutputProjection.FromItem(ItemRegistry.Create(qid, stack, quality), mailAdditions: mail);

    private static GeodeOutputProjection CropFamily(int stack)
    {
        var ids = Game1.season switch
        {
            Season.Spring => new[] { "472", "473", "474", "475" },
            Season.Summer => new[] { "487", "483", "482", "484" },
            Season.Fall => new[] { "487", "488", "489", "490" },
            _ => new[] { "472", "473", "474", "475", "487", "483", "482", "484", "488", "489", "490" }
        };
        var accepted = ids.Distinct(StringComparer.Ordinal).Select(id => GeodeProjectedItem.From(ItemRegistry.Create("(O)" + id, stack))).ToArray();
        return new GeodeOutputProjection("complete_shared_rng_crop_family", "available", null, accepted,
            Array.Empty<string>(), "Crop.getRandomLowGradeCropForThisSeason consumes shared_Game1_random;complete_current-season_family_is_published_without_consuming_it");
    }

    private static bool IsGeodeData(string itemId, ObjectData data) =>
        itemId.Contains("MysteryBox", StringComparison.Ordinal) || data.GeodeDropsDefaultItems || data.GeodeDrops is { Count: > 0 };

    private static object[] ReadGeodeCounterTiles(GameLocation? location)
    {
        var layer = location?.map?.GetLayer("Buildings");
        if (location is null || layer is null) return Array.Empty<object>();
        var rows = new List<object>();
        for (var y = 0; y < layer.LayerHeight; y++)
        for (var x = 0; x < layer.LayerWidth; x++)
        {
            var raw = location.doesTileHaveProperty(x, y, "Action", "Buildings");
            if (raw?.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() == "Blacksmith")
                rows.Add(new { tile_x = x, tile_y = y, action_raw = raw, action_token = "Blacksmith" });
        }
        return rows.ToArray();
    }

    private static string GeodeProcessingSha256(string value) => Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private sealed record GeodeProjectedItem(
        [property: JsonPropertyName("qualified_item_id")] string QualifiedItemId,
        [property: JsonPropertyName("stack")] int Stack,
        [property: JsonPropertyName("quality")] int Quality,
        [property: JsonPropertyName("sale_price")] int SalePrice,
        [property: JsonPropertyName("set_flag_on_pickup")] string SetFlagOnPickup,
        [property: JsonPropertyName("inventory_persists")] bool InventoryPersists,
        [property: JsonPropertyName("pickup_effect_kind")] string PickupEffectKind,
        [property: JsonPropertyName("expected_mail_additions")] string[] ExpectedMailAdditions)
    {
        public static GeodeProjectedItem From(Item item, string[]? mailAdditions = null) => new(
            item.QualifiedItemId, item.Stack, item.Quality, item.salePrice(), item.SetFlagOnPickup ?? string.Empty,
            item.QualifiedItemId != "(O)73", "inventory_item", mailAdditions ?? Array.Empty<string>());
    }

    private sealed record GeodeOutputProjection(string kind, string status, GeodeProjectedItem? Primary,
        GeodeProjectedItem[] Accepted, string[] MailAdditions, string reason)
    {
        public static GeodeOutputProjection FromItem(Item item, string reason = "", string[]? mailAdditions = null)
        {
            var projected = GeodeProjectedItem.From(item, mailAdditions);
            return new("exact", "available", projected, new[] { projected }, mailAdditions ?? Array.Empty<string>(), reason);
        }

        public static GeodeOutputProjection Blocked(string reason) =>
            new("blocked", "blocked", null, Array.Empty<GeodeProjectedItem>(), Array.Empty<string>(), reason);
    }
}
