using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Core.Infrastructure;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.LiveTrainingLoop;

// A selected terminal obligation, not a deposit cursor, receipt or revenue credit.
internal sealed partial class CropTeacherTerminalShippingSelection
{
    private readonly CropSeedRuntimeArtifacts inputs;
    private readonly string saveId;
    private readonly string playerId;
    private readonly string gameVersion;
    private readonly int totalDay;
    private readonly int initialQuantity;
    private readonly string binScope;
    private readonly string binPlayer;
    private readonly CropTeacherTerminalVariantPlan? nativeVariants;
    internal string RetentionProofPath { get; }
    internal string QualifiedItemId { get; }
    internal int MinimumUnits { get; }
    internal long MinimumGrossRevenue { get; }
    internal int SlotIndex { get; }
    internal int Quality { get; }
    internal int UnitPrice { get; }
    internal int BinX { get; }
    internal int BinY { get; }
    internal int StandX { get; }
    internal int StandY { get; }
    internal IReadOnlyDictionary<string, string> InputHashes => inputs.Hashes;
    internal CropTeacherTerminalProductionReference ProductionReference { get; }
    internal void ValidateInputsUnchanged() => inputs.Validate();
    internal CropTeacherTerminalShippingTarget CreateTarget(int acceptedDepositCount) => new(
        RetentionProofPath, inputs.Hashes[RetentionProofPath], saveId, playerId, gameVersion, totalDay,
        QualifiedItemId, SlotIndex, Quality, UnitPrice, BinX, BinY, StandX, StandY, initialQuantity,
        MinimumUnits, acceptedDepositCount) { NativeVariants = nativeVariants };

    internal CropTeacherTerminalShippingSelection(CropTeacherManagedProductionEvidence evidence,
        string nativePlanPath, string snapshotPath, string ledgerPath) : this(evidence, nativePlanPath, snapshotPath, ledgerPath, false) { }

    internal static CropTeacherTerminalShippingSelection OpenNativeVariants(CropTeacherManagedProductionEvidence evidence,
        string nativePlanPath, string snapshotPath, string ledgerPath) => new(evidence, nativePlanPath, snapshotPath, ledgerPath, true);

