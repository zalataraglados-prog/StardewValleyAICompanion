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
    public void EndToEndShipCandidateThroughCompilerToQueueItem()
    {
        var inventory = Inventory(
            InventoryItem("0", "24", "(O)24", "Parsnip", 5, 17, 17, true, -75));
        var fsProgress = FullShipmentProgressField("available", 1,
            FsItem("24", "(O)24", "Parsnip", -75, "Basic", 0, false));
        var snapshot = BuildSnapshot(
            inventory,
            fsProgress,
            playerTileX: 67,
            playerTileY: 15);

        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "economy.ship_items" }, includeExecutorCalibrationOptions: true);

        var shipOption = availability.Options.Single(o => o.OptionId == "economy.ship_items");
        Assert.NotEmpty(shipOption.EventCandidates);

        var report = new BaselineTrainingReport
        {
            OptionScores = new[]
            {
                new BaselineOptionScore { OptionId = "economy.ship_items", AverageTotalReward = 0.1 }
            }
        };

        var ranked = new EventCandidateRanker().Rank(report, availability);

        var shipCandidates = ranked.Where(c => c.Kind == "ship_inventory_item_to_bin").ToArray();
        Assert.NotEmpty(shipCandidates);

        var first = shipCandidates[0];

        Assert.Equal("Farm", first.LocationId);
        Assert.NotNull(first.TileX);
        Assert.NotNull(first.TileY);

        Assert.Equal(1, first.Quantity);
        Assert.Equal("24", first.ItemId);
        Assert.Equal("(O)24", first.QualifiedItemId);
        Assert.Equal(0, first.SlotIndex);
        Assert.True(first.FullShipmentKnown);
        Assert.True(first.FullShipmentEligible);
        Assert.Equal(0, first.FullShipmentCurrentShippedCount);
        Assert.False(first.FullShipmentAlreadyShipped);
        Assert.True(first.FullShipmentContributes);

        var planCompiler = new DailyPlanCompiler();
        var plan = planCompiler.Compile(
            new[] { first },
            snapshot.StateHash,
            "test_e2e",
            "training_singleplayer",
            maxCandidates: 1);

        Assert.NotEmpty(plan.Steps);
        var shipStep = plan.Steps.FirstOrDefault(s => s.Kind == "ship_inventory_item_to_bin");
        Assert.NotNull(shipStep);
        Assert.Equal("Farm", shipStep.TargetLocation);

        var queueCompiler = new ActionQueueCompiler();
        var queue = queueCompiler.Compile(plan, snapshot);

        var diagMessages = string.Join("; ", queue.CompilerDiagnostics);
        var blockingReasons = queue.Items.SelectMany(i => i.BlockingReasons).Distinct().ToArray();
        var missingState = queue.Items.SelectMany(i => i.MissingStateFactors).Distinct().ToArray();
        var reasonsStr = "Diagnostics: " + diagMessages + " | Blocking: " + string.Join("; ", blockingReasons) + " | MissingState: " + string.Join("; ", missingState);

        Assert.True(queue.Status == "pending", reasonsStr);

        Assert.Equal("pending", queue.Status);
        var queueItem = queue.Items.FirstOrDefault(i => i.OptionId == "executor.ship_inventory_item_to_bin");
        Assert.NotNull(queueItem);

        Assert.Equal("pending", queueItem.Status);
        Assert.Equal(OptionBehaviorCategories.Mechanical, queueItem.BehaviorCategory);
        Assert.Equal(CompilerResponsibilities.FullActionExpansion, queueItem.CompilerResponsibility);
        Assert.Equal(TrainingRoles.ExecutorCalibration, queueItem.TrainingRole);

        Assert.Contains(queueItem.NormalizedCommand.Parameters,
            p => p.Name == "slot_index" && p.Value == "0");
        Assert.Contains(queueItem.NormalizedCommand.Parameters,
            p => p.Name == "qualified_item_id" && p.Value == "(O)24");
        Assert.Contains(queueItem.NormalizedCommand.Parameters,
            p => p.Name == "quantity" && p.Value == "1");
        Assert.Contains(queueItem.NormalizedCommand.Parameters,
            p => p.Name == "expected_unit_price" && p.Value == "17");
        Assert.Contains(queueItem.NormalizedCommand.Parameters,
            p => p.Name == "stand_tile_x");
        Assert.Contains(queueItem.NormalizedCommand.Parameters,
            p => p.Name == "stand_tile_y");
    }

    [Fact]
    public void ShippingApproachCompilesOnlyMovementAndCarriesExactContinuation()
    {
        var snapshot = BuildSnapshot(
            Inventory(
                InventoryItem("0", "24", "(O)24", "Parsnip", 5, 19, 17, true, -75)),
            FullShipmentProgressField(
                "available",
                1,
                FsItem("24", "(O)24", "Parsnip", -75, "Basic", 0, false)));
        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "economy.ship_items" }, includeExecutorCalibrationOptions: true);
        var candidate = Assert.Single(
            availability.Options.Single(option => option.OptionId == "economy.ship_items")
                .EventCandidates);

        Assert.Contains(candidate.Parameters,
            parameter => parameter.Name == "shipping_stage" && parameter.Value == "approach");
        Assert.Contains(candidate.Parameters,
            parameter => parameter.Name == "continuation.expected_unit_price" && parameter.Value == "19");
        Assert.Equal(19, candidate.UnitPrice);
        Assert.Equal(19, candidate.TotalValue);

        var ranked = new EventCandidateRanker().Rank(
            new BaselineTrainingReport
            {
                OptionScores = new[]
                {
                    new BaselineOptionScore
                    {
                        OptionId = "economy.ship_items",
                        AverageTotalReward = 0.1
                    }
                }
            },
            availability);
        var plan = new DailyPlanCompiler().Compile(
            ranked,
            snapshot.StateHash,
            maxCandidates: 1);

        var move = Assert.Single(plan.Steps);
        Assert.Equal("move_to_tile", move.Kind);
        Assert.Contains(move.Parameters,
            parameter => parameter.Name == "continuation.option_id" &&
                parameter.Value == "economy.ship_items");
    }

    [Fact]
    public void ShippingRemoteStageCompilesOneTransparentConnectorBeforeFarmApproach()
    {
        var snapshot = BuildSnapshot(
            Inventory(
                InventoryItem("0", "24", "(O)24", "Parsnip", 5, 19, 17, true, -75)),
            FullShipmentProgressField(
                "available",
                1,
                FsItem("24", "(O)24", "Parsnip", -75, "Basic", 0, false)),
            playerTileX: 27,
            playerTileY: 30,
            playerLocation: "FarmHouse",
            includeFarmRoute: true);
        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "economy.ship_items" }, includeExecutorCalibrationOptions: true);
        var candidate = Assert.Single(
            availability.Options.Single(option => option.OptionId == "economy.ship_items")
                .EventCandidates);

        Assert.Equal("route_connector_tile", candidate.Kind);
        Assert.Equal("FarmHouse", candidate.LocationId);
        Assert.Equal(27, candidate.TileX);
        Assert.Equal(31, candidate.TileY);
        Assert.Contains(candidate.Parameters,
            parameter => parameter.Name == "continuation.qualified_item_id" &&
                parameter.Value == "(O)24");
        Assert.Contains(candidate.Parameters,
            parameter => parameter.Name == "shipping_route.remaining_connector_count" &&
                parameter.Value == "1");

        var ranked = new EventCandidateRanker().Rank(
            new BaselineTrainingReport
            {
                OptionScores = new[]
                {
                    new BaselineOptionScore
                    {
                        OptionId = "economy.ship_items",
                        AverageTotalReward = 0.1
                    }
                }
            },
            availability);
        var plan = new DailyPlanCompiler().Compile(
            ranked,
            snapshot.StateHash,
            maxCandidates: 1);
        var connector = Assert.Single(plan.Steps);
        Assert.Equal("traverse_connector", connector.Kind);
    }

    // === Helper methods ===

    private static FullShipmentProgressRef CreateProgress(
        (string qualifiedItemId, string itemId, string displayName, int category, string objectType, int shippedCount)[] eligible,
        string[] shippedItemIds)
    {
        var items = eligible
            .Select(e => new FullShipmentItemProgressRef
            {
                QualifiedItemId = e.qualifiedItemId,
                ItemId = e.itemId,
                DisplayName = e.displayName,
                Category = e.category,
                ObjectType = e.objectType,
                CurrentShippedCount = e.shippedCount,
                Shipped = e.shippedCount > 0
            })
            .OrderBy(i => i.ItemId, StringComparer.Ordinal)
            .ThenBy(i => i.QualifiedItemId, StringComparer.Ordinal)
            .ToArray();

        var shippedCount = items.Count(i => i.Shipped);
        var totalCount = items.Length;
        var missing = items.Where(i => !i.Shipped).Select(i => i.ItemId).OrderBy(id => id, StringComparer.Ordinal).ToArray();

        return new FullShipmentProgressRef
        {
            EligibleItemCount = totalCount,
            ShippedEligibleItemCount = shippedCount,
            MissingItemCount = totalCount - shippedCount,
            CompletionRatio = totalCount > 0 ? (double)shippedCount / totalCount : 0,
            Complete = shippedCount == totalCount && totalCount > 0,
            Items = items,
            MissingItemIds = missing
        };
    }

    private static SnapshotEnvelope GrandpaSnapshot()
    {
        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>("""
        {
          "time": {
            "year": {"value":1,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "season": {"value":"spring","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "day": {"value":1,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "time": {"value":600,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "weather": {"value":"sunny","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "player": {
            "location_id": {"value":"Farm","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "money": {"value":500,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "total_money_earned": {"value":10000,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "energy": {"value":270,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "level": {"value":1,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "has_skull_key": {"value":false,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "has_rusty_key": {"value":false,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "married_or_roommate": {"value":false,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "farmhouse_upgrade_level": {"value":0,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "active_object_qualified_id": {"value":"","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "farm": {
            "shipping_bins": {"value":[{"days_of_construction_left":0,"player_within_shipping_range":true}],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "grandpa_score": {"value":0,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "world_progress": {
            "achievements": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "community_center": {"value":{"location_accessible":false,"completed":false},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "joja_membership": {"value":false,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "npcs": {
            "friendships": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "quests": {
            "mail_received": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "menus": {
            "active_menu": {"value":null,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """, JsonOptions)!;
        return new SnapshotEnvelope
        {
            SchemaVersion = "snapshot.v1",
            StateHash = SnapshotHash.ComputeStateHash(state),
            GameTick = 1,
            RealTimestamp = "2026-07-14T00:00:00Z",
            Completeness = "complete",
            State = state
        };
    }

    [Fact]
    public void ExecutorShipInventoryItemToBinIsRegistered()
    {
        var option = new StardewAI.Core.OptionRegistry.OptionRegistry().GetRequired("executor.ship_inventory_item_to_bin");
        Assert.NotNull(option);
        Assert.Equal("economy", option.Domain);
        Assert.Contains("inventory item deposited into shipping bin", option.EstimatedEffects);
        Assert.Contains("never_ship_protected_items", option.SafetyConstraints);
    }

    [Fact]
    public void ShipCandidateKindIsCorrect()
    {
        var snapshot = BuildSnapshot(
            Inventory(
                InventoryItem("0", "388", "(O)388", "Wood", 5, 2, 2, true, -16)),
            FullShipmentProgressField("available", 1, FsItem("388", "(O)388", "Wood", -16, "Basic", 0, false)));
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, new[] { "economy.ship_items" }, includeExecutorCalibrationOptions: true);
        var shipCandidate = FindShipCandidate(availability, "388");
        Assert.NotNull(shipCandidate);
        Assert.Equal("ship_inventory_item_to_bin", shipCandidate.Kind);
        Assert.Contains("executor_kind=ship_inventory_item_to_bin", shipCandidate.ExpectedEffect);
        Assert.Contains("slot_index=0", shipCandidate.ExpectedEffect);
    }

    [Fact]
    public void ShipCandidateHasSlotIndexParameter()
    {
        var snapshot = BuildSnapshot(
            Inventory(
                InventoryItem("2", "388", "(O)388", "Wood", 3, 2, 2, true, -16)),
            FullShipmentProgressField("available", 1, FsItem("388", "(O)388", "Wood", -16, "Basic", 0, false)));
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, new[] { "economy.ship_items" }, includeExecutorCalibrationOptions: true);
        var shipCandidate = FindShipCandidate(availability, "388");
        Assert.NotNull(shipCandidate);
        var slotParam = shipCandidate.Parameters.FirstOrDefault(p => p.Name == "slot_index");
        Assert.NotNull(slotParam);
        Assert.Equal("2", slotParam.Value);
    }

    [Fact]
    public void TrainingExecutionRequestSlotIndexSerializes()
    {
        var req = new TrainingExecutionRequest
        {
            OptionId = "executor.ship_inventory_item_to_bin",
            SlotIndex = 3,
            QualifiedItemId = "(O)388",
            Quantity = 1
        };
        var json = JsonSerializer.Serialize(req, JsonOptions);
        Assert.Contains("\"slot_index\":3", json);
        Assert.Contains("\"option_id\":\"executor.ship_inventory_item_to_bin\"", json);
    }

    [Fact]
    public void TrainingExecutionResultShipFieldsSerialize()
    {
        var result = new TrainingExecutionResult
        {
            OptionId = "executor.ship_inventory_item_to_bin",
            PrimitiveKind = "ship_inventory_item_to_bin",
            ShipSlotIndex = 3,
            ShipQualifiedItemId = "(O)388",
            ShipInventoryCountBefore = 5,
            ShipInventoryCountAfter = 4,
            ShipBinCountBefore = 0,
            ShipBinCountAfter = 1,
            ShipBinTotalCountBefore = 10,
            ShipBinTotalCountAfter = 11,
            ShipBinDistinctCountBefore = 3,
            ShipBinDistinctCountAfter = 4,
            ShipBinSignatureBefore = "before_sig",
            ShipBinSignatureAfter = "after_sig",
            ShipBasicShippedCountBefore = 0,
            ShipSourceDate = "12345"
        };
        var json = JsonSerializer.Serialize(result, JsonOptions);
        Assert.Contains("\"ship_slot_index\":3", json);
        Assert.Contains("\"ship_qualified_item_id\":\"(O)388\"", json);
        Assert.Contains("\"ship_inventory_count_before\":5", json);
        Assert.Contains("\"ship_inventory_count_after\":4", json);
        Assert.Contains("\"ship_bin_count_before\":0", json);
        Assert.Contains("\"ship_bin_count_after\":1", json);
        Assert.Contains("\"primitive_kind\":\"ship_inventory_item_to_bin\"", json);
        Assert.Contains("\"ship_source_date\":\"12345\"", json);
    }

    [Fact]
    public void ShipCandidateShowsRuntimeVerified()
    {
        var snapshot = BuildSnapshot(
            Inventory(
                InventoryItem("0", "388", "(O)388", "Wood", 5, 2, 2, true, -16)),
            FullShipmentProgressField("available", 1, FsItem("388", "(O)388", "Wood", -16, "Basic", 0, false)));
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, new[] { "economy.ship_items" }, includeExecutorCalibrationOptions: true);
        var shipCandidate = FindShipCandidate(availability, "388");
        Assert.NotNull(shipCandidate);
        Assert.Contains("shipping_executor_status=runtime_verified", shipCandidate.ExpectedEffect);
        var statusParam = shipCandidate.Parameters.FirstOrDefault(p => p.Name == "shipping_executor_available");
        Assert.NotNull(statusParam);
        Assert.Equal("runtime_verified", statusParam.Value);
    }}
