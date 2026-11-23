using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Strategy;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    private sealed class DeveloperSavedSourceFixture : IDisposable
    {
        internal SavedReadbackOwnerFixture Parent { get; } = new();
        internal CropTeacherManagedSeedOwnerOptions Options { get; private set; } = null!;
        internal JsonObject Manifest { get; private set; } = null!;
        internal SnapshotEnvelope Loaded => LifecycleRead<SnapshotEnvelope>(Parent.LoadedPath);
        internal string Root => Path.Combine(Parent.Witness.Root, "developer-source");
        internal string ManifestPath => Path.Combine(Root, "source.json");
        internal string RequestPath => Path.Combine(Root, "request.json");
        internal string ReplyPath => Path.Combine(Root, "reply.json");

        internal async Task Prepare(Action<SnapshotEnvelope>? prepareLoaded = null)
        {
            Directory.CreateDirectory(Root);
            await using var owner = await Parent.Night.Open();
            await Parent.CompleteNight(owner, prepareLoaded);
            var acceptedPath = Path.Combine(Parent.Night.Options.EvidenceDirectory, "operation-000003-accepted.json");
            var before = LifecycleRead<StrategyCommitmentLedger>(owner.CurrentLedgerPath);
            var request = new ReservationPortfolioCommitRequest
            {
                StateHash = Loaded.StateHash, ExpectedLedgerRevision = before.Revision,
                PortfolioId = "fixture.developer-rebind", SourceDecisionId = "fixture.developer-rebind", GoalId = "earn_money"
            };
            var reply = new ReservationPortfolioLedgerService().Commit(before, Loaded, request, RecordedAt);
            Assert.True(reply.Accepted, string.Join(",", reply.Errors));
            var ledger = StrategyCommitmentReadReconciler.Reconcile(reply.Ledger!, Loaded, RecordedAt);
            var ledgerPath = Path.Combine(Root, "rebound-ledger.json");
            var reviewPath = Path.Combine(Root, "review.json");
            Fixture.Save(RequestPath, request); Fixture.Save(ReplyPath, reply); Fixture.Save(ledgerPath, ledger);
            Fixture.Save(reviewPath, new
            {
                status = "developer_saved_day_readback_accepted", source_sequence = 3,
                copied_file_bytes_match = true, manual_fork_review = true,
                generic_native_reload_certificate_claimed = false, formal_training_authorized = false,
                input_sha256 = new Dictionary<string, string> { [Parent.LoadedPath] = SavedResourceFixture.Hash(Parent.LoadedPath) }
            });
            Options = Parent.Night.Options with
            {
                EvidenceDirectory = Path.Combine(Root, "child-owner"), UseSelectedRefillResourceEffects = true,
                NativeSaveBinding = new(Path.Combine(Root, "copied-saves"), Parent.Night.Options.NativeSaveBinding!.SaveSlot),
                DeveloperSavedSourcePath = ManifestPath
            };
            Manifest = JsonSerializer.SerializeToNode(new
            {
                schema_version = "crop_teacher_developer_saved_source.v1", run_id = Options.RunId,
                developer_only = true, exclude_from_formal_training = true, formal_product_training_authorized = false,
                parent_owner_directory = Parent.Night.Options.EvidenceDirectory, parent_accepted_sequence = 3,
                parent_header_sha256 = SavedResourceFixture.Hash(Path.Combine(Parent.Night.Options.EvidenceDirectory, "owner-intent.json")),
                parent_acceptance_sha256 = SavedResourceFixture.Hash(acceptedPath),
                snapshot_path = Parent.LoadedPath, ledger_path = ledgerPath, review_path = reviewPath,
                rebind_request_path = RequestPath, rebind_reply_path = ReplyPath,
                save_binding = Options.NativeSaveBinding, process = Parent.Witness.Observation.Process,
                input_artifact_hashes = new[] { Parent.LoadedPath, ledgerPath, reviewPath, RequestPath, ReplyPath }
                    .ToDictionary(path => path, SavedResourceFixture.Hash)
            }, JsonOptions)!.AsObject();
            SaveManifest();
        }

        internal void SaveManifest() => Fixture.Save(ManifestPath, Manifest);
        internal Task<CropTeacherManagedSeedOwner> Open() => CropTeacherManagedSeedOwner.OpenAsync(Options,
            (path, json) => InvokeLive<Task>("WritePortfolioJournalFileAsync", path, json));
        internal Task<CropTeacherManagedSeedOwner> Resume() => CropTeacherManagedOwnerResume.OpenAsync(Options.EvidenceDirectory,
            Options.RunId, Options.ExecutorVersion, Options.BackendUrl, Options.PortfolioJournalDirectory, Options.NativeSaveBinding!,
            (_, _) => throw new InvalidOperationException("Saved-source resume must not rewrite evidence."));
        public void Dispose() => Parent.Dispose();
    }
}
