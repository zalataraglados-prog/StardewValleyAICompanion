using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed partial class MasterAnglerMainlineTests
{
    [Fact]
    public void GingerIslandBoatRepairCompilesOnlyFromExactNativeProjection()
    {
        var fixture = CreateFixture("IslandSouthEastCave:5", targetLocation: "IslandSouthEastCave");
        try
        {
            var snapshot = GingerIslandSnapshot(
                fixture.Before,
                "BoatTunnel",
                3,
                9,
                GingerIslandAccess("ready_to_donate_batteries"));
            var candidate = EvaluateGingerIslandCandidate(snapshot, fixture.Parameters);
            Assert.True(candidate.Kind == "interact_endpoint",
                GingerIslandCandidateDiagnostic(candidate));
            Assert.Contains(candidate.Parameters, row =>
                row.Name == "expected_action_type" && row.Value == "WillyBoatDonateBatteries");
            Assert.Contains(candidate.Parameters, row =>
                row.Name == "required_executor_profile" && row.Value == "ginger_island_access_executor");

            var item = CompileGingerIslandInteraction(snapshot, fixture.Parameters);
            Assert.Empty(item.BlockingReasons);

            var drifted = RewriteGingerIslandAccess(snapshot, access =>
                access["repairs"]!["battery"]!["available_count"] = 4);
            var blocked = CompileGingerIslandInteraction(snapshot, fixture.Parameters, drifted);
            Assert.Contains("ginger_island_boat_repair_projection_drifted", blocked.BlockingReasons);
        }
        finally
        {
            fixture.Dispose();
        }
    }

    [Fact]
    public void GingerIslandResortRequiresExactFiftyOneWalnutDependencyChain()
    {
        var fixture = CreateFixture("IslandSouthEastCave:5", targetLocation: "IslandSouthEastCave");
        try
        {
            var access = GingerIslandAccess("ready_to_restore_resort");
            var snapshot = GingerIslandSnapshot(fixture.Before, "IslandSouth", 16, 22, access);
            var candidate = EvaluateGingerIslandCandidate(snapshot, fixture.Parameters);
            Assert.True(candidate.Kind == "interact_endpoint",
                GingerIslandCandidateDiagnostic(candidate));
            Assert.Contains(candidate.Parameters, row =>
                row.Name == "expected_action_type" && row.Value == "IslandResort");
            Assert.Equal(1,
                access["first_parrot"]!["expected_required_nuts"]!.GetValue<int>());
            Assert.Equal(10,
                access["western_turtle_parrot"]!["expected_required_nuts"]!.GetValue<int>());
            Assert.Equal(20,
                access["island_house_parrot"]!["expected_required_nuts"]!.GetValue<int>());
            Assert.Equal(20,
                access["resort_parrot"]!["expected_required_nuts"]!.GetValue<int>());
            Assert.Equal("Island_UpgradeHouse",
                access["resort_parrot"]!["expected_required_mail"]!.GetValue<string>());

            var item = CompileGingerIslandInteraction(snapshot, fixture.Parameters);
            Assert.Empty(item.BlockingReasons);

            var drifted = RewriteGingerIslandAccess(snapshot, row =>
                row["resort_parrot"]!["required_mail"] = string.Empty);
            var blocked = CompileGingerIslandInteraction(snapshot, fixture.Parameters, drifted);
            Assert.Contains("ginger_island_parrot_projection_drifted", blocked.BlockingReasons);
        }
        finally
        {
            fixture.Dispose();
        }
    }

    [Fact]
    public void RuntimeGingerIslandInteractionsUseNativeActionsWithoutDirectProgressWrites()
    {
        var source = RuntimeHarnessSources.LoadFile("ModEntry.GingerIsland.cs");
        Assert.Contains("Game1.currentLocation.checkAction", source, StringComparison.Ordinal);
        Assert.Contains("active.Location.answerDialogue(yes)", source, StringComparison.Ordinal);
        Assert.Contains("parrotUpgradePerches", source, StringComparison.Ordinal);
        Assert.DoesNotContain("GoldenWalnuts -=", source, StringComparison.Ordinal);
        Assert.DoesNotContain("mailReceived.Add", source, StringComparison.Ordinal);
        Assert.DoesNotContain("mailForTomorrow.Add", source, StringComparison.Ordinal);
        Assert.DoesNotMatch(@"farmhouseRestored\.Value\s*=(?!=)", source);
        Assert.DoesNotMatch(@"resortRestored\.Value\s*=(?!=)", source);
    }

    private static EventCandidate EvaluateGingerIslandCandidate(
        SnapshotEnvelope snapshot,
        SmallModelActionParameter[] parameters) => Assert.Single(Assert.Single(
            new CandidateOptionAvailabilityEvaluator().Evaluate(
                snapshot,
                new[]
                {
                    new OptionAvailabilityCandidate
                    {
                        OptionId = "fishing.catch_fish",
                        Parameters = parameters
                    }
                }).Options).EventCandidates);

    private static string GingerIslandCandidateDiagnostic(EventCandidate candidate) =>
        $"kind={candidate.Kind};available={candidate.Available};blocks={string.Join(",", candidate.BlockReasons)};parameters=" +
        string.Join(",", candidate.Parameters.Select(row => row.Name + "=" + row.Value));

    private static StardewAI.Contracts.Execution.ActionQueueItem CompileGingerIslandInteraction(
        SnapshotEnvelope snapshot,
        SmallModelActionParameter[] parameters,
        SnapshotEnvelope? compileSnapshot = null)
    {
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(
            snapshot,
            new[]
            {
                new OptionAvailabilityCandidate
                {
                    OptionId = "fishing.catch_fish",
                    Parameters = parameters
                }
            });
        var plan = new DailyPlanCompiler().Compile(
            new EventCandidateRanker().Rank(
                new StardewAI.Contracts.Training.BaselineTrainingReport(),
                availability,
                "goal.fishing.complete_master_angler"),
            snapshot.StateHash);
        var queue = new ActionQueueCompiler().Compile(plan, compileSnapshot ?? snapshot);
        return Assert.Single(queue.Items.Where(item => item.OptionId == "executor.interact"));
    }

    private static SnapshotEnvelope GingerIslandSnapshot(
        SnapshotEnvelope source,
        string locationId,
        int playerX,
        int playerY,
        JsonObject access)
    {
        var state = JsonSerializer.SerializeToNode(source.State, JsonOptions)!.AsObject();
        state["player"]!["location_id"]!["value"] = locationId;
        state["player"]!["tile_x"]!["value"] = playerX;
        state["player"]!["tile_y"]!["value"] = playerY;
        state["current_location"]!["map"]!["value"] = new JsonObject
        {
            ["location_id"] = locationId,
            ["width"] = 120,
            ["height"] = 120
        };
        state["locations"]!["collision_grid"]!["value"] = new JsonObject
        {
            ["location_id"] = locationId,
            ["width"] = 120,
            ["height"] = 120,
            ["notable_tiles"] = new JsonArray()
        };
        const string targetLocation = "IslandSouthEastCave";
        state["locations"]!["route_graph"] = Field(new JsonObject
        {
            ["edges"] = new JsonArray
            {
                new JsonObject
                {
                    ["kind"] = "warp",
                    ["from_location"] = locationId,
                    ["from_x"] = playerX + 1,
                    ["from_y"] = playerY,
                    ["target_location"] = targetLocation,
                    ["target_x"] = 1,
                    ["target_y"] = 1,
                    ["resolved"] = true
                }
            }
        });
        state["locations"]!["social_route_date_evidence"] = Field(new JsonObject
        {
            ["schema_version"] = "social_route_date_evidence.v2",
            ["capture_total_days"] = 5,
            ["all_location_static_walkability_complete"] = true,
            ["projection_status"] = "current_date_static_route_and_gate_evidence_complete_movement_timing_pending",
            ["location_count"] = 2,
            ["locations"] = new JsonArray
            {
                RouteLocationEvidence(locationId, 120, 120),
                RouteLocationEvidence(targetLocation, 120, 120)
            }
        });
        state["locations"]!["ginger_island_access"] = Field(access);
        return SnapshotFromState(source, state);
    }

    private static SnapshotEnvelope RewriteGingerIslandAccess(
        SnapshotEnvelope source,
        Action<JsonObject> rewrite)
    {
        var state = JsonSerializer.SerializeToNode(source.State, JsonOptions)!.AsObject();
        var access = state["locations"]!["ginger_island_access"]!["value"]!.AsObject();
        rewrite(access);
        return SnapshotFromState(source, state);
    }

    private static JsonObject GingerIslandAccess(string status)
    {
        var readyResort = status == "ready_to_restore_resort";
        return new JsonObject
        {
            ["schema_version"] = "ginger_island_access.v1",
            ["projection_status"] = "complete_locked_base_1.6.15_native_contract",
            ["status"] = status,
            ["target_access"] = new JsonObject
            {
                ["island_south_accessible"] = true,
                ["island_north_accessible"] = true,
                ["island_west_accessible"] = true,
                ["island_south_east_accessible"] = false,
                ["island_south_east_cave_accessible"] = false
            },
            ["repairs"] = new JsonObject
            {
                ["battery"] = Repair("(O)787", 5, 5, 4, 9, "WillyBoatDonateBatteries"),
                ["hull"] = Repair("(O)709", 200, 200, 6, 8, "WillyBoatDonateHardwood"),
                ["anchor"] = Repair("(O)337", 5, 5, 8, 10, "WillyBoatDonateIridium"),
                ["all_complete_or_pending"] = false,
                ["boat_fixed_received"] = false,
                ["boat_fixed_pending"] = false
            },
            ["ticket"] = new JsonObject
            {
                ["raw_action"] = "BoatTicket",
                ["tile_x"] = 4,
                ["tile_y"] = 9,
                ["exact_action_count"] = 1,
                ["identity_exact"] = true,
                ["price"] = 1000,
                ["money_available"] = 1000
            },
            ["walnuts"] = new JsonObject { ["current"] = 20, ["found"] = 51 },
            ["first_parrot"] = Perch("IslandHut", 7, 6, "Hut", 1, string.Empty, true),
            ["western_turtle_parrot"] = Perch("IslandSouth", 5, 9, "Turtle", 10, "Island_FirstParrot", true),
            ["island_house_parrot"] = Perch("IslandWest", 81, 40, "House", 20, string.Empty, true),
            ["resort_parrot"] = Perch("IslandSouth", 17, 22, "Resort", 20, "Island_UpgradeHouse", !readyResort)
        };
    }

    private static JsonObject Repair(
        string qualifiedItemId,
        int required,
        int available,
        int x,
        int y,
        string actionType) => new()
        {
            ["qualified_item_id"] = qualifiedItemId,
            ["required_count"] = required,
            ["available_count"] = available,
            ["tile_x"] = x,
            ["tile_y"] = y,
            ["complete_or_pending"] = false,
            ["expected_action_type"] = actionType
        };

    private static JsonObject Perch(
        string location,
        int x,
        int y,
        string name,
        int nuts,
        string requiredMail,
        bool complete) => new()
        {
            ["source_location"] = location,
            ["tile_x"] = x,
            ["tile_y"] = y,
            ["upgrade_name"] = name,
            ["required_nuts"] = nuts,
            ["expected_required_nuts"] = nuts,
            ["required_mail"] = requiredMail,
            ["expected_required_mail"] = requiredMail,
            ["available"] = !complete,
            ["current_state"] = complete ? "Complete" : "Idle",
            ["complete_or_pending"] = complete,
            ["identity_exact"] = true
        };
}
