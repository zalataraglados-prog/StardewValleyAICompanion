using System.Text.Json;
using StardewAI.Contracts.Capabilities;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed class AnimalPurchaseMainlineTests
{
    private const string Identity = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    [Fact]
    public void ExactOpenPurchaseMenuFlowsToNativeTransactionPrimitive()
    {
        var snapshot = Snapshot(occupants: 1, capacity: 4);
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(
            snapshot,
            new[] { "animals.purchase" },
            includeExecutorCalibrationOptions: true);
        var candidate = Assert.Single(Assert.Single(availability.Options).EventCandidates);

        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Equal("purchase_animal", candidate.Kind);
        Assert.Contains(candidate.Parameters, row =>
            row.Name == "continuation.animal_type_id" && row.Value == "Chicken");
        Assert.Contains(candidate.Parameters, row =>
            row.Name == "continuation.home_building_tile_x" && row.Value == "10");

        var ranked = new EventCandidateRanker().Rank(new(), availability);
        var plan = new DailyPlanCompiler().Compile(ranked, snapshot.StateHash);
        var planStep = Assert.Single(plan.Steps);
        Assert.Equal("purchase_animal", planStep.Kind);
        Assert.Contains("native_PurchaseAnimalsMenu_lifecycle_only", planStep.SafetyConstraints);
        Assert.Contains(planStep.Parameters, row => row.Name == "animal_type_id" && row.Value == "Chicken");

        var item = Assert.Single(new ActionQueueCompiler().Compile(plan, snapshot).Items);
        Assert.Equal("executor.purchase_animal", item.OptionId);
        Assert.Empty(item.BlockingReasons);
        Assert.Equal("purchase_animal", Assert.Single(item.NormalizedCommand.Steps).StepType);
    }

    [Fact]
    public void FullHomeIsExcludedBeforeRanking()
    {
        var snapshot = Snapshot(occupants: 4, capacity: 4);
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(
            snapshot,
            new[] { "animals.purchase" },
            includeExecutorCalibrationOptions: true);
        var candidate = Assert.Single(Assert.Single(availability.Options).EventCandidates);

        Assert.False(candidate.Available);
        Assert.Contains("animal_purchase_home_full", candidate.BlockReasons);
    }

    [Fact]
    public void RuntimeUsesNativeMenuControlsWithoutDirectAdoptionOrMoneyMutation()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(root, "tools", "StardewAI.RuntimeTestHarness", "ModEntry.AnimalPurchases.cs"));
        var supported = File.ReadAllText(Path.Combine(root, "tools", "StardewAI.RuntimeTestHarness", "ModEntry.SupportedOptions.cs"));
        var loop = File.ReadAllText(Path.Combine(root, "tools", "StardewAI.LiveTrainingLoop", "Program.RuntimeExecution.cs"));
        Assert.Contains("menu.receiveLeftClick", source, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("active.Menu.receiveLeftClick", source, StringComparison.Ordinal);
        Assert.Contains("debug.setup_animal_purchase", supported, StringComparison.Ordinal);
        Assert.Contains("ReadQueueParameterInt(item, \"expected_price\")", loop, StringComparison.Ordinal);
        Assert.DoesNotContain("adoptAnimal(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.player.Money -=", source, StringComparison.Ordinal);
        Assert.DoesNotContain("animalsThatLiveHere.Add", source, StringComparison.Ordinal);
    }

    [Fact]
    public void TypedRuntimeRequestRoundTripsPurchaseProjection()
    {
        var request = new TrainingExecutionRequest
        {
            OptionId = "executor.purchase_animal",
            AnimalTypeId = "Chicken",
            PossibleActualTypeIdsJson = "[\"White Chicken\",\"Brown Chicken\"]",
            AnimalPurchaseTargetLocationId = "Farm",
            AnimalHomeBuildingType = "Coop",
            AnimalHomeBuildingTileX = 10,
            AnimalHomeBuildingTileY = 12,
            GeneratedAnimalName = "AI Chicken 1",
            ExpectedAnimalHomeOccupantCountBefore = 1,
            ExpectedAnimalHomeCapacity = 4,
            AnimalPurchaseCandidateIdentitySha256 = Identity
        };
        var roundTrip = JsonSerializer.Deserialize<TrainingExecutionRequest>(
            JsonSerializer.Serialize(request, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

        Assert.Equal("Chicken", roundTrip.AnimalTypeId);
        Assert.Equal("Farm", roundTrip.AnimalPurchaseTargetLocationId);
        Assert.Equal(10, roundTrip.AnimalHomeBuildingTileX);
        Assert.Equal(4, roundTrip.ExpectedAnimalHomeCapacity);
    }

    [Fact]
    public void LiveAnimalPurchaseAdapterIsAuthorizedByRequiredFactPolicy()
    {
        var option = new StardewAI.Core.OptionRegistry.OptionRegistry().GetRequired("animals.purchase");
        Assert.Contains("vanilla_1_6_animal_purchase", option.RequiredFactPolicy.DefaultRule.AllowedAdapterIds);
    }

    [Fact]
    public void HighLevelPurchaseRemainsOutsideTrainingUntilDialogueAndPagingRuntimeCalibration()
    {
        var highLevel = OptionCapabilityRegistrySource.GetRequired("animals.purchase");
        var terminal = OptionCapabilityRegistrySource.GetRequired("executor.purchase_animal");

        Assert.Equal(TrainingEvidenceGateStatus.RuntimeVerified, highLevel.ReadTrainingGate);
        Assert.Equal(TrainingEvidenceGateStatus.RuntimeVerified, highLevel.CandidateTrainingGate);
        Assert.Equal(TrainingEvidenceGateStatus.RuntimeVerified, highLevel.CompilerTrainingGate);
        Assert.Equal(TrainingEvidenceGateStatus.Missing, highLevel.RuntimeTrainingGate);
        Assert.Equal(TrainingEvidenceGateStatus.Missing, highLevel.OutputTrainingGate);
        Assert.DoesNotContain("animals.purchase", OptionCapabilityRegistrySource.TrainingAllowlist);
        Assert.Equal(new[] { "EVD-247" }, terminal.RuntimeEvidenceIds);
        Assert.Equal(new[] { "EVD-247" }, terminal.OutputEvidenceIds);
    }

    [Fact]
    public void OffPageAnimalHomeCompilesOneNativeNextPageResponse()
    {
        var snapshot = PagedDialogueSnapshot();
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(
            snapshot, new[] { "animals.purchase" }, includeExecutorCalibrationOptions: true);
        var candidate = Assert.Single(Assert.Single(availability.Options).EventCandidates.Where(value =>
            value.ItemId == "Animal5"));

        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Equal("animal_purchase_navigate_location_page", candidate.Kind);
        Assert.Contains(candidate.Parameters, value =>
            value.Name == "dialogue_response_key" && value.Value == "nextPage");
        Assert.Contains(candidate.Parameters, value =>
            value.Name == "expected_menu_type_after" && value.Value == "DialogueBox");

        var ranked = new EventCandidateRanker().Rank(new(), availability);
        ranked = new[] { ranked.Single(value => value.CandidateId == candidate.CandidateId) };
        var plan = new DailyPlanCompiler().Compile(ranked, snapshot.StateHash);
        var queue = new ActionQueueCompiler().Compile(plan, snapshot);
        var item = Assert.Single(queue.Items);
        Assert.Equal("executor.choose_animal_purchase_response", item.OptionId);
        Assert.Empty(item.BlockingReasons);
    }

    private static SnapshotEnvelope Snapshot(int occupants, int capacity)
    {
        object Field(object value) => new
        {
            value,
            status = "available",
            source = new { kind = "game_object", path = "test" },
            adapter = "test",
            read_at_tick = 1,
            confidence = 1
        };
        var home = new
        {
            location_id = "Farm",
            building_type = "Coop",
            building_tile_x = 10,
            building_tile_y = 12,
            indoor_location_id = "Coop123",
            is_under_construction = false,
            valid_occupant_types = new[] { "Coop" },
            required_house_types = new[] { "Coop" },
            compatible_with_all_possible_types = true,
            occupant_count = occupants,
            capacity,
            available_slots = Math.Max(0, capacity - occupants)
        };
        var stock = new
        {
            animal_type_id = "Chicken",
            display_name = "Chicken",
            possible_actual_type_ids = new[] { "White Chicken", "Brown Chicken" },
            price = 800,
            required_building_met = true,
            player_money = 5000,
            can_afford = true,
            generated_unique_name = "AI Chicken 1",
            candidate_identity_sha256 = Identity,
            compatible_homes = new[] { home }
        };
        var raw = new Dictionary<string, object>
        {
            ["time"] = new Dictionary<string, object> { ["time"] = Field(1000) },
            ["player"] = new Dictionary<string, object>
            {
                ["location_id"] = Field("AnimalShop"),
                ["tile_x"] = Field(13),
                ["tile_y"] = Field(16),
                ["money"] = Field(5000)
            },
            ["farm"] = new Dictionary<string, object>
            {
                ["animal_purchase_catalog"] = Field(new[]
                {
                    new { target_location_id = "Farm", stock = new[] { stock } }
                })
            },
            ["locations"] = new Dictionary<string, object>
            {
                ["route_graph"] = Field(new { edges = Array.Empty<object>() })
            },
            ["menus"] = new Dictionary<string, object>
            {
                ["active_menu"] = Field(new { is_open = true, type = "PurchaseAnimalsMenu", last_question_key = (string?)null }),
                ["menu_specific_state"] = Field(new
                {
                    kind = "purchase_animals",
                    target_location_id = "Farm",
                    stock = new[] { new { animal_type_id = "Chicken", price = 800, required_building_met = true } }
                })
            }
        };
        var json = JsonSerializer.Serialize(raw);
        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!;
        return new SnapshotEnvelope
        {
            StateHash = SnapshotHash.ComputeStateHash(state),
            GameTick = 1,
            RealTimestamp = "2026-08-12T00:00:00Z",
            Completeness = "complete",
            State = state
        };
    }

    private static SnapshotEnvelope PagedDialogueSnapshot()
    {
        object Field(object value) => new
        {
            value,
            status = "available",
            source = new { kind = "game_object", path = "test" },
            adapter = "test",
            read_at_tick = 1,
            confidence = 1
        };
        var locations = Enumerable.Range(0, 6).Select(index => new
        {
            target_location_id = "Farm" + index,
            native_location_choice_index = index,
            stock = new[]
            {
                new
                {
                    animal_type_id = "Animal" + index,
                    display_name = "Animal " + index,
                    possible_actual_type_ids = new[] { "Animal" + index },
                    price = 800,
                    required_building_met = true,
                    player_money = 5000,
                    can_afford = true,
                    generated_unique_name = "AI Animal " + index,
                    candidate_identity_sha256 = index.ToString("x64"),
                    compatible_homes = new[]
                    {
                        new
                        {
                            location_id = "Farm" + index,
                            building_type = "Coop",
                            building_tile_x = 10 + index,
                            building_tile_y = 12,
                            indoor_location_id = "Coop" + index,
                            is_under_construction = false,
                            compatible_with_all_possible_types = true,
                            occupant_count = 0,
                            capacity = 4,
                            available_slots = 4
                        }
                    }
                }
            }
        }).ToArray();
        var responses = Enumerable.Range(0, 5)
            .Select(index => (object)new { response_key = "Farm" + index, response_text = "Farm " + index })
            .Append(new { response_key = "nextPage", response_text = "Next" })
            .ToArray();
        var raw = new Dictionary<string, object>
        {
            ["time"] = new Dictionary<string, object> { ["time"] = Field(1000) },
            ["player"] = new Dictionary<string, object>
            {
                ["location_id"] = Field("AnimalShop"), ["tile_x"] = Field(13),
                ["tile_y"] = Field(16), ["money"] = Field(5000)
            },
            ["farm"] = new Dictionary<string, object> { ["animal_purchase_catalog"] = Field(locations) },
            ["locations"] = new Dictionary<string, object> { ["route_graph"] = Field(new { edges = Array.Empty<object>() }) },
            ["menus"] = new Dictionary<string, object>
            {
                ["active_menu"] = Field(new { is_open = true, type = "DialogueBox", last_question_key = "pagedResponse" }),
                ["menu_specific_state"] = Field(new { kind = "dialogue", responses })
            }
        };
        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(JsonSerializer.Serialize(raw))!;
        return new SnapshotEnvelope
        {
            StateHash = SnapshotHash.ComputeStateHash(state), GameTick = 1,
            RealTimestamp = "2026-08-12T00:00:00Z", Completeness = "complete", State = state
        };
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "StardewValleyAICompanion.sln")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("repository root not found");
    }
}
