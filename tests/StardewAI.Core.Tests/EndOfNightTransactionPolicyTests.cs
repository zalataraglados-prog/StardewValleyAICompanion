using StardewAI.Contracts.Capabilities;
using StardewAI.RuntimePrimitives;

namespace StardewAI.Core.Tests;

public sealed class EndOfNightTransactionPolicyTests
{
    [Theory]
    [InlineData("sleep_pending", false, true)]
    [InlineData("native_outcomes_pending", false, true)]
    [InlineData("awaiting_planned_profession_selection", false, true)]
    [InlineData("ready_for_safe_next_day_action", false, true)]
    [InlineData("ready_for_safe_next_day_action", true, false)]
    [InlineData("debug_negative_fixture_pending", true, true)]
    [InlineData("unknown", true, true)]
    [InlineData("complete", false, false)]
    [InlineData("blocked", false, false)]
    public void NativeControlOwnershipDoesNotDependOnFollowupWorkEvidence(
        string status,
        bool sawSafeNewDayReadyState,
        bool expected)
    {
        Assert.Equal(
            expected,
            EndOfNightTransactionPolicy.RequiresNativeTransitionControl(
                status,
                sawSafeNewDayReadyState));
    }

    [Fact]
    public void ReleasingNativeControlDoesNotCompleteTheCompositeProof()
    {
        const string status = "ready_for_safe_next_day_action";

        Assert.False(
            EndOfNightTransactionPolicy.RequiresNativeTransitionControl(
                status,
                sawSafeNewDayReadyState: true));
        Assert.True(
            EndOfNightTransactionPolicy.MatchesActiveSleepRequest(
                status, "run", "queue", "item", "run", "queue", "item"));
    }

    [Fact]
    public void ProductionOverallBudgetRemainsUnchangedByDebugFixtures()
    {
        Assert.Equal(36000, EndOfNightTransactionPolicy.MaximumOverallTicks);
    }

    [Theory]
    [InlineData("sleep_pending", "run", "queue", "item", true)]
    [InlineData("native_outcomes_pending", "run", "queue", "item", true)]
    [InlineData("complete", "run", "queue", "item", false)]
    [InlineData("blocked", "run", "queue", "item", false)]
    [InlineData("sleep_pending", "other", "queue", "item", false)]
    [InlineData("sleep_pending", "run", "other", "item", false)]
    [InlineData("sleep_pending", "run", "queue", "other", false)]
    public void SleepCallbacksOnlyMatchTheCurrentNonTerminalRequest(
        string status,
        string requestRunId,
        string requestQueueId,
        string requestQueueItemId,
        bool expected)
    {
        Assert.Equal(
            expected,
            EndOfNightTransactionPolicy.MatchesActiveSleepRequest(
                status,
                "run",
                "queue",
                "item",
                requestRunId,
                requestQueueId,
                requestQueueItemId));
    }

    [Theory]
    [InlineData("run", "queue", "item", true)]
    [InlineData("other", "queue", "item", false)]
    [InlineData("run", "other", "item", false)]
    [InlineData("run", "queue", "other", false)]
    public void TerminalReceiptAttachmentUsesIdentityWithoutReopeningState(
        string requestRunId,
        string requestQueueId,
        string requestQueueItemId,
        bool expected)
    {
        Assert.Equal(
            expected,
            EndOfNightTransactionPolicy.MatchesSleepRequestIdentity(
                "run",
                "queue",
                "item",
                requestRunId,
                requestQueueId,
                requestQueueItemId));
    }

    [Theory]
    [InlineData("farm_event:SoundInTheNightEvent", 7200)]
    [InlineData("story_event:Event", 7200)]
    [InlineData("minigame:TargetGame", 7200)]
    [InlineData("menu:ShippingMenu:1", 3600)]
    [InlineData("menu:SaveGameMenu:2", 3600)]
    [InlineData("native_save_processing", 3600)]
    [InlineData("menu:LevelUpMenu:ordinary:3", 1800)]
    [InlineData("menu:DialogueBox:4", 1800)]
    [InlineData("menu:UnknownModMenu:5", 900)]
    [InlineData("controller_handoff", 2400)]
    public void TransitionBudgetsAreBoundedPerObservedKind(
        string transitionKey,
        int expectedTicks)
    {
        Assert.Equal(
            expectedTicks,
            EndOfNightTransactionPolicy.TransitionTickBudget(transitionKey));
    }

    [Theory]
    [InlineData("debug.prepare_native_sleep")]
    [InlineData("executor.close_menu")]
    [InlineData("executor.close_shipping_summary")]
    [InlineData("executor.face_direction")]
    [InlineData("executor.select_safe_item_slot")]
    [InlineData("executor.sleep")]
    [InlineData("executor.wait")]
    [InlineData("executor.wait_ticks")]
    [InlineData("recovery.close_blocking_menu")]
    [InlineData("recovery.stabilize_day")]
    [InlineData("skills.choose_profession")]
    [InlineData("story.advance_event")]
    [InlineData("story.advance_event_minigame")]
    [InlineData("minigame.play_junimo_kart")]
    [InlineData("executor.advance_story_event")]
    [InlineData("executor.advance_story_event_minigame")]
    [InlineData("executor.choose_dialogue_response")]
    public void NonWorkActionsCannotCompleteTheTransaction(string optionId)
    {
        Assert.False(
            EndOfNightTransactionPolicy.IsSafeNextDayExecutionOption(
                optionId));
    }

    [Theory]
    [InlineData("executor.move_to_tile")]
    [InlineData("executor.traverse_connector")]
    [InlineData("executor.harvest_crop")]
    public void VerifiedProductExecutionPrimitivesCanCompleteTheTransaction(
        string optionId)
    {
        Assert.True(
            EndOfNightTransactionPolicy.IsSafeNextDayExecutionOption(
                optionId));
        Assert.True(ProductExecutorCapabilityCatalog.IsSupported(optionId));
    }

    [Fact]
    public void UnregisteredExecutorPrefixCannotCompleteTheTransaction()
    {
        const string optionId = "executor.unregistered_action";

        Assert.False(ProductExecutorCapabilityCatalog.IsSupported(optionId));
    }

    [Theory]
    [InlineData("exploration.visit_location")]
    [InlineData("farm.maintain_crops")]
    [InlineData("social.talk")]
    public void PlanningOptionsCannotMasqueradeAsExecutionPrimitives(
        string optionId)
    {
        Assert.False(
            EndOfNightTransactionPolicy.IsSafeNextDayExecutionOption(
                optionId));
    }

    [Theory]
    [InlineData("debug.prepare_native_sleep")]
    [InlineData("executor.move_to_tile")]
    [InlineData("recovery.stabilize_day")]
    [InlineData("skills.choose_profession")]
    [InlineData("story.advance_event")]
    [InlineData("minigame.play_junimo_kart")]
    public void ControlPlansCannotCompleteTheTransaction(string optionId)
    {
        Assert.False(
            EndOfNightTransactionPolicy.IsSafeNextDayPlanningOption(optionId));
    }

    [Theory]
    [InlineData("exploration.visit_location")]
    [InlineData("farm.maintain_crops")]
    [InlineData("social.talk_npc")]
    public void WorkPlansCanCompleteTheTransaction(string optionId)
    {
        Assert.True(
            EndOfNightTransactionPolicy.IsSafeNextDayPlanningOption(optionId));
    }
}
