using System.Reflection;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;
using StardewAI.Core.Training;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Core.Tests;

public sealed class MasterAnglerTeacherNativeJoinTests
{
    [Fact]
    public void ReadOnlyEnvelopeEntryUsesOriginalDatasetValidationWithoutAdmittingTeacherEvidence()
    {
        var row = StardewAI.Testing.PolicySupervisionFixture.Observation();
        Assert.Null(PolicyTrajectoryDatasetBuilder.ValidateEnvelope(row));
        Assert.Null(row.Audit.TeacherSupervision);
        row.Versions.FeatureSchema = "unknown";
        Assert.Equal("version_binding_missing", PolicyTrajectoryDatasetBuilder.ValidateEnvelope(row));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BindingOnlyConnectsMatchingRodOrTrapEvidence(bool trap) => Invoke(Fixture(trap));

    [Theory]
    [InlineData("run")]
    [InlineData("save")]
    [InlineData("state")]
    [InlineData("executor")]
    [InlineData("partition")]
    [InlineData("before_source")]
    [InlineData("after_source")]
    [InlineData("missing_source")]
    [InlineData("inventory")]
    [InlineData("lowering")]
    [InlineData("candidate")]
    [InlineData("selected_option")]
    [InlineData("teacher_candidate")]
    [InlineData("outcome_queue")]
    [InlineData("primitive")]
    [InlineData("unverified_outcome")]
    [InlineData("stale_capture")]
    [InlineData("duration")]
    [InlineData("item")]
    [InlineData("native_queue")]
    [InlineData("queue_semantics")]
    [InlineData("duplicate_item")]
    [InlineData("other_fish")]
    [InlineData("no_credit")]
    [InlineData("no_transition")]
    [InlineData("failed_transition")]
    public void BindingOnlyRejectsCrossSourceOrUnrelatedCollectionEvidence(string mode)
    {
        var f = Fixture(false);
        switch (mode)
        {
            case "run": f = f with { Native = f.Native with { RunId = "foreign" } }; break;
            case "save": f.Row.Context.SaveId = "foreign"; break;
            case "state": f.Row.SourceStateHash = "foreign"; break;
            case "executor": f.Row.Versions.Executor = PolicyTrajectoryVersionPins.ProductExecutor; break;
            case "partition": f.Row.Context.DatasetPartition = f.Row.Context.DatasetPartition == "train" ? "test" : "train"; break;
            case "before_source": f.Hashes["teacher.before_snapshot"] = "other"; break;
            case "after_source": f.Hashes["teacher.after_snapshot"] = "other"; break;
            case "missing_source": f.Hashes.Remove("teacher.after_snapshot"); break;
            case "inventory": f.Hashes["teacher.requirement_inventory"] = "other"; break;
            case "lowering": f.Hashes["teacher.acquisition_lowering"] = "other"; break;
            case "candidate": f.Row.Selection.CandidateId = "other"; break;
            case "selected_option": f.Row.Selection.OptionId = "other"; break;
            case "teacher_candidate": f.Row.Audit.TeacherSupervision!.SelectedCandidateId = "other"; break;
            case "outcome_queue": f.Row.Outcome.QueueId = "other"; break;
            case "primitive": f.Row.Outcome.PrimitiveOptionId = "other"; break;
            case "unverified_outcome": f.Row.Outcome.Success = false; break;
            case "stale_capture": f.Row.Outcome.AfterSnapshotFresh = false; break;
            case "duration": f.Row.Outcome.ActualTicks++; break;
            case "item": f.Row.Audit.TeacherSupervision!.SelectedQueueItemIds = new[] { "other" }; break;
            case "native_queue": f.Queue.QueueId = "other"; break;
            case "queue_semantics": f.Queue.Items[0].NormalizedCommand.Parameters = new[] { new SmallModelActionParameter { Name = "quantity", Value = "999" } }; break;
            case "duplicate_item": f.Queue.Items = new[] { f.Queue.Items[0], f.Queue.Items[0] }; break;
            case "other_fish": f = f with { Native = f.Native with { QualifiedItemId = "(O)other" } }; break;
            case "no_credit": f.Preference.SelectedCandidate = f.Preference.SelectedCandidate! with { RequirementCredits = Array.Empty<CurrentCollectionRequirementCredit>() }; break;
            case "no_transition": f.Row.Audit.TeacherSupervision!.RequirementTransitions = Array.Empty<PolicyTeacherRequirementTransition>(); break;
            case "failed_transition": f.Row.Audit.TeacherSupervision!.RequirementTransitions[0].Verified = false; break;
        }
        var exception = Assert.Throws<TargetInvocationException>(() => Invoke(f));
        Assert.IsType<InvalidDataException>(exception.InnerException);
    }

    private sealed record BindingFixture(MasterAnglerNativeCatchReceipt Native, PolicyDecisionTrajectoryEnvelope Row,
        CurrentStageOneCollectionTeacherPreferenceLabel Preference, ActionQueueEnvelope Queue, Dictionary<string, string> Hashes);

    private static BindingFixture Fixture(bool trap)
    {
        var primitive = trap ? "executor.collect_crab_pot" : "executor.catch_fish";
        var semantic = trap ? "fishing.collect_crab_pots" : "fishing.catch_fish";
        var nativeHashes = new Dictionary<string, string>
        {
            ["before"] = "before-source", ["after"] = "after-source",
            ["requirement_inventory"] = "inventory-source", ["acquisition_lowering"] = "lowering-source"
        };
        var native = new MasterAnglerNativeCatchReceipt("request", nativeHashes, "run", "save", "actor", "1.6.15", "1.0",
            6, primitive, "queue", "item", "nonce", "(O)test", "before", "after", 10, 30);
        var queue = new ActionQueueEnvelope
        {
            QueueId = "queue", Items = new[] { new ActionQueueItem { QueueItemId = "item", OptionId = primitive } }
        };
        var row = new PolicyDecisionTrajectoryEnvelope
        {
            RunId = "run", SourceStateHash = "before",
            Context = new() { SaveId = "save", Year = 1, Season = "spring", Day = 7, SplitKey = "save:1:spring:7" },
            Versions = new() { Executor = PolicyTrajectoryVersionPins.RuntimeTestHarnessExecutor },
            Selection = new() { CandidateId = "candidate", OptionId = semantic },
            Outcome = new() { QueueId = "queue", PrimitiveOptionId = primitive, Status = "applied", Success = true, AfterSnapshotFresh = true, ActualTicks = 20 },
            Audit = new() { TeacherSupervision = new()
            {
                SelectedCandidateId = "candidate", SelectedQueueItemIds = new[] { "item" },
                RequirementTransitions = new[] { new PolicyTeacherRequirementTransition
                {
                    RequirementSetId = "master_angler", QualifiedItemId = "(O)test",
                    TransitionKind = "native_collection_false_to_true", Verified = true, BeforeValue = "False", AfterValue = "True"
                } }
            } }
        };
        row.Context.DatasetPartition = PolicyTrajectoryDatasetBuilder.PartitionFor(row.Context.SplitKey);
        var preference = new CurrentStageOneCollectionTeacherPreferenceLabel
        {
            SelectedCandidate = new("candidate", semantic, trap ? "collect_crab_pot" : "catch_fish", "fixture", 1, 2,
                new[] { new CurrentCollectionRequirementCredit("master_angler", "fixture", 0, "(O)test", "authoritative_window_terminal_attempt", 1, 0, "fixture") }, "fixture"),
            CompiledQueue = JsonSerializer.Deserialize<ActionQueueEnvelope>(JsonSerializer.Serialize(queue))!
        };
        var hashes = new Dictionary<string, string>
        {
            ["teacher.before_snapshot"] = "before-source", ["teacher.after_snapshot"] = "after-source",
            ["teacher.requirement_inventory"] = "inventory-source", ["teacher.acquisition_lowering"] = "lowering-source"
        };
        return new(native, row, preference, queue, hashes);
    }

    private static void Invoke(BindingFixture f) => typeof(MasterAnglerTeacherNativeJoinBuilder)
        .GetMethod("ValidateBinding", BindingFlags.NonPublic | BindingFlags.Static)!
        .Invoke(null, new object[] { f.Native, f.Row, f.Preference, f.Queue, f.Hashes });
}
