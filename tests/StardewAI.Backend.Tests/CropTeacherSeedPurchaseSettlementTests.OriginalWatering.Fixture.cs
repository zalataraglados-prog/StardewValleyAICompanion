using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    private sealed class OwnedWateringFixture : IDisposable
    {
        internal WateringFixture Basis { get; }
        internal OwnedWateringFixture(int water = 20, bool atRefillApproach = false) =>
            Basis = new(water: water, bowlMirrors: true, atRefillApproach: atRefillApproach);
        internal OwnedRouteFixture Shared => Basis.Shared;
        internal string QueuePath => Path.Combine(Shared.Runtime.LiveOptions.SnapshotDir, "crop-teacher-batch-0001-compiled-queue.json");
        internal string ModelPath => Path.Combine(Shared.Runtime.LiveOptions.SnapshotDir, "crop-teacher-batch-0001-model-action.json");
        internal string ReceiptPath => Shared.ReceiptPath;
        internal string AfterPath => Shared.AfterPath;

        internal Task<CropTeacherManagedSeedOperation> Prepare(CropTeacherManagedSeedOwner owner) =>
            InvokeLive<Task<CropTeacherManagedSeedOperation>>("PrepareCropTeacherOwnedWateringAsync", Shared.Runtime.Client,
                Shared.Runtime.LiveOptions, 1, Basis.Step, Shared.Inputs.Plan, Shared.Inputs.Request, owner, Shared.Files.Native);

        internal async Task<JsonObject> EmitNative(int applied = 2, bool failedSuffix = false, bool refillOnlySuffix = false)
        {
            await Basis.Emit(applied: applied, failedSuffix: failedSuffix, refillOnlySuffix: refillOnlySuffix, retainedQueuePath: QueuePath);
            Fixture.Save(ReceiptPath, JsonNode.Parse(File.ReadAllText(Basis.Transition.ExecutionReceiptPath))!);
            Fixture.Save(AfterPath, Basis.After);
            return new JsonObject { ["execution_path"] = ReceiptPath, ["after_snapshot_path"] = AfterPath };
        }

        internal Task Rebind(CropTeacherManagedSeedOwner owner, CropTeacherManagedSeedOperation operation) =>
            owner.RunWateringMaintenanceAsync(operation, Shared.Runtime.Client, (diagnostic, request) =>
                InvokeLive<Task<JsonObject>>("PostJournaledPortfolioCommitAsync", Shared.Runtime.Client,
                    Shared.Runtime.LiveOptions, diagnostic, request), Shared.Files.Post);

        public void Dispose() => Basis.Dispose();
    }
}