    private CropTeacherTerminalShippingSelection(CropTeacherManagedProductionEvidence evidence,
        string nativePlanPath, string snapshotPath, string ledgerPath, bool useNativeVariants)
    {
        if (string.IsNullOrWhiteSpace(evidence.ManifestSha256))
            throw new InvalidDataException("Selected crop terminal requires its just-built production manifest hash.");
        inputs = new(new Dictionary<string, string> { [Path.GetFullPath(evidence.ManifestPath)] = evidence.ManifestSha256 });
        var manifest = inputs.Read<JsonObject>(evidence.ManifestPath);
        if (manifest["schema_version"]?.GetValue<string>() != "crop_teacher_managed_production_evidence.v1" ||
            manifest["native_crop_lifecycle_verified"]?.GetValue<bool>() != true ||
            manifest["whole_wave_output_retention_verified"]?.GetValue<bool>() != true ||
            manifest["terminal_settlement_verified"]?.GetValue<bool>() != false ||
            manifest["formal_product_training_authorized"]?.GetValue<bool>() != false ||
            !SamePath(Text(manifest, "native_run_plan_path"), nativePlanPath) ||
            !SamePath(Text(manifest, "accepted_snapshot_path"), snapshotPath) ||
            !SamePath(Text(manifest, "accepted_ledger_path"), ledgerPath))
            throw new InvalidDataException("Selected crop terminal does not continue its original owner production boundary.");
        var retained = manifest["input_artifact_hashes"]!.Deserialize<Dictionary<string, string>>(CropTeacherManagedSeedOwner.JsonOptions)!;
        foreach (var (path, hash) in retained)
            if (inputs.Pin(path) != hash) throw new InvalidDataException("Selected crop terminal production input changed.");
        foreach (var path in new[] { nativePlanPath, snapshotPath, ledgerPath })
            if (!retained.Keys.Any(key => SamePath(key, path)))
                throw new InvalidDataException("Selected crop terminal input is outside the production proof.");
        var proofPaths = manifest["output_retention_proof_paths"]!.Deserialize<string[]>()!;
        if (!proofPaths.Select(Path.GetFullPath).SequenceEqual(evidence.OutputRetentionProofPaths.Select(Path.GetFullPath), PathComparer) ||
            proofPaths.Any(path => !retained.Keys.Any(key => SamePath(key, path))))
            throw new InvalidDataException("Selected crop terminal retention proof set changed.");
        var plan = inputs.Read<MoneyRevenueCropNativeProductionRunPlan>(nativePlanPath);
        var source = inputs.Read<SnapshotEnvelope>(snapshotPath);
        var matches = proofPaths.Select(path => (Path: path, Proof: inputs.Read<MoneyRevenueCropOutputRetentionProof>(path)))
            .Where(row => SamePath(row.Proof.AfterSnapshotPath, snapshotPath) && row.Proof.AfterStateHash == source.StateHash).ToArray();
        if (matches is not [var selected] || !selected.Proof.NativeOutputRetentionVerified ||
            selected.Proof.FormalProductTrainingAuthorized || selected.Proof.MinimumUnits <= 0 || selected.Proof.MinimumGrossRevenue <= 0 ||
            selected.Proof.AfterSnapshotSha256 != inputs.Hashes[Path.GetFullPath(snapshotPath)] ||
            !SamePath(selected.Proof.CropProductionPlanPath, plan.CropProductionPlanPath) ||
            selected.Proof.CropProductionPlanSha256 != plan.CropProductionPlanSha256 ||
            source.StateHash != SnapshotHash.ComputeStateHash(source.State))
            throw new InvalidDataException("Selected crop terminal lacks one exact retained wave at its accepted head.");
        RetentionProofPath = Path.GetFullPath(selected.Path);
        QualifiedItemId = selected.Proof.HarvestItemQualifiedId;
        MinimumUnits = selected.Proof.MinimumUnits; MinimumGrossRevenue = selected.Proof.MinimumGrossRevenue;
        saveId = source.SaveId.Value!; playerId = source.PlayerId.Value!; gameVersion = source.GameVersion;
        totalDay = Number(Field(source, "time", "total_days"));
        if (source.SaveId.Status != FieldStatus.Available || source.PlayerId.Status != FieldStatus.Available ||
            string.IsNullOrWhiteSpace(saveId) || string.IsNullOrWhiteSpace(playerId) || totalDay != selected.Proof.HarvestTotalDay ||
            Field(source, "player", "location_id").GetString() != "Farm")
            throw new InvalidDataException("Selected crop terminal source actor/day/location is unavailable.");
        var stacks = Field(source, "player", "inventory").EnumerateArray().Where(row =>
            String(row, "qualified_item_id") == QualifiedItemId && Integer(row, "stack") >= (useNativeVariants ? 1 : MinimumUnits) &&
            Integer(row, "sell_to_store_price") > 0 && Boolean(row, "can_be_shipped")).OrderBy(row => Integer(row, "slot_index")).ToArray();
        if (stacks.Length == 0 || !useNativeVariants && stacks.Length != 1)
            throw new InvalidDataException("Selected crop terminal requires the original exact shippable retained stack.");
        var stack = stacks[0];
        SlotIndex = Integer(stack, "slot_index"); Quality = Integer(stack, "quality");
        initialQuantity = Integer(stack, "stack"); UnitPrice = Integer(stack, "sell_to_store_price");
        if (SlotIndex < 0 || Quality < 0 || (!useNativeVariants && checked((long)MinimumUnits * UnitPrice) < MinimumGrossRevenue))
            throw new InvalidDataException("Selected crop terminal quantity/price does not meet its original wave.");
        var bins = Field(source, "farm", "shipping_bins").EnumerateArray().Where(row => Boolean(row, "completed") &&
            Boolean(row, "pending_settlement_value_complete") && Long(row, "pending_settlement_value") == 0 &&
            row.TryGetProperty("interaction_stand_tile_x", out var x) && x.TryGetInt32(out _) &&
            row.TryGetProperty("interaction_stand_tile_y", out var y) && y.TryGetInt32(out _) &&
            string.IsNullOrWhiteSpace(String(row, "interaction_stand_tile_blocked_reason"))).ToArray();
        if (bins is not [var bin]) throw new InvalidDataException("Selected crop terminal requires the original exact empty completed shipping bin.");
        BinX = Integer(bin, "tile_x"); BinY = Integer(bin, "tile_y");
        StandX = Integer(bin, "interaction_stand_tile_x"); StandY = Integer(bin, "interaction_stand_tile_y");
        binScope = String(bin, "bin_scope"); binPlayer = bin.GetProperty("player_id").ToString();
        if (new[] { BinX, BinY, StandX, StandY }.Any(value => value < 0) || string.IsNullOrWhiteSpace(binScope) || binPlayer != playerId)
            throw new InvalidDataException("Selected crop terminal bin identity is unavailable.");
        if (useNativeVariants)
        {
            CropTeacherTerminalVariant? next = null;
            foreach (var row in stacks.Reverse()) next = new(Integer(row, "slot_index"), Integer(row, "quality"),
                Integer(row, "sell_to_store_price"), Integer(row, "stack"), next);
            nativeVariants = new("crop_teacher_native_terminal_variants.v1", binScope, next!);
            CreateTarget(0).VerifyNativeVariantSource(source, 0, MinimumGrossRevenue);
        }
        inputs.Validate();
        // The full trajectory was checked once above. Per-action binding keeps only its terminal certificate and source.
        var terminalPaths = new[] { evidence.ManifestPath, nativePlanPath, snapshotPath, ledgerPath, RetentionProofPath }
            .Select(Path.GetFullPath).ToHashSet(PathComparer);
        inputs = new(inputs.Hashes.Where(row => terminalPaths.Contains(row.Key)).ToDictionary(row => row.Key, row => row.Value, PathComparer));
        ProductionReference = new(Path.GetFullPath(evidence.ManifestPath), evidence.ManifestSha256);
    }

    private static JsonElement Field(SnapshotEnvelope source, string domain, string name)
    {
        if (!source.State.TryGetValue(domain, out var fields) || !fields.TryGetProperty(name, out var field) ||
            !field.TryGetProperty("status", out var status) || !FieldEnvelopeValidator.IsReadableStatus(status.GetString() ?? "") ||
            !field.TryGetProperty("value", out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            throw new InvalidDataException("Selected crop terminal source field unavailable: " + domain + "." + name);
        return value;
    }
    private static int Number(JsonElement value) => value.TryGetInt32(out var number) ? number :
        throw new InvalidDataException("Selected crop terminal integer unavailable.");
    private static int Integer(JsonElement row, string name) => row.TryGetProperty(name, out var value) ? Number(value) : -1;
    private static long Long(JsonElement row, string name) => row.TryGetProperty(name, out var value) && value.TryGetInt64(out var number) ? number : -1;
    private static bool Boolean(JsonElement row, string name) => row.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;
    private static string String(JsonElement row, string name) => row.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString()! : "";
    private static string Text(JsonObject row, string name) => row[name]?.GetValue<string>() ?? throw new InvalidDataException("Selected crop terminal identity unavailable.");
    private static StringComparer PathComparer => OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
    private static bool SamePath(string first, string second) => PathComparer.Equals(Path.GetFullPath(first), Path.GetFullPath(second));
}
