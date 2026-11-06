using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Core.Training;
using static StardewAI.GoalConditionedBootstrap.NativeExecutionSourceSupport;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class MuseumTerminalNativeJoinBuilder
{
    public static MuseumTerminalNativeJoin Build(string requestPath) => BuildSource(requestPath).Receipt;

    private static VerifiedMuseumTerminalSource BuildSource(string requestPath)
    {
        requestPath = RequiredFile(requestPath);
        var request = ReadTyped<MuseumTerminalNativeJoinRequest>(requestPath, "museum_terminal_native_join_request.v1");
        Require(!string.IsNullOrWhiteSpace(request.RunId) && !string.IsNullOrWhiteSpace(request.PreferredCandidateId),
            "Museum join needs a run and an exact independently preferred candidate.");
        var comparisonPath = RequiredFile(request.ComparisonPath);
        var comparison = MuseumTerminalTeacherComparisonBuilder.Verify(comparisonPath);
        Require(comparison.IndependentPairwiseLabelEligible && comparison.PairwisePreferences.Any(row =>
            row.PreferredCandidateId == request.PreferredCandidateId), "Museum native candidate has no original independent pair.");
        var selected = comparison.CandidateEvaluations.Single(row => row.CandidateId == request.PreferredCandidateId);
        Require(selected.Admitted && selected.ProjectedNewAchievementCredit == 1 && selected.CompiledQueue is { Items.Length: 1 },
            "Museum native candidate has no original terminal compilation.");
        var comparisonRequest = ReadTyped<MuseumTerminalTeacherComparisonRequest>(comparison.RequestPath, "museum_terminal_teacher_comparison_request.v1");
        // Comparison Verify already rebuilt these sources. Metadata is reused only under its exact digest.
        var intake = CurrentTeacherFrontierSupport.Read<CurrentMuseumCollectionIntake>(RequiredFile(comparisonRequest.IntakePath), "Verified Museum intake");
        Require(CurrentTeacherFrontierSupport.HashFile(comparisonRequest.IntakePath) == comparison.SourceSha256["intake"],
            "Museum verified intake changed during join.");
        var source = NativeExecutionActionSourceReader.ReadQueue(request.ActionQueuePath, request.BeforeSnapshotPath,
            request.ExecutionReceiptPath, request.AfterSnapshotPath, request.RunId, request.PreferredCandidateId);
        Require(source.Actions.Length == 1 && source.Queue.Items.Length == 1 && source.Queue.GoalId == comparison.GoalId &&
            source.Queue.CandidateAudit.Count(row => row.Decision == "accepted") == 1 &&
            source.Queue.CandidateAudit.Single(row => row.Decision == "accepted").CandidateId == request.PreferredCandidateId &&
            source.Queue.Items.Select((item, index) => ExecutionReceiptValidationSupport.EffectiveQueueItemSemanticallyMatches(
                JsonSerializer.SerializeToElement(item, JsonDefaults.Options), selected.CompiledQueue!.Items[index])).All(matches => matches) &&
            JsonNode.DeepEquals(JsonNode.Parse(CurrentTeacherFrontierSupport.ReadArtifactText(intake.SnapshotPath)),
                JsonNode.Parse(CurrentTeacherFrontierSupport.ReadArtifactText(request.BeforeSnapshotPath))) &&
            source.Before.StateHash == comparison.SourceStateHash && source.Before.GameTick == comparison.GameTick &&
            Identity(source.Before.SaveId) == comparison.SaveId && Identity(source.Before.PlayerId) == comparison.PlayerId &&
            TotalDay(source.Before) == comparison.TotalDay && TotalDay(source.After) == comparison.TotalDay &&
            source.After.GameTick - source.Before.GameTick <= comparison.ComparisonHorizonTicks,
            "Museum native source differs from its exact independent frame/compilation or comparison horizon.");
        var action = source.Actions[0];
        Require(action.Result.OptionId == "executor.donate_museum_item" && (action.Result.PrimitiveVerificationReasons ?? Array.Empty<string>())
            .ToHashSet(StringComparer.Ordinal).SetEquals(new[] { "LibraryMuseum.OpenDonationMenu_completed", "MuseumMenu.receiveLeftClick_inventory_completed",
                "MuseumMenu.receiveLeftClick_display_completed", "Game1.exitActiveMenu_reward_settlement_completed" }),
            "Museum native action lacks the original full donation/menu settlement facts.");
        var slot = action.Request.InventorySlotIndex ?? -1;
        Require(slot >= 0 && action.Request.QualifiedItemId == selected.QualifiedItemId,
            "Museum native wire does not own the exact selected item slot.");
        var inventory = CurrentTeacherFrontierSupport.Read<AuthoritativeRequirementInventoryReport>(intake.RequirementInventoryPath, "Verified Museum authority");
        Require(CurrentTeacherFrontierSupport.HashFile(intake.RequirementInventoryPath) == comparison.SourceSha256["requirement_inventory"],
            "Museum verified requirement authority changed during join.");
        var set = CurrentTeacherFrontierSupport.SingleSet(inventory.RequirementSets, row => row.RequirementSetId, "museum_collection", "Museum native join");
        var transition = ValidateTransition(intake, set, source.Before, source.After, selected.QualifiedItemId, slot);
        var hashes = new Dictionary<string, string>(StringComparer.Ordinal)
            { ["request"] = CurrentTeacherFrontierSupport.HashFile(requestPath), ["comparison"] = CurrentTeacherFrontierSupport.HashFile(comparisonPath) };
        foreach (var hash in comparison.SourceSha256) hashes["comparison." + hash.Key] = hash.Value;
        foreach (var hash in source.SourceSha256) hashes["native." + hash.Key] = hash.Value;
        var receipt = new MuseumTerminalNativeJoin(requestPath, hashes, request.RunId, comparison.SaveId, comparison.PlayerId,
            comparison.TotalDay, comparison.SplitKey, comparison.DatasetPartition, selected.CandidateId, selected.QualifiedItemId, slot,
            transition.StackBefore, transition.StackAfter, comparison.OriginalDonationCandidateCount, comparison.PairwisePreferences, transition.Items,
            new(source.Queue.QueueId, action.Item.QueueItemId, action.Request.RequestNonce, source.Before.StateHash, source.After.StateHash,
                source.Before.GameTick, source.After.GameTick, action.Result.ActualTicks));
        return new(receipt, comparison, intake, source);
    }

    public static MuseumTerminalNativeJoin Verify(string joinPath) => VerifySource(joinPath).Receipt;

    internal static VerifiedMuseumTerminalSource VerifySource(string joinPath)
    {
        var text = CurrentTeacherFrontierSupport.ReadArtifactText(RequiredFile(joinPath));
        var stored = JsonSerializer.Deserialize<MuseumTerminalNativeJoin>(text, JsonDefaults.Options) ??
            throw new InvalidDataException("Museum native join is null.");
        var rebuilt = BuildSource(stored.RequestPath);
        Require(JsonNode.DeepEquals(JsonNode.Parse(text), JsonSerializer.SerializeToNode(rebuilt.Receipt, JsonDefaults.Options)),
            "Museum native join differs from its complete original sources.");
        return rebuilt;
    }

    private static void Require(bool condition, string reason)
    {
        if (!condition) throw new InvalidDataException(reason);
    }
}

internal sealed record VerifiedMuseumTerminalSource(MuseumTerminalNativeJoin Receipt, MuseumTerminalTeacherComparison Comparison,
    CurrentMuseumCollectionIntake Intake, VerifiedNativeExecutionQueue Native);
