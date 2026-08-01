using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed class MaterialTransferMainlineTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExplicitIntentFlowsThroughCandidatePlanAndExistingMechanicalPrimitive(
        bool withdraw)
    {
        var snapshot = MaterialTransferTestFixture.Snapshot(locked: false);
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(
            snapshot,
            new[]
            {
                new OptionAvailabilityCandidate
                {
                    OptionId = "inventory.transfer_item",
                    Parameters = MaterialTransferTestFixture.Parameters(
                        includeStand: false,
                        withdraw: withdraw)
                }
            },
            includeExecutorCalibrationOptions: true);

        var option = Assert.Single(availability.Options);
        Assert.True(option.Available, string.Join(";", option.BlockingReasons));
        var candidate = Assert.Single(option.EventCandidates);
        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Equal("transfer_inventory_item", candidate.Kind);
        Assert.Contains(candidate.Parameters, parameter =>
            parameter.Name == "stand_tile_x" && parameter.Value == "4");
        Assert.Contains(candidate.Parameters, parameter =>
            parameter.Name == "stand_tile_y" && parameter.Value == "6");

        var ranked = new EventCandidateRanker().Rank(
            new BaselineTrainingReport(),
            availability);
        var plan = new DailyPlanCompiler().Compile(ranked, snapshot.StateHash);

        Assert.Collection(
            plan.Steps,
            step => Assert.Equal("move_to_tile", step.Kind),
            step => Assert.Equal("transfer_material", step.Kind));

        var queue = new ActionQueueCompiler().Compile(plan, snapshot);

        Assert.Equal("pending", queue.Status);
        Assert.Collection(
            queue.Items,
            item => Assert.Equal("executor.move_to_tile", item.OptionId),
            item =>
            {
                Assert.Equal("executor.transfer_material", item.OptionId);
                Assert.Equal("transfer_material", Assert.Single(item.NormalizedCommand.Steps).StepType);
            });
    }

    [Fact]
    public void MissingIntentIsRejectedBeforeRanking()
    {
        var snapshot = MaterialTransferTestFixture.Snapshot(locked: false);

        var option = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "inventory.transfer_item" }, true)
            .Options.Single();

        Assert.False(option.Available);
        Assert.Empty(option.EventCandidates);
        Assert.Contains("material_transfer_intent_required", option.BlockingReasons);
    }

    [Fact]
    public void LockedChestIsRejectedByProjectionAndCandidateGate()
    {
        var snapshot = MaterialTransferTestFixture.Snapshot(locked: true);

        var option = new CandidateOptionAvailabilityEvaluator().Evaluate(
            snapshot,
            new[]
            {
                new OptionAvailabilityCandidate
                {
                    OptionId = "inventory.transfer_item",
                    Parameters = MaterialTransferTestFixture.Parameters(includeStand: false)
                }
            },
            true).Options.Single();

        Assert.False(option.Available);
        var candidate = Assert.Single(option.EventCandidates);
        Assert.False(candidate.Available);
        Assert.Contains("material_transfer_chest_locked_by_other_player", candidate.BlockReasons);
    }

    [Fact]
    public void HighLevelCompilerRebuildsDerivedProjectionAndTargetParameters()
    {
        var snapshot = MaterialTransferTestFixture.Snapshot(locked: false);
        var request = new SmallModelActionEnvelope
        {
            ModelOutputId = "material-transfer.high-level",
            SourceModel = "test",
            StateHash = snapshot.StateHash,
            GoalId = "test",
            ExecutionMode = "training_singleplayer",
            Actor = ExecutionTargetProfiles.CreateActor("training_singleplayer"),
            Actions = new[]
            {
                new SmallModelAction
                {
                    ActionId = "transfer.high-level",
                    OptionId = "inventory.transfer_item",
                    Parameters = MaterialTransferTestFixture.Parameters(includeStand: false)
                        .Concat(new[]
                        {
                            new SmallModelActionParameter { Name = "LOCATION_ID", Value = "forged" },
                            new SmallModelActionParameter { Name = "TARGET_TILE_X", Value = "999" },
                            new SmallModelActionParameter { Name = "MATERIAL_TRANSFER_PROJECTION_JSON", Value = "{}" }
                        }).ToArray()
                }
            }
        };

        var queue = new ActionQueueCompiler().Compile(request, snapshot);

        Assert.Equal("pending", queue.Status);
        var item = Assert.Single(queue.Items);
        Assert.Empty(item.NormalizedCommand.Steps);
        Assert.Contains(item.NormalizedCommand.Parameters, parameter =>
            parameter.Name == "location_id" && parameter.Value == "Farm");
        Assert.Contains(item.NormalizedCommand.Parameters, parameter =>
            parameter.Name == "target_tile_x" && parameter.Value == "4");
        var projectionJson = Assert.Single(item.NormalizedCommand.Parameters, parameter =>
            parameter.Name == "material_transfer_projection_json").Value;
        Assert.Contains("\"status\":\"projected\"", projectionJson, StringComparison.Ordinal);
    }

    [Fact]
    public void DuplicateCanonicalIntentFieldIsRejectedAsAmbiguous()
    {
        var snapshot = MaterialTransferTestFixture.Snapshot(locked: false);
        var request = new SmallModelActionEnvelope
        {
            ModelOutputId = "material-transfer.duplicate",
            SourceModel = "test",
            StateHash = snapshot.StateHash,
            GoalId = "test",
            ExecutionMode = "training_singleplayer",
            Actor = ExecutionTargetProfiles.CreateActor("training_singleplayer"),
            Actions = new[]
            {
                new SmallModelAction
                {
                    ActionId = "transfer.duplicate",
                    OptionId = "inventory.transfer_item",
                    Parameters = MaterialTransferTestFixture.Parameters(includeStand: false)
                        .Concat(new[]
                        {
                            new SmallModelActionParameter { Name = "quantity", Value = "1" }
                        }).ToArray()
                }
            }
        };

        var queue = new ActionQueueCompiler().Compile(request, snapshot);

        Assert.Equal("blocked", queue.Status);
        Assert.Contains(
            "material_transfer_typed_intent_required",
            Assert.Single(queue.Items).BlockingReasons);
    }

    [Fact]
    public void TwoChestIntentFailsClosedInsteadOfThrowing()
    {
        var snapshot = MaterialTransferTestFixture.Snapshot(locked: false);
        var parameters = MaterialTransferTestFixture.Parameters(includeStand: false);
        parameters.Single(parameter => parameter.Name == "source_node_id").Value = "chest:Farm:4,5";
        parameters.Single(parameter => parameter.Name == "destination_node_id").Value = "chest:Farm:8,5";
        parameters.Single(parameter => parameter.Name == "source_slot_index").Value = "0";
        parameters.Single(parameter => parameter.Name == "quantity").Value = "3";
        parameters.Single(parameter => parameter.Name == "expected_source_stack").Value = "5";

        var option = new CandidateOptionAvailabilityEvaluator().Evaluate(
            snapshot,
            new[]
            {
                new OptionAvailabilityCandidate
                {
                    OptionId = "inventory.transfer_item",
                    Parameters = parameters
                }
            },
            true).Options.Single();

        Assert.False(option.Available);
        var candidate = Assert.Single(option.EventCandidates);
        Assert.Contains("material_transfer_requires_one_player_inventory", candidate.BlockReasons);
        Assert.Contains("material_transfer_chest_access_not_unique", candidate.BlockReasons);
    }
}
