using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Infrastructure;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    // Synthetic wire conversion only: no native execution or training evidence is produced.
    private sealed class NativeRefillProductFixture(bool product = true) : IDisposable
    {
        internal RefillTraceFixture Trace { get; } = new(repair: false,
            completeSnapshotInventory: true, resourceNeutral: true, observeNativeOutputs: false);
        private readonly List<string> productResults = [];
        private readonly List<string> productJournals = [];
        private string DirectoryPath => Trace.Approach.Watering.Shared.Runtime.LiveOptions.SnapshotDir;
        internal string ReceiptPath => product ? Path.Combine(DirectoryPath, "refill-product-aggregate.json") : Trace.ReceiptPath;
        internal string AfterPath => Trace.AfterPath;
        internal IReadOnlyList<string> Results => product ? productResults : Trace.Results;
        internal IReadOnlyList<string> Journals => product ? productJournals : Trace.Journals;
        internal string RunId => Trace.Approach.Watering.Shared.Runtime.LiveOptions.RunId;

        internal async Task Build(bool retainCompilation = false)
        {
            await Trace.Build(retainCompilation: retainCompilation);
            if (!product) return;
            var aggregate = JsonNode.Parse(File.ReadAllText(Trace.ReceiptPath))!.AsObject();
            for (var index = 0; index < Trace.Results.Count; index++)
            {
                var original = ExecutionRequestJournalReader.Read(Trace.Journals[index]);
                var beforePath = original.Metadata.GetProperty("before_snapshot_path").GetString()!;
                var before = JsonSerializer.Deserialize<SnapshotEnvelope>(File.ReadAllText(beforePath), JsonOptions)!;
                var after = JsonSerializer.Deserialize<SnapshotEnvelope>(File.ReadAllText(Trace.AfterFrames[index]), JsonOptions)!;
                var written = await RuntimeExecutionRequestJournal.WriteAsync(DirectoryPath, 231, index + 1,
                    original.RequestJson, "http://test/api/v1/product/execute", beforePath);
                var journal = ExecutionRequestJournalReader.Read(written.JournalPath);
                Assert.Equal(original.RequestJson, journal.RequestJson);
                Assert.Equal(original.Request.RequestNonce, journal.Request.RequestNonce);
                productJournals.Add(journal.JournalPath);

                var request = journal.Request;
                var result = JsonNode.Parse(File.ReadAllText(Trace.Results[index]))!.AsObject();
                result["execution_request_journal_path"] = journal.JournalPath;
                result["execution_request_sha256"] = journal.Metadata.GetProperty("raw_request_sha256").GetString();
                result["execution_request_nonce"] = request.RequestNonce;
                result["source"] = "product_executor";
                result["product_executor_schema_version"] = "stardewai.product_executor.v1";
                result["product_authorization_status"] = "authorized";
                result["product_receipt_id"] = Convert.ToHexString(SHA256.HashData(
                    Encoding.UTF8.GetBytes(request.RunId + "\n" + request.RequestNonce))).ToLowerInvariant();
                result["product_request_sha256"] = journal.Metadata.GetProperty("product_request_sha256").GetString();
                result["product_request_before_state_hash"] = request.BeforeStateHash;
                result["product_before_state_hash"] = before.StateHash;
                result["product_before_game_tick"] = before.GameTick;
                result["product_after_state_hash"] = after.StateHash;
                result["product_after_game_tick"] = after.GameTick;
                result["product_request_state_hash_drift"] = false;
                result["product_dispatch_guard"] = "native_action_preconditions";
                result["product_replan_required"] = false;
                result["product_idempotent_replay"] = false;
                var resultPath = Path.Combine(DirectoryPath, "refill-product-result-" + index + ".json");
                Fixture.Save(resultPath, result);
                productResults.Add(resultPath);
                aggregate["step_results"]![index]!["execution_path"] = resultPath;
                aggregate["step_results"]![index]!["execution_request_nonce"] = request.RequestNonce;
            }
            Fixture.Save(ReceiptPath, aggregate);
        }

        internal CropTeacherRefillApproachNativeTrace OpenTrace(bool selected, string executorVersion) => selected
            ? CropTeacherRefillApproachNativeTrace.OpenNativeWithSelectedResourceEffects(
                Trace.Approach.Open(), ReceiptPath, AfterPath, RunId, executorVersion)
            : CropTeacherRefillApproachNativeTrace.OpenNativeResourceNeutral(
                Trace.Approach.Open(), ReceiptPath, AfterPath, RunId, executorVersion);

        internal CropTeacherWateringMaintenance.Preparation OpenPreparation(bool selected, bool native)
        {
            var approach = Trace.Approach;
            var shared = approach.Watering.Shared;
            if (native)
                return selected
                    ? CropTeacherWateringMaintenance.Preparation.OpenNativeRefillApproachWithSelectedResourceEffects(
                        shared.Inputs.Plan.RequestPath, shared.Source.SourcePath, shared.Source.LedgerPath,
                        shared.Inputs.Plan.CoveredCommitmentIds, Id, "refill-product.synthetic", approach.ModelPath, approach.QueuePath)
                    : CropTeacherWateringMaintenance.Preparation.OpenNativeRefillApproach(
                        shared.Inputs.Plan.RequestPath, shared.Source.SourcePath, shared.Source.LedgerPath,
                        shared.Inputs.Plan.CoveredCommitmentIds, Id, "refill-product.synthetic", approach.ModelPath, approach.QueuePath);
            return selected
                ? CropTeacherWateringMaintenance.Preparation.OpenRefillApproachWithSelectedResourceEffects(
                    shared.Inputs.Plan.RequestPath, shared.Source.SourcePath, shared.Source.LedgerPath,
                    shared.Inputs.Plan.CoveredCommitmentIds, Id, "refill-product.synthetic", approach.ModelPath, approach.QueuePath)
                : CropTeacherWateringMaintenance.Preparation.OpenRefillApproach(
                    shared.Inputs.Plan.RequestPath, shared.Source.SourcePath, shared.Source.LedgerPath,
                    shared.Inputs.Plan.CoveredCommitmentIds, Id, "refill-product.synthetic", approach.ModelPath, approach.QueuePath);
        }

        internal MoneyRevenueCropNativeLifecycleTransitionRequest Transition(
            CropTeacherWateringMaintenance.Preparation preparation, string executorVersion) => new(
                "crop_actions", preparation.TotalDay, Trace.Approach.QueuePath,
                Trace.Approach.Watering.Shared.Source.SourcePath, ReceiptPath, AfterPath,
                RunId, executorVersion, preparation.CandidateId);

        public void Dispose() => Trace.Dispose();
    }
}
