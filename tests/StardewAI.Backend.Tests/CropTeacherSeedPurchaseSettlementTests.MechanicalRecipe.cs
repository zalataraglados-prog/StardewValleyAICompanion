using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Core.Training;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Theory]
    [InlineData("executor.till_soil", "authoritative_crop_site_preparation_plan.v1")]
    [InlineData("executor.water_crop", "authoritative_crop_watering_plan.v1")]
    [InlineData("executor.harvest_crop", "authoritative_crop_harvest_plan.v1")]
    public void MechanicalRecipeRetainsOriginalWholeModelAndDoesNotMutateCallerInputs(string option, string source)
    {
        var options = new LiveTrainingOptions();
        var actor = JsonSerializer.SerializeToNode(options.TargetActor, JsonOptions)!;
        var parent = new JsonObject { ["actor"] = actor };
        var parameters = new[] { new SmallModelActionParameter { Name = "target_location", Value = "Farm" } };
        var original = JsonSerializer.SerializeToNode(new
        {
            schema_version = "small_model_action.v1", model_output_id = "model-output:candidate",
            source_model = source, state_hash = "accepted-full", goal_id = "daily.closed_loop",
            execution_mode = options.TargetExecutionMode, actor = options.TargetActor,
            actions = new[] { new { action_id = "candidate:action", option_id = option, rationale = source, parameters } }
        }, JsonOptions);
        var model = CropMechanicalActionModelBuilder.Build("accepted-full", source, "candidate", option,
            parameters, options.TargetExecutionMode, actor);
        Assert.True(JsonNode.DeepEquals(original, model)); Assert.Same(parent, actor.Parent);
        Assert.NotSame(actor, model["actor"]);
        Assert.True(JsonNode.DeepEquals(model, InvokeLive<JsonObject>("BuildCropTeacherActionModel", options,
            "accepted-full", source, "candidate", option, parameters)));
        model["actions"]![0]!["parameters"]![0]!["value"] = "changed-copy";
        Assert.Equal("Farm", parameters[0].Value);
    }

    [Theory]
    [InlineData("direct_till_ready")]
    [InlineData("clear_then_fresh_snapshot_rebind")]
    [InlineData("clear_access_then_fresh_snapshot_rebind")]
    public void MechanicalRecipeSharedSiteFrontierRetainsOriginalFirstStepIdentity(string status)
    {
        var first = new MoneyRevenueCropSitePreparationStep(1, 1, "observed", status, "executor.till_soil", "first",
            Array.Empty<SmallModelActionParameter>(), "next", Array.Empty<string>());
        var second = first with { CandidateId = "second" };
        var plan = new MoneyRevenueCropSitePreparationPlan { Steps = new[] { first, second } };
        Assert.Same(first, MoneyRevenueCropSitePreparationPlanBuilder.SelectNextExecutableStep(plan));
        Assert.Same(first, CropTeacherSitePreparationFrontier.SelectNextExecutableStep(plan));
    }

    [Fact]
    public void MechanicalRecipeSharedSiteFrontierRetainsNullEmptyAndBlockedContracts()
    {
        Assert.Throws<ArgumentNullException>(() => MoneyRevenueCropSitePreparationPlanBuilder.SelectNextExecutableStep(null!));
        Assert.Throws<ArgumentNullException>(() => CropTeacherSitePreparationFrontier.SelectNextExecutableStep(null!));
        Assert.Null(MoneyRevenueCropSitePreparationPlanBuilder.SelectNextExecutableStep(new()));
        var blocked = new MoneyRevenueCropSitePreparationStep(1, 1, "observed", "blocked", "", "",
            Array.Empty<SmallModelActionParameter>(), "next", new[] { "first", "", "first", "second" });
        var plan = new MoneyRevenueCropSitePreparationPlan { Steps = new[] { blocked } };
        Assert.Equal("crop_teacher_site_preparation_blocked:first,second",
            Assert.Throws<InvalidDataException>(() => MoneyRevenueCropSitePreparationPlanBuilder.SelectNextExecutableStep(plan)).Message);
    }
}
