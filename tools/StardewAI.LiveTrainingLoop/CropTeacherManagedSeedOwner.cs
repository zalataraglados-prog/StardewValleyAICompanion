using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Infrastructure;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.LiveTrainingLoop;

public sealed record CropTeacherManagedSeedOwnerOptions(string EvidenceDirectory, string RunId, string ExecutorVersion,
    string BackendUrl, string NativeRunPlanPath, string InitialSnapshotPath, string InitialLedgerPath,
    string PortfolioJournalDirectory) : ICropSeedPortfolioRecoveryOptions
{
    public bool UseSelectedRefillResourceEffects { get; init; }
    public CropTeacherNativeSaveBinding? NativeSaveBinding { get; init; }
}

public sealed record CropTeacherNativeSaveBinding(string SaveIsolationPath, string SaveSlot);

public sealed class CropTeacherManagedSeedOperation
{
    internal CropTeacherManagedSeedOperation(CropTeacherManagedSeedOwner owner, JsonObject record,
        string recordPath, CropTeacherManagedSeedScopes? scopes, CropTeacherSeedLifecycleSupport? lifecycle = null,
        CropTeacherSupportingRoute? supportingRoute = null, CropTeacherSupportingRoute.Preparation? routeStart = null,
        CropTeacherWateringMaintenance? watering = null, CropTeacherWateringMaintenance.Preparation? wateringStart = null,
        CropTeacherHarvest.Preparation? harvestStart = null, CropTeacherOvernight.Preparation? overnightStart = null,
        CropTeacherControlRecovery.Preparation? controlStart = null, CropTeacherTerminalShipping.Preparation? terminalStart = null,
        CropTeacherNativeSavedContentReadback? savedReadback = null)
    {
        Owner = owner; Record = record; RecordPath = recordPath; seedScopes = scopes; Lifecycle = lifecycle;
        SupportingRoute = supportingRoute; RouteStart = routeStart; Watering = watering; WateringStart = wateringStart;
        HarvestStart = harvestStart; OvernightStart = overnightStart; ControlStart = controlStart; TerminalStart = terminalStart;
        SavedReadback = savedReadback;
    }
    private readonly CropTeacherManagedSeedScopes? seedScopes;
    internal CropTeacherSeedLifecycleSupport? Lifecycle { get; }
    internal CropTeacherSupportingRoute? SupportingRoute { get; set; }
    internal CropTeacherSupportingRoute.Preparation? RouteStart { get; }
    internal CropTeacherWateringMaintenance? Watering { get; set; }
    internal CropTeacherWateringMaintenance.Preparation? WateringStart { get; }
    internal CropTeacherHarvest.Preparation? HarvestStart { get; }
    internal CropTeacherHarvest? Harvest { get; set; }
    internal CropTeacherOvernight.Preparation? OvernightStart { get; }
    internal CropTeacherOvernight? Overnight { get; set; }
    internal CropTeacherControlRecovery.Preparation? ControlStart { get; }
    internal CropTeacherControlRecovery? Control { get; set; }
    internal CropTeacherTerminalShipping.Preparation? TerminalStart { get; }
    internal CropTeacherTerminalShipping? Terminal { get; set; }
    internal CropTeacherNativeSavedContentReadback? SavedReadback { get; }
    internal ICropTeacherSupportingPreparation? OriginalStart => (ICropTeacherSupportingPreparation?)RouteStart ??
        (ICropTeacherSupportingPreparation?)WateringStart ?? (ICropTeacherSupportingPreparation?)HarvestStart ??
        (ICropTeacherSupportingPreparation?)OvernightStart ?? (ICropTeacherSupportingPreparation?)ControlStart ?? TerminalStart;
    internal ICropTeacherSupportingTransition? SupportingTransition => (ICropTeacherSupportingTransition?)SupportingRoute ??
        (ICropTeacherSupportingTransition?)Watering ?? (ICropTeacherSupportingTransition?)Harvest ??
        (ICropTeacherSupportingTransition?)Overnight ?? (ICropTeacherSupportingTransition?)Control ??
        (ICropTeacherSupportingTransition?)Terminal ?? SavedReadback;
    internal CropTeacherManagedSeedOwner Owner { get; }
    internal JsonObject Record { get; }
    internal string RecordPath { get; }
    internal JsonObject? Prepared { get; set; }
    internal JsonObject? Started { get; set; }
    internal JsonObject? Dispatch { get; set; }
    internal JsonObject? Continuation { get; set; }
    internal ICropTeacherSeedRuntime? PreparedRuntime { get; set; }
    internal Action? RuntimeArtifactGuard { get; set; }
    internal bool ExecutionActive { get; set; }
    internal CropTeacherSeedNoDispatchBoundary? VerifiedStop { get; set; }
    internal bool StopAccepted { get; set; }
    public int Sequence => Record["sequence"]!.GetValue<int>();
    public string SupportStepId => Record["support_step_id"]!.GetValue<string>();
    public string RuntimeDirectory => Record["runtime_directory"]!.GetValue<string>();
    public bool IsLifecycle => Lifecycle is not null;
    public bool IsSupportingRoute => SupportingRoute is not null || RouteStart is not null;
    public bool IsWateringMaintenance => Watering is not null || WateringStart is not null;
    public bool IsHarvest => HarvestStart is not null || Harvest is not null;
    public bool IsOvernight => OvernightStart is not null || Overnight is not null;
    public bool IsControlRecovery => ControlStart is not null || Control is not null;
    public bool IsTerminalShipping => TerminalStart is not null || Terminal is not null;
    public bool IsSavedContentReadback => SavedReadback is not null;
    public bool IsSupportingTransition => IsSupportingRoute || IsWateringMaintenance || IsHarvest || IsOvernight || IsControlRecovery || IsTerminalShipping || IsSavedContentReadback;
    public CropTeacherManagedSeedScopes Scopes => seedScopes ?? throw new InvalidOperationException("Lifecycle operations have no native seed scope.");
    public CropTeacherRunStep ReadStep() => IsLifecycle || IsSupportingTransition ? throw new InvalidOperationException("Non-seed operations have no native seed step.") :
        Record["step"]!.Deserialize<CropTeacherRunStep>(CropTeacherManagedSeedOwner.JsonOptions)!;
}

