using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Strategy;

public interface IStrategyCommitmentRepository
{
    StrategyCommitmentLedger Get(SnapshotEnvelope snapshot);

    StrategyCommitmentMutationResult Upsert(SnapshotEnvelope snapshot, CropPlantingCommitmentUpsertRequest request);

    StrategyCommitmentMutationResult Cancel(SnapshotEnvelope snapshot, string commitmentId, StrategyCommitmentCancelRequest request);

    StrategyCommitmentMutationResult UpsertMaterial(
        SnapshotEnvelope snapshot,
        MaterialReservationUpsertRequest request);

    StrategyCommitmentMutationResult CancelMaterial(
        SnapshotEnvelope snapshot,
        string reservationId,
        StrategyCommitmentCancelRequest request);
}

public sealed class FileStrategyCommitmentRepository : IStrategyCommitmentRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };

    private readonly object sync = new();
    private readonly string root;
    private readonly CropCommitmentLedgerService service = new();
    private readonly MaterialReservationLedgerService materialService = new();
    private readonly Dictionary<string, StrategyCommitmentLedger> cache = new(StringComparer.Ordinal);

    public FileStrategyCommitmentRepository()
        : this(ResolveDefaultRoot())
    {
    }

    public FileStrategyCommitmentRepository(string root)
    {
        this.root = Path.GetFullPath(root);
    }

    public StrategyCommitmentLedger Get(SnapshotEnvelope snapshot)
    {
        lock (sync)
        {
            var key = IdentityKey(snapshot);
            var current = Load(key, snapshot);
            var reconciled = service.ReconcileCompleted(current, snapshot, DateTimeOffset.UtcNow.ToString("O"));
            if (reconciled.Revision != current.Revision)
            {
                Save(key, reconciled);
            }
            return reconciled;
        }
    }

    public StrategyCommitmentMutationResult Upsert(SnapshotEnvelope snapshot, CropPlantingCommitmentUpsertRequest request)
    {
        lock (sync)
        {
            var key = IdentityKey(snapshot);
            var current = Load(key, snapshot);
            var result = service.Upsert(current, snapshot, request, DateTimeOffset.UtcNow.ToString("O"));
            if (result.Accepted && result.Ledger is not null)
            {
                Save(key, result.Ledger);
            }
            return result;
        }
    }

    public StrategyCommitmentMutationResult Cancel(SnapshotEnvelope snapshot, string commitmentId, StrategyCommitmentCancelRequest request)
    {
        lock (sync)
        {
            var key = IdentityKey(snapshot);
            var current = Load(key, snapshot);
            var result = service.Cancel(current, snapshot, commitmentId, request, DateTimeOffset.UtcNow.ToString("O"));
            if (result.Accepted && result.Ledger is not null)
            {
                Save(key, result.Ledger);
            }
            return result;
        }
    }

    public StrategyCommitmentMutationResult UpsertMaterial(
        SnapshotEnvelope snapshot,
        MaterialReservationUpsertRequest request)
    {
        lock (sync)
        {
            var key = IdentityKey(snapshot);
            var current = Load(key, snapshot);
            var result = materialService.Upsert(
                current,
                snapshot,
                request,
                DateTimeOffset.UtcNow.ToString("O"));
            if (result.Accepted && result.Ledger is not null)
            {
                Save(key, result.Ledger);
            }
            return result;
        }
    }

    public StrategyCommitmentMutationResult CancelMaterial(
        SnapshotEnvelope snapshot,
        string reservationId,
        StrategyCommitmentCancelRequest request)
    {
        lock (sync)
        {
            var key = IdentityKey(snapshot);
            var current = Load(key, snapshot);
            var result = materialService.Cancel(
                current,
                snapshot,
                reservationId,
                request,
                DateTimeOffset.UtcNow.ToString("O"));
            if (result.Accepted && result.Ledger is not null)
            {
                Save(key, result.Ledger);
            }
            return result;
        }
    }

    private StrategyCommitmentLedger Load(string key, SnapshotEnvelope snapshot)
    {
        if (cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var path = LedgerPath(key);
        StrategyCommitmentLedger ledger;
        if (File.Exists(path))
        {
            ledger = JsonSerializer.Deserialize<StrategyCommitmentLedger>(File.ReadAllText(path), JsonOptions)
                ?? NewLedger(snapshot);
            if (!string.Equals(ledger.SchemaVersion, "strategy_commitment_ledger.v1", StringComparison.Ordinal))
            {
                throw new InvalidDataException("persisted strategy ledger schema is unsupported");
            }
            if (!string.Equals(ledger.SaveId, SaveId(snapshot), StringComparison.Ordinal) ||
                !string.Equals(ledger.PlayerId, PlayerId(snapshot), StringComparison.Ordinal))
            {
                throw new InvalidDataException("persisted strategy ledger identity mismatch");
            }
            ledger.CropPlantingCommitments ??= Array.Empty<CropPlantingCommitment>();
            foreach (var commitment in ledger.CropPlantingCommitments)
            {
                commitment.HarvestContextTags ??= Array.Empty<string>();
            }
            ledger.MaterialReservations ??= Array.Empty<MaterialReservation>();
            ledger.History ??= Array.Empty<StrategyCommitmentHistoryEntry>();
        }
        else
        {
            ledger = NewLedger(snapshot);
        }
        cache[key] = ledger;
        return ledger;
    }

    private void Save(string key, StrategyCommitmentLedger ledger)
    {
        Directory.CreateDirectory(root);
        var path = LedgerPath(key);
        var temporaryPath = path + ".tmp." + Guid.NewGuid().ToString("N");
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(ledger, JsonOptions), new UTF8Encoding(false));
        File.Move(temporaryPath, path, true);
        cache[key] = ledger;
    }

    private string LedgerPath(string key) => Path.Combine(root, key + ".json");

    private static string IdentityKey(SnapshotEnvelope snapshot)
    {
        var identity = SaveId(snapshot) + "\n" + PlayerId(snapshot);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity))).ToLowerInvariant();
    }

    private static StrategyCommitmentLedger NewLedger(SnapshotEnvelope snapshot) => new()
    {
        LedgerId = "strategy-ledger:" + SaveId(snapshot) + ":" + PlayerId(snapshot),
        SaveId = SaveId(snapshot),
        PlayerId = PlayerId(snapshot),
        SourceStateHash = snapshot.StateHash,
        UpdatedAt = DateTimeOffset.UtcNow.ToString("O")
    };

    private static string SaveId(SnapshotEnvelope snapshot) => snapshot.SaveId.Value ?? string.Empty;

    private static string PlayerId(SnapshotEnvelope snapshot) => snapshot.PlayerId.Value ?? string.Empty;

    private static string ResolveDefaultRoot()
    {
        var configured = Environment.GetEnvironmentVariable("STARDEWAI_STRATEGY_LEDGER_DIR");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }
        const string isolatedRoot = @"E:\StardewAITraining\strategy-commitments";
        return Directory.Exists(Path.GetPathRoot(isolatedRoot))
            ? isolatedRoot
            : Path.Combine(AppContext.BaseDirectory, "strategy-commitments");
    }
}
