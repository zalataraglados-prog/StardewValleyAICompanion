using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Core.Training;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Core.Tests;

public sealed partial class MarriageTeacherCorpusTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OriginalSleepAndSameDayWeddingStoryPhasesRetainTheOriginPartition(bool story)
    {
        var run = story ? StoryTransition(Run("story")) : Run("sleep");
        var original = JsonSerializer.Serialize(run);
        Invoke("ValidateRuns", new[] { run });
        Assert.Equal(original, JsonSerializer.Serialize(run));
        var corpus = Corpus(run);
        Assert.Equal(1, corpus.NativeOutcomeCount);
        Assert.Equal(2, corpus.RowCount);
        Assert.False(corpus.MechanicalControlsArePreferenceLabels);
        Assert.False(corpus.FormalProductTrainingAuthorized);
        Assert.Equal(new[] { "train" }, corpus.CriterionCoverage.Single().NativeOutcomePartitions);
    }

    [Theory]
    [InlineData("wrong_before_day")]
    [InlineData("wrong_after_day")]
    [InlineData("foreign_option")]
    [InlineData("foreign_queue")]
    [InlineData("foreign_item")]
    [InlineData("foreign_nonce")]
    [InlineData("missing_native_proof")]
    [InlineData("repeated_transition")]
    [InlineData("preference_label")]
    public void WeddingStoryCompatibilityDoesNotRelaxNativeOrExactControlIdentity(string mutation)
    {
        var run = StoryTransition(Run("story"));
        var transition = JsonNode.Parse(run.JointCriterionTransition.GetRawText())!;
        var chain = JsonNode.Parse(run.QueueChain.GetRawText())!.AsArray();
        switch (mutation)
        {
            case "wrong_before_day": transition["total_day_before"] = 209; break;
            case "wrong_after_day": transition["total_day_after"] = 211; break;
            case "foreign_option": transition["option_id"] = "executor.wait_ticks"; break;
            case "foreign_queue": transition["queue_id"] = "foreign"; break;
            case "foreign_item": transition["queue_item_id"] = "foreign"; break;
            case "foreign_nonce": transition["request_nonce"] = "foreign"; break;
            case "missing_native_proof": run = run with { NativeOutcomeVerified = false }; break;
            case "repeated_transition": chain.Add(chain[3]!.DeepClone()); break;
            case "preference_label": chain[3]!["role"] = "independent_gift_preparation"; break;
        }
        Reject("ValidateRuns", new[] { run with
        {
            QueueChain = JsonSerializer.SerializeToElement(chain),
            JointCriterionTransition = JsonSerializer.SerializeToElement(transition)
        } });
    }

    private static MarriageTeacherCorpusRun StoryTransition(MarriageTeacherCorpusRun run)
    {
        var queue = run.QueueChain[3];
        return run with { JointCriterionTransition = JsonSerializer.SerializeToElement(new
        {
            queue_id = queue.GetProperty("queue_id").GetString(),
            queue_item_id = queue.GetProperty("queue_item_ids")[0].GetString(),
            request_nonce = queue.GetProperty("request_nonces")[0].GetString(),
            option_id = "executor.advance_story_event", total_day_before = 210, total_day_after = 210
        }) };
    }

    [Theory]
    [InlineData("omitted_preparation")]
    [InlineData("repeated_preparation")]
    [InlineData("omitted_controls")]
    [InlineData("control_label")]
    [InlineData("missing_nonce")]
    [InlineData("reused_nonce")]
    [InlineData("decision")]
    [InlineData("trajectory")]
    [InlineData("comparison")]
    [InlineData("queue_binding")]
    [InlineData("partition")]
    [InlineData("transition_option")]
    [InlineData("transition_nonce")]
    [InlineData("transition_day")]
    [InlineData("no_native")]
    public void IncompleteOrReusedIdentitiesCannotBecomeNewJointEvidence(string mutation)
    {
        var run = Run("a");
        var chain = JsonNode.Parse(run.QueueChain.GetRawText())!.AsArray();
        var transition = JsonNode.Parse(run.JointCriterionTransition.GetRawText())!;
        var rows = run.PreparationDecisions;
        switch (mutation)
        {
            case "omitted_preparation": rows = rows.Take(1).ToArray(); break;
            case "repeated_preparation": rows[1] = rows[1] with { PreparationIndex = 0 }; break;
            case "omitted_controls": while (chain.Count > 2) chain.RemoveAt(chain.Count - 1); break;
            case "control_label": chain[2]!["role"] = "independent_gift_preparation"; break;
            case "missing_nonce": chain[0]!["request_nonces"] = new JsonArray(); break;
            case "reused_nonce":
                chain[1]!["request_nonces"]![0] = rows[0].RequestNonces[0];
                rows[1] = rows[1] with { RequestNonces = rows[0].RequestNonces };
                break;
            case "decision":
                rows[1] = rows[1] with { BeforeStateHash = rows[0].BeforeStateHash, SelectedCandidateId = rows[0].SelectedCandidateId };
                chain[1]!["before_state_hash"] = rows[0].BeforeStateHash;
                chain[1]!["candidate_id"] = rows[0].SelectedCandidateId;
                break;
            case "trajectory": rows[1] = rows[1] with { TrajectoryId = rows[0].TrajectoryId }; break;
            case "comparison": rows[1] = rows[1] with { StrictPairCount = 0 }; break;
            case "queue_binding": rows[1] = rows[1] with { QueueItemIds = rows[0].QueueItemIds }; break;
            case "partition": run = run with { DatasetPartition = "test" }; break;
            case "transition_option": transition["option_id"] = "executor.social_interact"; break;
            case "transition_nonce": transition["request_nonce"] = "foreign"; break;
            case "transition_day": transition["total_day_after"] = 211; break;
            case "no_native": run = run with { NativeOutcomeVerified = false }; break;
        }
        run = run with { PreparationDecisions = rows, QueueChain = JsonSerializer.SerializeToElement(chain), JointCriterionTransition = JsonSerializer.SerializeToElement(transition) };
        Reject("ValidateRuns", new[] { run });
    }

    [Fact]
    public void CopiedSourcesCannotCountAsAnotherNativeOutcome()
    {
        var first = Run("a");
        var second = Run("b") with { NativeReceiptSha256 = first.NativeReceiptSha256 };
        Reject("ValidateRuns", new[] { first, second });
    }

    // Protocol-port fixtures only: the public builder additionally verifies the full native source.
    private static MarriageTeacherCorpusRun Run(string id, string partition = "train")
    {
        var number = 0;
        string save;
        do { save = "fixture" + number++; } while (PolicyTrajectoryDatasetBuilder.PartitionFor(save + ":1:spring:7") != partition);
        string Queue(int index) => id + ".queue." + index;
        string Item(int index) => id + ".item." + index;
        string Nonce(int index) => id + ".nonce." + index;
        string Before(int index) => id + ".before." + index;
        string After(int index) => id + ".after." + index;
        string Candidate(int index) => id + ".candidate." + index;
        var options = new[] { "executor.traverse_connector", "executor.social_interact", "executor.sleep", "executor.advance_story_event" };
        var chain = Enumerable.Range(0, 4).Select(index => new
        {
            index, role = index < 2 ? "independent_gift_preparation" : "mechanical_control_not_preference_label",
            candidate_id = Candidate(index), queue_id = Queue(index), queue_item_ids = new[] { Item(index) },
            primitive_options = new[] { options[index] }, request_nonces = new[] { Nonce(index) },
            before_state_hash = Before(index), after_state_hash = After(index), total_day_before = index < 3 ? 209 : 210,
            total_day_after = index < 2 ? 209 : 210
        }).ToArray();
        return new()
        {
            SourceId = id, NativeReceiptPath = "native" + id, NativeReceiptSha256 = "hash" + id, RunId = "run" + id,
            SaveId = save, PlayerId = "player", OriginSplitKey = save + ":1:spring:7", DatasetPartition = partition,
            BeforeStateHash = Before(0), AfterStateHash = "deferred-wedding-return" + id, TotalDayBefore = 209, TotalDayAfter = 210,
            FinalSnapshotSha256 = "final" + id, NativeOutcomeVerified = true, QueueChain = JsonSerializer.SerializeToElement(chain),
            JointCriterionTransition = JsonSerializer.SerializeToElement(new { queue_id = Queue(2), queue_item_id = Item(2),
                request_nonce = Nonce(2), option_id = options[2], total_day_before = 209, total_day_after = 210 }),
            PreparationDecisions = Enumerable.Range(0, 2).Select(index => new MarriageTeacherCorpusDecision(index,
                "row" + id + index, "hash", "trajectory" + id + index, Before(index), After(index), Candidate(index), "denominator", 83,
                Queue(index), new[] { Item(index) }, new[] { options[index] }, new[] { Nonce(index) }, 1, new[] { "uncompared" }, false)).ToArray()
        };
    }
}
