using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Plans;
using StardewAI.Contracts.State;
using StardewAI.Core.Execution;

namespace StardewAI.Core.Tests;

public sealed class ActionQueueCompilerShopSafetyWaitTests
{
    [Fact]
    public void ExactBoundedWaitAllowsSameDeferredPurchase()
    {
        var snapshot = ShopSafetyTimerSnapshot();
        var continuation = Continuation("IronOre_YearOne");
        var plan = Plan(
            snapshot.StateHash,
            WaitStep(continuation),
            BuyStep(continuation, "IronOre_YearOne"),
            new SmallModelPlanStep
            {
                StepId = "shop.close",
                Kind = "close_menu",
                ExpectedEffects = new[] { "menus.active_menu.is_open=false" }
            });

        var queue = new ActionQueueCompiler().Compile(plan, snapshot);

        Assert.Equal("pending", queue.Status);
        Assert.Equal(
            new[]
            {
                "executor.wait_ticks",
                "executor.buy_shop_item",
                "executor.close_menu"
            },
            queue.Items.Select(item => item.OptionId));
        Assert.All(queue.Items, item => Assert.Empty(item.BlockingReasons));
        Assert.Contains(
            queue.Items[1].NormalizedCommand.Parameters,
            parameter => parameter.Name ==
                    "compiler_context.preceded_by_bounded_shop_menu_readiness_wait" &&
                parameter.Value == "true");
        Assert.Contains(
            queue.Items[1].NormalizedCommand.Parameters,
            parameter => parameter.Name ==
                    "compiler_context.runtime_shop_stock_recheck_required" &&
                parameter.Value == "true");
    }

    [Fact]
    public void PurchaseIdentityDriftDoesNotInheritWaitAuthorization()
    {
        var snapshot = ShopSafetyTimerSnapshot();
        var plan = Plan(
            snapshot.StateHash,
            WaitStep(Continuation("IronOre_YearOne")),
            BuyStep(Continuation("IronOre_YearTwo"), "IronOre_YearTwo"));

        var queue = new ActionQueueCompiler().Compile(plan, snapshot);

        Assert.Equal("blocked", queue.Status);
        Assert.DoesNotContain(
            queue.Items[1].NormalizedCommand.Parameters,
            parameter => parameter.Name ==
                "compiler_context.preceded_by_bounded_shop_menu_readiness_wait");
        Assert.Contains(
            "exact_purchase_candidate_not_found",
            queue.Items[1].BlockingReasons);
    }

    [Fact]
    public void MissingWaitSafetyConstraintKeepsActiveMenuGateClosed()
    {
        var snapshot = ShopSafetyTimerSnapshot();
        var continuation = Continuation("IronOre_YearOne");
        var wait = WaitStep(continuation);
        wait.SafetyConstraints = new[]
        {
            "purchase_continuation_identity_preserved"
        };

        var queue = new ActionQueueCompiler().Compile(
            Plan(snapshot.StateHash, wait),
            snapshot);

        Assert.Equal("blocked", queue.Status);
        Assert.Contains(
            "active_menu_must_be_closed_before_action",
            queue.Items[0].BlockingReasons);
    }

    private static SmallModelPlanStep WaitStep(
        SmallModelActionParameter[] continuation) => new()
        {
            StepId = "shop.wait",
            Kind = "wait_ticks",
            WaitTicks = 18,
            Preconditions = new[]
            {
                "shop_menu_open=true",
                "shop_menu_safety_timer_active=true"
            },
            ExpectedEffects = new[] { "fresh_snapshot_replan_required=true" },
            SafetyConstraints = new[]
            {
                "bounded_native_shop_menu_readiness_wait",
                "purchase_continuation_identity_preserved"
            },
            Parameters = continuation
        };

    private static SmallModelPlanStep BuyStep(
        SmallModelActionParameter[] continuation,
        string stockId) => new()
        {
            StepId = "shop.buy",
            Kind = "buy_shop_item",
            Parameters = new[]
            {
                Parameter("qualified_item_id", "(O)380"),
                Parameter("shop_item_id", "380"),
                Parameter("quantity", "1"),
                Parameter("max_unit_price", "150"),
                Parameter("expected_unit_price", "150"),
                Parameter("expected_shop_id", "Blacksmith"),
                Parameter("expected_stock_id", stockId),
                Parameter("expected_output_stack", "1"),
                Parameter("expected_output_quality", "0")
            }.Concat(continuation).ToArray()
        };

    private static SmallModelActionParameter[] Continuation(string stockId) =>
        new[]
        {
            Parameter("continuation.option_id", "economy.buy_supplies"),
            Parameter("continuation.shop_id", "Blacksmith"),
            Parameter("continuation.qualified_item_id", "(O)380"),
            Parameter("continuation.stock_id", stockId),
            Parameter("continuation.output_stack_per_purchase", "1"),
            Parameter("continuation.output_quality", "0"),
            Parameter("continuation.max_unit_price", "150"),
            Parameter("continuation.quantity", "1")
        };

    private static SmallModelActionParameter Parameter(
        string name,
        string value) => new() { Name = name, Value = value };

    private static SmallModelPlanEnvelope Plan(
        string stateHash,
        params SmallModelPlanStep[] steps) => new()
        {
            PlanId = "plan.shop-safety",
            SourceModel = "test",
            StateHash = stateHash,
            GoalId = "grandpa.maximum_21",
            ExecutionMode = "training_singleplayer",
            Actor = new ActionActorRef
            {
                ActorId = "training_farmer.main",
                ActorType = "training_farmer",
                ControlSurface = "training_sandbox"
            },
            Steps = steps
        };

    private static SnapshotEnvelope ShopSafetyTimerSnapshot()
    {
        const string json = """
        {
          "time":{"time":{"value":900,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}},
          "player":{
            "location_id":{"value":"Blacksmith","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "money":{"value":500,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "inventory":{"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "menus":{
            "active_menu":{"value":{"is_open":true,"type":"ShopMenu"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "sleep_prompt_context":{"value":{"prompt_open":false},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "menu_specific_state":{"value":{"kind":"shop","shop_id":"Blacksmith","read_only":false,"safety_timer":250,"held_item_present":false},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "shop_stock":{"value":{
              "shop_id":"Blacksmith","read_only":false,"safety_timer":250,
              "held_item_present":false,"currency":0,
              "shop_on_purchase_present":false,"executor_purchase_enabled":false,
              "entries":[{
                "item_id":"380","qualified_item_id":"(O)380",
                "synced_key":"IronOre_YearOne","stack":1,"quality":0,
                "price":150,"stock":2147483647,"infinite_stock":true,
                "can_buy_item":true,"can_afford_one_with_currency":true,
                "can_afford_one_with_trade_item":true,
                "could_inventory_accept":true,"executor_purchase_enabled":false,
                "executor_block_reasons":["shop_menu_safety_timer_active"]
              }]
            },"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """;
        using var document = JsonDocument.Parse(json);
        var state = document.RootElement.EnumerateObject()
            .ToDictionary(
                property => property.Name,
                property => property.Value.Clone(),
                StringComparer.Ordinal);
        var snapshot = new SnapshotEnvelope
        {
            SchemaVersion = "snapshot.v1",
            GameVersion = "1.6.15",
            StateHash = SnapshotHash.ComputeStateHash(state),
            GameTick = 1,
            RealTimestamp = "2026-10-12T00:00:00Z",
            Completeness = "complete",
            State = state
        };
        return snapshot;
    }
}
