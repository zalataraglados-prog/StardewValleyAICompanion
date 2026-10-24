using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace StardewAI.LiveTrainingLoop;

public sealed record CropTeacherHarvestReceipt(
    [property: JsonPropertyName("harvest_tile_key")] string HarvestTileKey,
    [property: JsonPropertyName("commitment_id")] string CommitmentId,
    [property: JsonPropertyName("total_day")] int TotalDay,
    [property: JsonPropertyName("location_id")] string LocationId,
    [property: JsonPropertyName("tile_x")] int TileX,
    [property: JsonPropertyName("tile_y")] int TileY,
    [property: JsonPropertyName("queue_id")] string QueueId,
    [property: JsonPropertyName("queue_item_id")] string QueueItemId,
    [property: JsonPropertyName("before_state_hash")] string BeforeStateHash,
    [property: JsonPropertyName("after_state_hash")] string AfterStateHash,
    [property: JsonPropertyName("primitive_verification_status")]
    string PrimitiveVerificationStatus,
    [property: JsonPropertyName("observed_effect")] string ObservedEffect,
    [property: JsonPropertyName("recorded_at_utc")] string RecordedAtUtc);

public sealed record CropTeacherDayBoundaryReceipt(
    [property: JsonPropertyName("day_boundary_key")] string DayBoundaryKey,
    [property: JsonPropertyName("from_total_day")] int FromTotalDay,
    [property: JsonPropertyName("to_total_day")] int ToTotalDay,
    [property: JsonPropertyName("from_day_key")] string FromDayKey,
    [property: JsonPropertyName("to_day_key")] string ToDayKey,
    [property: JsonPropertyName("queue_id")] string QueueId,
    [property: JsonPropertyName("queue_item_id")] string QueueItemId,
    [property: JsonPropertyName("before_state_hash")] string BeforeStateHash,
    [property: JsonPropertyName("after_state_hash")] string AfterStateHash,
    [property: JsonPropertyName("before_save_sha256")]
    string BeforeSaveSha256,
    [property: JsonPropertyName("after_save_sha256")]
    string AfterSaveSha256,
    [property: JsonPropertyName("recorded_at_utc")] string RecordedAtUtc);

public sealed class CropTeacherTransitionLedger
{
    private const string CurrentSchemaVersion =
        "crop_teacher_transition_ledger.v1";
    private static readonly JsonSerializerOptions JsonOptions = new(
        JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } = CurrentSchemaVersion;

    [JsonPropertyName("run_plan_path")]
    public string RunPlanPath { get; set; } = string.Empty;

    [JsonPropertyName("run_plan_sha256")]
    public string RunPlanSha256 { get; set; } = string.Empty;

    [JsonPropertyName("harvest_receipts")]
    public List<CropTeacherHarvestReceipt> HarvestReceipts { get; set; } = [];

    [JsonPropertyName("day_boundary_receipts")]
    public List<CropTeacherDayBoundaryReceipt> DayBoundaryReceipts
        { get; set; } = [];

    [JsonIgnore]
    public string LedgerPath { get; private set; } = string.Empty;

    [JsonIgnore]
    public IReadOnlySet<string> CompletedHarvestTileKeys =>
        HarvestReceipts.Select(receipt => receipt.HarvestTileKey)
            .ToHashSet(StringComparer.Ordinal);

    [JsonIgnore]
    public IReadOnlySet<string> CompletedDayBoundaryKeys =>
        DayBoundaryReceipts.Select(receipt => receipt.DayBoundaryKey)
            .ToHashSet(StringComparer.Ordinal);

