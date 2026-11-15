using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Contracts.Training;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;
using static StardewAI.Backend.Tests.CropTeacherSeedPurchaseSettlementTests;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedTransferTests
{
    // Declared wire/native observations and original Backend mutations; not actual game evidence.
    private sealed class SharedTransferReturnFixture : IDisposable
    {
        internal Fixture Files { get; } = new(nativeEvidence: true, depleteSource: true, emptyChest: true, noPlayerSeed: true,
            beforeChange: before =>
            {
                before.State["environment"] = JsonSerializer.SerializeToElement(new
                {
                    training_run_id = new { status = "available", value = "run.owned.seed.transfer" },
                    training_mode = new { status = "available", value = "1" },
                    save_isolation_path = new { status = "available", value = "isolated-shared-transfer-fixture" }
                });
                CropTeacherNativeSupportFixture.CompleteEnvelopeMetadata(before, "declared-shared-transfer-fixture", "2026-11-15T00:00:00Z");
            });
        internal RuntimeFixture Runtime { get; }
        internal MoneyRevenueCropNativeLifecycleTransitionRequest Transition { get; set; } = null!;
        internal CropTeacherSharedTransferSettlementReference Reference { get; private set; } = null!;
        internal ReservationPortfolioSupportingTransitionSettlementRequest Request { get; private set; } = null!;
        internal ReservationPortfolioSupportingTransitionSettlementResult Reply { get; private set; } = null!;
        internal CropTeacherSharedTransferPreparationReference PreparationReference { get; set; } = null!;
        internal MoneyRevenueCropNativeLifecycleTransitionRequest? NativeTemplate { get; set; }
        internal string NativeResultPath => Path.Combine(Path.GetDirectoryName(Files.SourcePath)!, "shared-transfer-native-result.json");
        internal string ReplyPath => Path.Combine(Path.GetDirectoryName(Files.SourcePath)!, "shared-transfer-settlement-reply.json");

        internal SharedTransferReturnFixture() => Runtime = new(Files);

        internal async Task Build(bool product = false, bool includePreparation = false,
            Func<SharedTransferReturnFixture, Task>? beforeNative = null, bool settle = true, bool alreadyPrepared = false)
        {
            Runtime.Options.SaveIsolationPath = "isolated-shared-transfer-fixture";
            Runtime.Options.SkipTraining = true;
            if (!alreadyPrepared) await Runtime.Prepare();
            var preparedLedger = Runtime.Repository.Get(Files.Before);
            if (!includePreparation) Files.AdoptLedger(preparedLedger);
            if (!alreadyPrepared)
            {
                Transition = Runtime.Transition with { TransitionKind = "shared_source_return" };
                var preparationJournal = Directory.EnumerateFiles(Runtime.Options.SnapshotDir, "*-rpc-journal.json")
                    .Single(path => JsonNode.Parse(File.ReadAllText(path))!["operation"]!.GetValue<string>() == "commit");
                var preparation = JsonNode.Parse(File.ReadAllText(preparationJournal))!;
                PreparationReference = new(Path.Combine(Path.GetDirectoryName(preparationJournal)!, preparation["request_file"]!.GetValue<string>()),
                    Path.Combine(Files.RuntimePath, "marker-reply.json"), preparationJournal, Runtime.Options.BackendUrl);
            }
            if (beforeNative is not null) await beforeNative(this);
            var queue = JsonSerializer.Deserialize<ActionQueueEnvelope>(File.ReadAllText(Transition.QueuePath), JsonOptions)!;
            var item = Assert.Single(queue.Items);
            var request = InvokeLive<TrainingExecutionRequest>("BuildExecutionRequest", Runtime.Options,
                JsonSerializer.SerializeToNode(item, JsonOptions)!.AsObject(), Files.Before.StateHash, queue.QueueId, item.OptionId);
            var journal = await RuntimeExecutionRequestJournal.WriteAsync(Runtime.Options.SnapshotDir, 400, 1,
                JsonSerializer.Serialize(request, JsonOptions), product ? "http://test/api/v1/product/execute" :
                    "http://test/api/v1/training/execute", Files.SourcePath);
            var facts = new[] { new SimulatedFactChange { Path = "player.inventory.(O)745", Before = "0", After = "10" } };
            var result = JsonSerializer.SerializeToNode(new TrainingExecutionResult
            {
                RunId = request.RunId, QueueId = queue.QueueId, QueueItemId = item.QueueItemId, OptionId = item.OptionId,
                BeforeStateHash = Files.Before.StateHash, Status = "applied", FeedbackAvailable = true,
                PrimitiveKind = "transfer_material", PrimitiveVerificationStatus = "verified", ChangedFacts = facts,
                ProductAfterStateHash = Files.After.StateHash, ProductAfterGameTick = Files.After.GameTick
            }, JsonOptions)!.AsObject();
            result["execution_request_journal_path"] = journal.JournalPath;
            result["execution_request_nonce"] = request.RequestNonce; result["execution_request_sha256"] = journal.RawRequestSha256;
            result["mechanical_compiled_queue_path"] = Transition.QueuePath;
            result["after_snapshot_path"] = Transition.AfterSnapshotPath; result["after_state_hash"] = Files.After.StateHash;
            result["before_game_tick"] = Files.Before.GameTick; result["after_game_tick"] = Files.After.GameTick;
            result["after_snapshot_fresh"] = true;
            if (product)
            {
                result["source"] = "product_executor"; result["product_executor_schema_version"] = "stardewai.product_executor.v1";
                result["product_authorization_status"] = "authorized";
                result["product_receipt_id"] = HashText(request.RunId + "\n" + request.RequestNonce);
                result["product_request_sha256"] = journal.ProductRequestSha256;
                result["product_request_before_state_hash"] = Files.Before.StateHash;
                result["product_before_state_hash"] = Files.Before.StateHash; result["product_before_game_tick"] = Files.Before.GameTick;
                result["product_request_state_hash_drift"] = false; result["product_dispatch_guard"] = "native_action_preconditions";
                result["product_replan_required"] = false; result["product_idempotent_replay"] = false;
            }
            Fixture.Save(NativeResultPath, result);
            var template = NativeTemplate ?? Runtime.Transition;
            var aggregate = JsonNode.Parse(File.ReadAllText(template.ExecutionReceiptPath))!;
            if (alreadyPrepared && Runtime.Options.UseProductExecutor)
                aggregate["executor_version"] = Runtime.Options.PolicyTrajectoryExecutorVersion;
            var step = aggregate["step_results"]![0]!;
            aggregate["queue_id"] = queue.QueueId; step["effective_queue_id"] = queue.QueueId; step["queue_item_id"] = item.QueueItemId;
            step["option_id"] = item.OptionId; step["effective_queue_item"] = JsonSerializer.SerializeToNode(item, JsonOptions);
            step["before_snapshot_path"] = Transition.BeforeSnapshotPath; step["after_snapshot_path"] = Transition.AfterSnapshotPath;
            step["execution_path"] = NativeResultPath; step["execution_request_nonce"] = request.RequestNonce;
            step["changed_facts"] = JsonSerializer.SerializeToNode(facts, JsonOptions);
            step["selected_queue_candidate_completed"] = true;
            aggregate["after_snapshot_path"] = Transition.AfterSnapshotPath;
            if (Transition.AfterSnapshotPath != template.AfterSnapshotPath) Fixture.Save(Transition.AfterSnapshotPath, Files.After);
            Fixture.Save(Transition.ExecutionReceiptPath, aggregate);
            Runtime.ObserveAfter();
            if (!settle) return;
            var prepared = preparedLedger.PreparedMaterialTransfer!;
            var moved = prepared.ReservationProjection.ReservationTransitions.Select(row => row.ReservationId).ToHashSet(StringComparer.Ordinal);
            Request = new()
            {
                StateHash = Files.After.StateHash, ExpectedLedgerRevision = preparedLedger.Revision,
                PortfolioId = prepared.PortfolioId, GoalId = prepared.GoalId, RouteSourceDecisionId = prepared.RouteSourceDecisionId,
                PreparedMaterialTransferSha256 = prepared.BindingSha256, SupportingTransitionReceiptSha256 = Hash(Transition.ExecutionReceiptPath),
                MaterialRelocations = prepared.MaterialRelocations,
                RebindActiveReservationIds = preparedLedger.MaterialReservations.Where(row => row.Status == StrategyCommitmentStatuses.Active &&
                    row.SourceDecisionId == prepared.RouteSourceDecisionId && !moved.Contains(row.ReservationId))
                    .Select(row => row.ReservationId).ToArray(),
                Reason = "declared_original_shared_transfer"
            };
            var diagnostic = new JsonObject();
            var reply = await InvokeLive<Task<JsonObject>>("PostJournaledPortfolioSettlementAsync", Runtime.Client, Runtime.Options, diagnostic, Request);
            Assert.True(reply["accepted"]!.GetValue<bool>(), reply.ToJsonString());
            Fixture.Save(ReplyPath, reply);
            Reply = reply.Deserialize<ReservationPortfolioSupportingTransitionSettlementResult>(JsonOptions)!;
            var rpc = diagnostic["material_reservation_settlement_request_journal_path"]!.GetValue<string>();
            var rawJournal = JsonNode.Parse(File.ReadAllText(rpc))!;
            Reference = new(Path.Combine(Path.GetDirectoryName(rpc)!, rawJournal["request_file"]!.GetValue<string>()),
                ReplyPath, rpc, Runtime.Options.BackendUrl);
            if (includePreparation)
            {
                Reference = Reference with { Preparation = PreparationReference };
            }
        }

        internal CropTeacherSharedSourceReturn Open() => Reference.Preparation is not null
            ? CropTeacherSharedSourceReturn.OpenAfterPreparedAndSettledMaterialTransfer(
                Files.RequestPath, Files.SourcePath, Files.LedgerPath, [Id], Transition, "shared.return.transfer.fixture", Reference)
            : CropTeacherSharedSourceReturn.OpenAfterSettledMaterialTransfer(
            Files.RequestPath, Files.SourcePath, Files.LedgerPath, [Id], Transition, "shared.return.transfer.fixture", Reference);
        internal static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
        private static string HashText(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
        public void Dispose() => Runtime.Dispose();
    }
}
