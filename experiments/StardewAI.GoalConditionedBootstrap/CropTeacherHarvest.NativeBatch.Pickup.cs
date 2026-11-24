using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;

namespace StardewAI.GoalConditionedBootstrap;

public sealed record CropTeacherHarvestPickupFiles(string BeforeSnapshotPath, string QueuePath, string ReceiptPath, string AfterSnapshotPath)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool ObservedWithoutDispatch { get; init; }
}

public sealed partial class CropTeacherHarvest
{
    public sealed partial class NativeBatch
    {
        public sealed class Pickup
        {
            internal NativeBatch Batch { get; }
            internal SnapshotEnvelope Before { get; }
            internal CropTeacherRetainedInputs Source { get; }
            internal string PreviousPath { get; }
            internal string SourcePath { get; }
            internal long GroundQuantity { get; }
            public SmallModelActionEnvelope Model { get; }
            public ActionQueueEnvelope Queue { get; }
            public string CandidateId { get; }

            internal Pickup(NativeBatch batch, SnapshotEnvelope before, string path, CropTeacherRetainedInputs source,
                long ground, SmallModelActionEnvelope model, ActionQueueEnvelope queue, string candidateId)
            {
                Batch = batch; Before = before; Source = source; PreviousPath = batch.AfterSnapshotPath; SourcePath = path;
                GroundQuantity = ground; Model = model; Queue = queue; CandidateId = candidateId;
            }
        }

        public Pickup PreparePickup(string path) => PreparePickup(ReadPickupCapture(path));

        private Pickup PreparePickup(PickupCapture capture)
        {
            Require(capture.PassiveGain == 0, "An already observed crop pickup must settle before native dispatch.");
            var before = capture.Snapshot;
            var item = Prepared.HarvestItemQualifiedId;
            foreach (var drop in capture.Drops)
            {
                if (drop["debris_index"]?.GetValue<int>() is not int index || index < 0 ||
                    drop["item_quality"]?.GetValue<int>() is not int quality || quality < 0 ||
                    drop["chunks"] is not JsonArray { Count: > 0 } chunks ||
                    chunks[0]?["tile_x"]?.GetValue<int>() is not int x || chunks[0]?["tile_y"]?.GetValue<int>() is not int y)
                    continue;
                var location = Prepared.LocationId;
                var candidate = FormattableString.Invariant($"pickup-debris:{location}:{index}:{x},{y}:{item}");
                static SmallModelActionParameter Arg(string key, string value) => new() { Name = key, Value = value };
                static string Text(int value) => value.ToString(CultureInfo.InvariantCulture);
                var model = new SmallModelActionEnvelope
                {
                    SourceModel = "native_first_harvest_custody.v1", ModelOutputId = "model-output:" + candidate,
                    StateHash = before.StateHash, Actor = queue.Actor, GoalId = queue.GoalId, ExecutionMode = queue.ExecutionMode,
                    Actions = new[] { new SmallModelAction { ActionId = candidate + ":action", OptionId = "executor.pickup_debris",
                        Rationale = "native_first_harvest_custody.v1", Parameters = new[] { Arg("target_location", location),
                            Arg("target_tile_x", Text(x)), Arg("target_tile_y", Text(y)), Arg("debris_index", Text(index)),
                            Arg("qualified_item_id", item), Arg("item_quality", Text(quality)), Arg("precondition", "candidate_id:" + candidate) } } }
                };
                var compiled = new ActionQueueCompiler().Compile(model, before);
                if (compiled.Status == "pending" && compiled.Items is [{ Status: "pending", OptionId: "executor.pickup_debris" }])
                    return new(this, before, capture.Path, capture.Input, capture.GroundQuantity, model, compiled, candidate);
            }
            throw new InvalidDataException("Owned harvest has no compilable native crop drop pickup.");
        }