    public static CropTeacherTransitionLedger Open(
        string ledgerPath,
        string runPlanPath)
    {
        var fullLedgerPath = Path.GetFullPath(Required(ledgerPath, "ledger"));
        var fullRunPlanPath = Path.GetFullPath(Required(runPlanPath, "run plan"));
        if (!File.Exists(fullRunPlanPath))
        {
            throw new FileNotFoundException(
                "crop_teacher_run_plan_missing",
                fullRunPlanPath);
        }

        var runPlanSha256 = HashFile(fullRunPlanPath);
        CropTeacherTransitionLedger ledger;
        if (File.Exists(fullLedgerPath))
        {
            ledger = JsonSerializer.Deserialize<CropTeacherTransitionLedger>(
                    File.ReadAllText(fullLedgerPath, Encoding.UTF8),
                    JsonOptions) ?? throw new InvalidDataException(
                        "crop_teacher_transition_ledger_invalid");
            if (ledger.SchemaVersion != CurrentSchemaVersion ||
                !string.Equals(
                    Path.GetFullPath(ledger.RunPlanPath),
                    fullRunPlanPath,
                    StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(
                    ledger.RunPlanSha256,
                    runPlanSha256,
                    StringComparison.Ordinal) ||
                ledger.HarvestReceipts is null ||
                ledger.DayBoundaryReceipts is null ||
                ledger.HarvestReceipts.Select(receipt => receipt.HarvestTileKey)
                    .Distinct(StringComparer.Ordinal).Count() !=
                    ledger.HarvestReceipts.Count ||
                ledger.DayBoundaryReceipts.Select(receipt =>
                        receipt.DayBoundaryKey)
                    .Distinct(StringComparer.Ordinal).Count() !=
                    ledger.DayBoundaryReceipts.Count)
            {
                throw new InvalidDataException(
                    "crop_teacher_transition_ledger_binding_invalid");
            }
        }
        else
        {
            ledger = new CropTeacherTransitionLedger
            {
                RunPlanPath = fullRunPlanPath,
                RunPlanSha256 = runPlanSha256
            };
        }

        ledger.LedgerPath = fullLedgerPath;
        return ledger;
    }

    public int RecordVerifiedHarvestResults(
        JsonObject execution,
        CropTeacherRunStep step,
        string recordedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(execution);
        ArgumentNullException.ThrowIfNull(step);
        var batch = step.ActionBatch;
        if (step.Stage != CropTeacherRunStageKinds.ExecuteCropBatch ||
            batch is null ||
            batch.OptionId != "executor.harvest_crop")
        {
            return 0;
        }

        var results = execution["step_results"] is JsonArray stepResults
            ? stepResults.OfType<JsonObject>().ToArray()
            : new[] { execution };
        var expectedTiles = batch.Tiles
            .Select(tile => (tile.TileX, tile.TileY))
            .ToHashSet();
        var existingKeys = CompletedHarvestTileKeys.ToHashSet(
            StringComparer.Ordinal);
        var appended = 0;
        foreach (var result in results)
        {
            if (ReadString(result, "option_id") != "executor.harvest_crop" ||
                ReadString(result, "status") != "applied" ||
                ReadString(result, "primitive_verification_status") != "verified" ||
                result["feedback_available"]?.GetValue<bool>() != true)
            {
                continue;
            }

            var tileX = ReadBoundRequiredInt(result, "target_tile_x");
            var tileY = ReadBoundRequiredInt(result, "target_tile_y");
            if (!expectedTiles.Contains((tileX, tileY)) ||
                ReadBoundString(result, "target_location") != batch.LocationId ||
                ReadActionCommitmentId(result) != batch.CommitmentId)
            {
                throw new InvalidDataException(
                    "crop_teacher_harvest_execution_binding_invalid");
            }

            var key = HarvestTileKey(
                batch.CommitmentId,
                step.TotalDay,
                tileX,
                tileY);
            if (!existingKeys.Add(key))
            {
                continue;
            }
            var beforeStateHash = ReadString(result, "before_state_hash");
            var afterStateHash = ReadString(result, "after_state_hash");
            var queueId = ReadString(result, "queue_id");
            var queueItemId = ReadString(result, "queue_item_id");
            if (beforeStateHash.Length == 0 ||
                afterStateHash.Length == 0 ||
                queueId.Length == 0 ||
                queueItemId.Length == 0)
            {
                throw new InvalidDataException(
                    "crop_teacher_harvest_execution_receipt_incomplete");
            }
            HarvestReceipts.Add(new CropTeacherHarvestReceipt(
                key,
                batch.CommitmentId,
                step.TotalDay,
                batch.LocationId,
                tileX,
                tileY,
                queueId,
                queueItemId,
                beforeStateHash,
                afterStateHash,
                "verified",
                ReadString(result, "observed_effect"),
                Required(recordedAtUtc, "recorded timestamp")));
            appended++;
        }

        if (appended > 0)
        {
            Persist();
        }
        return appended;
    }

    public bool RecordVerifiedDayBoundary(
        CropTeacherDayBoundaryReceipt receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        var expectedKey = DayBoundaryKey(receipt.FromTotalDay);
        if (receipt.DayBoundaryKey != expectedKey ||
            receipt.ToTotalDay != receipt.FromTotalDay + 1 ||
            string.IsNullOrWhiteSpace(receipt.FromDayKey) ||
            string.IsNullOrWhiteSpace(receipt.ToDayKey) ||
            receipt.FromDayKey == receipt.ToDayKey ||
            string.IsNullOrWhiteSpace(receipt.QueueId) ||
            string.IsNullOrWhiteSpace(receipt.QueueItemId) ||
            string.IsNullOrWhiteSpace(receipt.BeforeStateHash) ||
            string.IsNullOrWhiteSpace(receipt.AfterStateHash) ||
            receipt.BeforeStateHash == receipt.AfterStateHash ||
            string.IsNullOrWhiteSpace(receipt.BeforeSaveSha256) ||
            string.IsNullOrWhiteSpace(receipt.AfterSaveSha256) ||
            receipt.BeforeSaveSha256 == receipt.AfterSaveSha256 ||
            string.IsNullOrWhiteSpace(receipt.RecordedAtUtc))
        {
            throw new InvalidDataException(
                "crop_teacher_day_boundary_receipt_invalid");
        }
        if (CompletedDayBoundaryKeys.Contains(expectedKey))
        {
            return false;
        }
        DayBoundaryReceipts.Add(receipt);
        Persist();
        return true;
    }

    public static string HarvestTileKey(
        string commitmentId,
        int totalDay,
        int tileX,
        int tileY)
    {
        if (string.IsNullOrWhiteSpace(commitmentId) ||
            totalDay < 0 ||
            tileX < 0 ||
            tileY < 0)
        {
            throw new ArgumentException(
                "crop_teacher_harvest_tile_key_invalid");
        }
        return $"{commitmentId}|day={totalDay}|tile={tileX},{tileY}";
    }

    public static string DayBoundaryKey(int fromTotalDay)
    {
        if (fromTotalDay < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(fromTotalDay));
        }
        return $"day={fromTotalDay}->day={fromTotalDay + 1}";
    }

