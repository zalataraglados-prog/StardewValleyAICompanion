using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Strategy;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.LiveTrainingLoop;

public sealed record CropTeacherOriginalTillingCompilation(string CommitmentId, string ObservationPath,
    string SiteRequestPath, string SitePlanPath, string OriginalModelActionPath, string ModelActionPath,
    string QueuePath, string ReceiptPath, string AfterSnapshotPath, string RuntimeDirectory);

public sealed partial class CropTeacherManagedSeedOwner
{
    public async Task<CropTeacherManagedSeedOperation> CompileAndAdmitOriginalTillingAsync(
        Func<Task<CropTeacherOriginalTillingCompilation>> compileOriginal)
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            ValidateState();
            if (Pending is not null || HeadRecoveryRequired)
                throw new InvalidOperationException("Recover the existing crop phase before original till compilation.");
            var value = await compileOriginal().ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(value.CommitmentId) || TillingPaths(value).Any(path => !Path.IsPathFullyQualified(path)))
                throw new InvalidDataException("Original till requires its explicit commitment and absolute artifact paths.");
            var record = CreateAdmissionRecord(headSnapshotPath, headLedgerPath, value.RuntimeDirectory);
            record["operation_kind"] = "site_till";
            record["till_execution_protocol"] = "original_loop.v1";
            record["till_compilation"] = JsonSerializer.SerializeToNode(value, JsonOptions);
            record["original_model_action_path"] = Path.GetFullPath(value.ModelActionPath);
            record["active_commitment_ids"] = JsonSerializer.SerializeToNode(ActiveCoveredCommitments(), JsonOptions);
            var transition = new MoneyRevenueCropNativeLifecycleTransitionRequest("crop_actions", 0,
                Path.GetFullPath(value.QueuePath), headSnapshotPath, Path.GetFullPath(value.ReceiptPath),
                Path.GetFullPath(value.AfterSnapshotPath), options.RunId, options.ExecutorVersion, "pending-original-candidate");
            record["till_transition"] = JsonSerializer.SerializeToNode(transition, JsonOptions);
            var start = OpenOriginalTillingStart(record, requireNativeIdentity: false);
            transition = transition with { TotalDay = start.TotalDay, SelectedCandidateId = start.CandidateId };
            record["till_transition"] = JsonSerializer.SerializeToNode(transition, JsonOptions);
            RequireNewSupportingOutputs(transition, watering: false);
            record["input_artifact_hashes"] = JsonSerializer.SerializeToNode(start.InputHashes, JsonOptions);
            record["original_intent_sha256"] = SupportingTransitionIntentHash(record, null);
            return await PersistAdmissionAsync(record, new(this, record, RecordPath(Sequence(record), "intent"), null,
                tillingStart: start)).ConfigureAwait(false);
        }
        finally { gate.Release(); }
    }

    public Task ExecuteOriginalTillingAsync(CropTeacherManagedSeedOperation operation, HttpClient http, Func<Task<JsonObject>> executeOriginal) =>
        ExecuteOriginalSupportingAsync(operation, http, executeOriginal, CropTeacherSupportingOperationKind.Tilling);

    public Task RunTillingAsync(CropTeacherManagedSeedOperation operation, HttpClient http,
        Func<JsonObject, ReservationPortfolioCommitRequest, Task<JsonObject>> commit,
        Func<string, string, Task<JsonObject>> post) => RunSupportingTransitionAsync(operation, http, commit, post, CropTeacherSupportingOperationKind.Tilling);

    private CropTeacherSiteTillCompilation OpenOriginalTillingStart(JsonObject record, bool requireNativeIdentity = true)
    {
        var value = record["till_compilation"]?.Deserialize<CropTeacherOriginalTillingCompilation>(JsonOptions)
            ?? throw new InvalidDataException("Original till compilation is missing.");
        var transition = record["till_transition"]?.Deserialize<MoneyRevenueCropNativeLifecycleTransitionRequest>(JsonOptions)
            ?? throw new InvalidDataException("Original till transition is missing.");
        var active = record["active_commitment_ids"]?.Deserialize<string[]>(JsonOptions);
        if (record["operation_kind"]?.GetValue<string>() != "site_till" || record["step"] is not null ||
            record["prior_verified_snapshot_path"] is not null || record["till_execution_protocol"]?.GetValue<string>() != "original_loop.v1" ||
            new[] { "route", "maintenance", "harvest", "overnight", "control", "terminal" }.Any(role =>
                record[role + "_transition"] is not null || record[role + "_execution_protocol"] is not null) ||
            TillingPaths(value).Concat(new[] { transition.QueuePath, transition.BeforeSnapshotPath,
                transition.ExecutionReceiptPath, transition.AfterSnapshotPath }).Any(path => !Path.IsPathFullyQualified(path)) ||
            !SamePath(StoredPath(record, "before_ledger_path"), headLedgerPath) ||
            !SamePath(transition.BeforeSnapshotPath, StoredPath(record, "before_snapshot_path")) ||
            !SamePath(value.QueuePath, transition.QueuePath) || !SamePath(value.ReceiptPath, transition.ExecutionReceiptPath) ||
            !SamePath(value.AfterSnapshotPath, transition.AfterSnapshotPath) || !SamePath(value.RuntimeDirectory, StoredPath(record, "runtime_directory")) ||
            !SamePath(value.ModelActionPath, StoredPath(record, "original_model_action_path")) ||
            transition.RunId != options.RunId || transition.ExecutorVersion != options.ExecutorVersion || transition.TransitionKind != "crop_actions" ||
            active is null || !active.SequenceEqual(ActiveCoveredCommitments(), StringComparer.Ordinal))
            throw new InvalidDataException("Original till changed its owner/run/executor/source or compilation roles.");
        var start = CropTeacherSiteTillCompilation.Open(plan.RequestPath, StoredPath(record, "before_snapshot_path"), headLedgerPath,
            active, value.CommitmentId, Text(record, "support_step_id"), value.ObservationPath,
            value.SiteRequestPath, value.SitePlanPath, value.OriginalModelActionPath, value.ModelActionPath, value.QueuePath);
        var expected = frontierHashes.TryGetValue(headSnapshotPath, out var hash) ? hash : fixedHashes[headSnapshotPath];
        if (start.InputHashes[start.SourcePath] != expected || requireNativeIdentity &&
            (transition.TotalDay != start.TotalDay || transition.SelectedCandidateId != start.CandidateId))
            throw new InvalidDataException("Original till does not continue its accepted source/candidate/day.");
        if (SamePath(transition.ExecutionReceiptPath, transition.AfterSnapshotPath) || start.InputHashes.Keys.Any(path =>
            SamePath(path, transition.ExecutionReceiptPath) || SamePath(path, transition.AfterSnapshotPath) || Within(value.RuntimeDirectory, path)))
            throw new IOException("Original till native output overlaps its retained inputs.");
        return start;
    }

    private JsonObject CreateOriginalTillingIntent(JsonObject record) => new()
    {
        ["schema_version"] = "crop_teacher_owned_till_execution.v1", ["run_id"] = options.RunId,
        ["executor_version"] = options.ExecutorVersion, ["backend_url"] = options.BackendUrl,
        ["runtime_directory"] = StoredPath(record, "runtime_directory"), ["support_step_id"] = Text(record, "support_step_id"),
        ["till_compilation"] = record["till_compilation"]!.DeepClone(),
        ["original_model_action_path"] = StoredPath(record, "original_model_action_path"),
        ["active_commitment_ids"] = record["active_commitment_ids"]!.DeepClone(),
        ["till_transition"] = record["till_transition"]!.DeepClone(),
        ["input_artifact_hashes"] = record["input_artifact_hashes"]!.DeepClone(),
        ["native_crop_lifecycle_verified"] = false, ["teacher_preference_label_eligible"] = false,
        ["formal_product_training_authorized"] = false
    };

    private static IEnumerable<string> TillingPaths(CropTeacherOriginalTillingCompilation value) => new[]
    { value.ObservationPath, value.SiteRequestPath, value.SitePlanPath, value.OriginalModelActionPath,
        value.ModelActionPath, value.QueuePath, value.ReceiptPath, value.AfterSnapshotPath, value.RuntimeDirectory };
}
