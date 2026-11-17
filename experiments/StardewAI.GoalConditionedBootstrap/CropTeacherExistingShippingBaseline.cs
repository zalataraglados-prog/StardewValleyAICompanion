using StardewAI.Contracts.State;

namespace StardewAI.GoalConditionedBootstrap;

// Bound to the original retained output, never income credited to this crop obligation.
public sealed record CropTeacherExistingShippingBaseline(string SchemaVersion, string SnapshotPath,
    string SnapshotSha256, long PendingValue, string ContentsSignature)
{
    public static CropTeacherExistingShippingBaseline Capture(CropTeacherTerminalShippingTarget target)
    {
        var proof = ReadRetention(target);
        var source = ReadSource(target, proof.AfterSnapshotPath, proof.AfterSnapshotSha256);
        var bin = CropSeedResourceBoundary.ReadTerminalShippingBin(source, target);
        if (bin["pending_settlement_value"]!.GetValue<long>() <= 0)
            throw new InvalidDataException("Existing shipping requires a nonempty valuable retained bank.");
        return new("crop_teacher_existing_shipping_baseline.v1", Path.GetFullPath(proof.AfterSnapshotPath),
            proof.AfterSnapshotSha256, bin["pending_settlement_value"]!.GetValue<long>(),
            bin["contents_signature"]!.GetValue<string>());
    }

    internal void Verify(CropTeacherTerminalShippingTarget target, SnapshotEnvelope current, int count)
    {
        var proof = ReadRetention(target);
        if (SchemaVersion != "crop_teacher_existing_shipping_baseline.v1" || PendingValue <= 0 ||
            target.NativeVariants is null || !Path.IsPathFullyQualified(SnapshotPath) ||
            !string.Equals(Path.GetFullPath(SnapshotPath), Path.GetFullPath(proof.AfterSnapshotPath),
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) ||
            SnapshotSha256 != proof.AfterSnapshotSha256)
            throw new InvalidDataException("Existing shipping baseline differs from the original retained output.");
        var source = ReadSource(target, SnapshotPath, SnapshotSha256);
        var bin = CropSeedResourceBoundary.ReadTerminalShippingBin(source, target);
        if (bin["pending_settlement_value"]!.GetValue<long>() != PendingValue ||
            bin["contents_signature"]!.GetValue<string>() != ContentsSignature)
            throw new InvalidDataException("Existing shipping baseline value/contents changed.");
        CropSeedResourceBoundary.VerifyTerminalShippingContents(source, current, target, count);
    }

    private static MoneyRevenueCropOutputRetentionProof ReadRetention(CropTeacherTerminalShippingTarget target)
    {
        if (CurrentTeacherFrontierSupport.HashFile(target.RetentionProofPath) != target.RetentionProofSha256)
            throw new InvalidDataException("Existing shipping retention certificate changed.");
        return CurrentTeacherFrontierSupport.Read<MoneyRevenueCropOutputRetentionProof>(target.RetentionProofPath, "existing shipping retention");
    }

    private static SnapshotEnvelope ReadSource(CropTeacherTerminalShippingTarget target, string path, string hash)
    {
        if (CurrentTeacherFrontierSupport.HashFile(path) != hash)
            throw new InvalidDataException("Existing shipping retained snapshot changed.");
        var source = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(path, "existing shipping source");
        MoneyRevenueCropSeedAcquisitionProofBuilder.ValidateSnapshot(source, "existing shipping source");
        if (source.SaveId.Value != target.SaveId || source.PlayerId.Value != target.PlayerId || source.GameVersion != target.GameVersion ||
            MoneyRevenueCropSeedAcquisitionProofBuilder.ReadInt(source, "time", "total_days") != target.TotalDay)
            throw new InvalidDataException("Existing shipping retained actor/day changed.");
        return source;
    }
}