public sealed partial class CropTeacherManagedSeedOwner : IAsyncDisposable
{
    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly CropTeacherManagedSeedOwnerOptions options;
    private readonly MoneyRevenueCropNativeProductionRunPlan plan;
    private readonly FileStream lease;
    private readonly Func<string, string, Task> write;
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly Dictionary<string, string> fixedHashes;
    private readonly Dictionary<string, string> frontierHashes = new(PathComparer);
    private readonly string headerHash;
    private readonly string saveId;
    private readonly string playerId;
    private string headSnapshotPath;
    private string headLedgerPath;
    private string lastAcceptedHash;
    private string? lastAcceptedPath;
    private CropTeacherManagedSeedOperation? lastAcceptedOperation;
    private readonly List<(int Sequence, string Path, string Hash)> acceptedHistory = [];
    private CropTeacherTerminalShippingTarget? terminalTarget;
    public int AcceptedTerminalDepositCount => terminalTarget?.AcceptedDepositCount ?? 0;
    private Action? liveHeadGuard;
    private bool disposed;
    private bool faulted;
    public CropTeacherManagedSeedOperation? Pending { get; private set; }
    public bool HeadRecoveryRequired { get; private set; }
    public string CurrentSnapshotPath => headSnapshotPath;
    public string CurrentLedgerPath => headLedgerPath;
    internal string EvidenceDirectory => options.EvidenceDirectory;
    internal bool UsesSelectedRefillResourceEffects => options.UseSelectedRefillResourceEffects;

    private CropTeacherManagedSeedOwner(CropTeacherManagedSeedOwnerOptions options, MoneyRevenueCropNativeProductionRunPlan plan,
        FileStream lease, Func<string, string, Task> write, Dictionary<string, string> fixedHashes, string headerHash,
        string saveId, string playerId)
    {
        this.options = options; this.plan = plan; this.lease = lease; this.write = write;
        this.fixedHashes = fixedHashes; this.headerHash = headerHash;
        this.saveId = saveId; this.playerId = playerId;
        headSnapshotPath = options.InitialSnapshotPath; headLedgerPath = options.InitialLedgerPath; lastAcceptedHash = headerHash;
    }