    private void Persist()
    {
        if (LedgerPath.Length == 0)
        {
            throw new InvalidOperationException(
                "crop_teacher_transition_ledger_path_missing");
        }
        Directory.CreateDirectory(Path.GetDirectoryName(LedgerPath)!);
        var temporaryPath = LedgerPath + ".tmp." + Guid.NewGuid().ToString("N");
        try
        {
            File.WriteAllText(
                temporaryPath,
                JsonSerializer.Serialize(this, JsonOptions),
                new UTF8Encoding(false));
            File.Move(temporaryPath, LedgerPath, true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static string ReadActionCommitmentId(JsonObject result)
        => ReadBoundString(result, "action_commitment_id");

    private static string ReadBoundString(
        JsonObject result,
        string property)
    {
        var direct = ReadString(result, property);
        if (!string.IsNullOrWhiteSpace(direct))
        {
            return direct;
        }

        return ReadCommandParameters(result)
            .FirstOrDefault(parameter =>
                ReadString(parameter, "name") == property)?
            ["value"]?.GetValue<string>() ?? string.Empty;
    }

    private static int ReadBoundRequiredInt(
        JsonObject result,
        string property)
    {
        if (result[property] is JsonValue direct &&
            direct.TryGetValue<int>(out var directValue))
        {
            return directValue;
        }

        var parameterValue = ReadBoundString(result, property);
        if (int.TryParse(
                parameterValue,
                System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture,
                out var parsed))
        {
            return parsed;
        }

        throw new InvalidDataException(
            "crop_teacher_harvest_execution_" + property + "_missing");
    }

    private static IEnumerable<JsonObject> ReadCommandParameters(
        JsonObject result) =>
        (result["effective_queue_item"]?
            ["normalized_command"]?["parameters"] as JsonArray)?
        .OfType<JsonObject>() ?? [];

    private static string ReadString(JsonObject value, string property) =>
        value[property]?.GetValue<string>() ?? string.Empty;

    private static string Required(string value, string label) =>
        !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new ArgumentException(
                "Crop teacher " + label + " is required.");

    private static string HashFile(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }
}