        public void ObservePickup(Pickup pickup, CropTeacherHarvestPickupFiles files)
        {
            Require(!files.ObservedWithoutDispatch && !completed && NeedsPickup && ReferenceEquals(pickup.Batch, this) && Comparer.Equals(AfterSnapshotPath, pickup.PreviousPath) &&
                    Comparer.Equals(files.BeforeSnapshotPath, pickup.SourcePath), "Owned harvest pickup does not follow its prepared batch.");
            var paths = new[] { files.QueuePath, files.ReceiptPath, files.AfterSnapshotPath };
            var retained = InputHashes;
            Require(paths.All(Path.IsPathFullyQualified) && paths.Distinct(Comparer).Count() == paths.Length &&
                    paths.All(path => !retained.ContainsKey(path) && !Comparer.Equals(path, pickup.SourcePath)),
                "Owned harvest pickup outputs overlap retained inputs.");
            var outputInputs = new CropTeacherRetainedInputs(paths);
            var queue = CurrentTeacherFrontierSupport.Read<ActionQueueEnvelope>(files.QueuePath, "original harvest pickup queue");
            CropTeacherOriginalQueueCompilation.Verify(queue, CropTeacherSeedSupportSource.Copy(pickup.Queue), "Owned harvest pickup");
            var receipt = CurrentTeacherFrontierSupport.Read<QueueExecutionReceiptEnvelope>(files.ReceiptPath, "native harvest pickup receipt");
            var after = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(files.AfterSnapshotPath, "native harvest pickup after");
            var reasons = QueueExecutionReceiptValidator.Validate(queue, pickup.Before, receipt, after, transition.RunId,
                transition.ExecutorVersion, pickup.CandidateId, pickup.Before.StateHash, requireTeacherPreferenceStateRebound: false);
            Require(reasons.Length == 0 && receipt.StepResults is [{ PrimitiveKind: "pickup_debris" }],
                "Owned harvest pickup receipt rejected: " + string.Join(",", reasons));
            var step = receipt.StepResults[0];
            CropTeacherNativeStepBoundary.Verify(pickup.Before, after, Before, step, Prepared.TotalDay, "Harvest pickup");
            MoneyRevenueCropNativeLifecycleProofBuilder.VerifyMaintenanceLocation(pickup.Before, after, Prepared.LocationId);
            var reference = transition with { QueuePath = files.QueuePath, BeforeSnapshotPath = files.BeforeSnapshotPath,
                ExecutionReceiptPath = files.ReceiptPath, AfterSnapshotPath = files.AfterSnapshotPath };
            var stepPath = CropTeacherNativeStepPaths.Read(reference, receipt,
                retained.Keys.Concat(paths).Append(pickup.SourcePath).ToArray(), "Harvest pickup").Single();
            CropTeacherRetainedInputs? stepInputs = null;
            if (!Comparer.Equals(stepPath, files.AfterSnapshotPath))
            {
                stepInputs = new CropTeacherRetainedInputs(new[] { stepPath });
                var stepAfter = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(stepPath, "native harvest pickup step after");
                Require(EqualJson(stepAfter, after), "Owned harvest pickup has an unbound native after frame.");
            }
            var gained = VerifyPickupDelta(pickup.Before, after,
                pickup.GroundQuantity - CropSeedResourceBoundary.CountHarvestDebris(after, Prepared.HarvestItemQualifiedId));
            pickup.Source.Validate();
            if (stepInputs is not null) inputs.Add(stepInputs);
            inputs.Add(pickup.Source);
            inputs.Add(outputInputs);
            AdvancePickup(files, after, gained);
        }

        internal static CropTeacherHarvestPickupFiles[] ReadPickupFiles(string path)
        {
            Require(Path.IsPathFullyQualified(path) && Comparer.Equals(path, Path.GetFullPath(path)), "Owned harvest pickup manifest path is not canonical.");
            var root = Path.GetDirectoryName(path)! + Path.DirectorySeparatorChar;
            var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            var files = CurrentTeacherFrontierSupport.Read<CropTeacherHarvestPickupFiles[]>(path, "harvest pickup settlement");
            bool Canonical(string input) => Path.IsPathFullyQualified(input) && Comparer.Equals(input, Path.GetFullPath(input));
            bool Inside(string input) => Canonical(input) && input.StartsWith(root, comparison) && !Comparer.Equals(path, input);
            Require(files is { Length: > 0 } && files.All(row => row is not null && Inside(row.AfterSnapshotPath) &&
                (row.ObservedWithoutDispatch
                    ? Canonical(row.BeforeSnapshotPath) && row.QueuePath == string.Empty && row.ReceiptPath == string.Empty
                    : Inside(row.BeforeSnapshotPath) && Inside(row.QueuePath) && Inside(row.ReceiptPath))),
                "Owned harvest pickup artifacts escaped their settlement directory.");
            return files;
        }
    }
}