    public static async Task<CropTeacherManagedSeedOwner> OpenAsync(CropTeacherManagedSeedOwnerOptions value,
        Func<string, string, Task> write)
    {
        if (string.IsNullOrWhiteSpace(value.RunId) || string.IsNullOrWhiteSpace(value.ExecutorVersion))
            throw new ArgumentException("Managed crop owner requires original run and executor identity.");
        var options = value with
        {
            EvidenceDirectory = Path.GetFullPath(value.EvidenceDirectory), BackendUrl = CropTeacherSeedLifecycleRuntime.NormalizeBackendOrigin(value.BackendUrl),
            NativeRunPlanPath = Path.GetFullPath(value.NativeRunPlanPath), InitialSnapshotPath = Path.GetFullPath(value.InitialSnapshotPath),
            InitialLedgerPath = Path.GetFullPath(value.InitialLedgerPath), PortfolioJournalDirectory = Path.GetFullPath(value.PortfolioJournalDirectory),
            NativeSaveBinding = NormalizeNativeSaveBinding(value.NativeSaveBinding)
        };
        if (SamePath(options.EvidenceDirectory, Path.GetPathRoot(options.EvidenceDirectory)!))
            throw new IOException("Managed crop owner cannot own a drive root.");
        if (options.NativeSaveBinding is { } nativeSave)
            RequireOutsideNativeSaveSlot(new[] { options.EvidenceDirectory, options.PortfolioJournalDirectory }, nativeSave);
        var hashes = new[] { options.NativeRunPlanPath, options.InitialSnapshotPath, options.InitialLedgerPath }
            .Distinct(PathComparer).ToDictionary(path => path, HashFile, PathComparer);
        var (_, plan) = CropTeacherResumeLoader.ReadVerifiedPlan(options.NativeRunPlanPath);
        var snapshot = Read<SnapshotEnvelope>(options.InitialSnapshotPath);
        var ledger = Read<StrategyCommitmentLedger>(options.InitialLedgerPath);
        if (plan.SourceStateHash != snapshot.StateHash)
            throw new InvalidDataException("Managed crop owner initial source differs from its original plan.");
        CropTeacherOwnedSeedSupply.Read(snapshot, ledger, plan.CoveredCommitmentIds);
        var paths = new[] { options.NativeRunPlanPath, plan.RequestPath, plan.CropProductionPlanPath,
            options.InitialSnapshotPath, options.InitialLedgerPath }.Select(Path.GetFullPath).Distinct(PathComparer).ToArray();
        if (paths.Any(path => Within(options.EvidenceDirectory, path)))
            throw new IOException("Managed crop owner output overlaps retained input files.");
        hashes[Path.GetFullPath(plan.RequestPath)] = plan.RequestSha256;
        hashes[Path.GetFullPath(plan.CropProductionPlanPath)] = plan.CropProductionPlanSha256;
        foreach (var (path, hash) in hashes)
            if (HashFile(path) != hash) throw new InvalidDataException("Managed crop owner inputs changed during original plan validation.");
        var header = new JsonObject
        {
            ["schema_version"] = "crop_teacher_managed_seed_owner.v1", ["run_id"] = options.RunId,
            ["executor_version"] = options.ExecutorVersion, ["backend_url"] = options.BackendUrl,
            ["portfolio_journal_directory"] = options.PortfolioJournalDirectory,
            ["native_run_plan_path"] = options.NativeRunPlanPath, ["run_request_path"] = plan.RequestPath,
            ["initial_snapshot_path"] = options.InitialSnapshotPath, ["initial_ledger_path"] = options.InitialLedgerPath,
            ["save_id"] = snapshot.SaveId.Value, ["player_id"] = snapshot.PlayerId.Value,
            ["input_artifact_hashes"] = JsonSerializer.SerializeToNode(hashes, JsonOptions),
            ["execution_authorized"] = false, ["formal_product_training_authorized"] = false
        };
        if (options.UseSelectedRefillResourceEffects)
            header["selected_refill_resource_effects"] = true;
        if (options.NativeSaveBinding is { } binding)
            header["native_save_binding"] = JsonSerializer.SerializeToNode(binding, JsonOptions);
        var headerPath = Path.Combine(options.EvidenceDirectory, "owner-intent.json");
        if (Directory.Exists(options.EvidenceDirectory) && !File.Exists(headerPath) &&
            Directory.EnumerateFileSystemEntries(options.EvidenceDirectory).Any(path => Path.GetFileName(path) != "owner.lock"))
            throw new IOException("Managed crop owner directory has unrelated or incomplete contents.");
        RequireDirectoryPath(options.EvidenceDirectory);
        Directory.CreateDirectory(options.EvidenceDirectory);
        Regular(options.EvidenceDirectory);
        var lockPath = Path.Combine(options.EvidenceDirectory, "owner.lock");
        if (File.Exists(lockPath)) Regular(lockPath);
        var lease = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        try
        {
            if (File.Exists(headerPath))
            {
                if (!JsonNode.DeepEquals(header, Read<JsonObject>(headerPath)))
                    throw new InvalidDataException("Managed crop owner does not match its retained original identity.");
            }
            else await write(headerPath, header.ToJsonString(JsonOptions)).ConfigureAwait(false);
            var owner = new CropTeacherManagedSeedOwner(options, plan, lease, write, hashes, HashFile(headerPath), ledger.SaveId, ledger.PlayerId);
            owner.LoadIndex();
            return owner;
        }
        catch { lease.Dispose(); throw; }
    }

    public async ValueTask DisposeAsync()
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try { if (!disposed) { disposed = true; lease.Dispose(); } }
        finally { gate.Release(); }
    }
}
