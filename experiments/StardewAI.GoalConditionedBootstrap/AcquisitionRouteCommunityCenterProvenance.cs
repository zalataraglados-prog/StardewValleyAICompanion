using System.Text.Json.Serialization;
using StardewAI.Contracts.State;

namespace StardewAI.GoalConditionedBootstrap;

public sealed class AcquisitionRouteCommunityCenterProvenance
{
    [JsonPropertyName("uses_current_community_center_denominator")]
    public bool UsesCurrentCommunityCenterDenominator { get; set; }

    [JsonPropertyName("community_center_bundle_mode")]
    public string CommunityCenterBundleMode { get; set; } = string.Empty;

    [JsonPropertyName("community_center_denominator_sha256")]
    public string CommunityCenterDenominatorSha256 { get; set; } =
        string.Empty;

    [JsonPropertyName("community_center_source_state_hash")]
    public string CommunityCenterSourceStateHash { get; set; } = string.Empty;

    [JsonPropertyName("community_center_snapshot_sha256")]
    public string CommunityCenterSnapshotSha256 { get; set; } = string.Empty;
}

internal static class AcquisitionRouteCommunityCenterProvenanceSupport
{
    public static AcquisitionRouteCommunityCenterProvenance From(
        AcquisitionRouteTargetDateCalendarReport calendar,
        SnapshotEnvelope snapshot,
        string snapshotSha256)
    {
        var result = new AcquisitionRouteCommunityCenterProvenance
        {
            UsesCurrentCommunityCenterDenominator =
                calendar.UsesCurrentCommunityCenterDenominator,
            CommunityCenterBundleMode = calendar.CommunityCenterBundleMode,
            CommunityCenterDenominatorSha256 =
                calendar.CommunityCenterDenominatorSha256,
            CommunityCenterSourceStateHash =
                calendar.CommunityCenterSourceStateHash,
            CommunityCenterSnapshotSha256 =
                calendar.CommunityCenterSnapshotSha256
        };
        Validate(result);
        if (result.UsesCurrentCommunityCenterDenominator &&
            (result.CommunityCenterSourceStateHash != snapshot.StateHash ||
             !string.Equals(
                 result.CommunityCenterSnapshotSha256,
                 snapshotSha256,
                 StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidDataException(
                "Current Community Center provenance is not bound to the portfolio decision snapshot.");
        }
        return result;
    }

    public static AcquisitionRouteCommunityCenterProvenance Clone(
        AcquisitionRouteCommunityCenterProvenance value)
    {
        ArgumentNullException.ThrowIfNull(value);
        Validate(value);
        return new AcquisitionRouteCommunityCenterProvenance
        {
            UsesCurrentCommunityCenterDenominator =
                value.UsesCurrentCommunityCenterDenominator,
            CommunityCenterBundleMode = value.CommunityCenterBundleMode,
            CommunityCenterDenominatorSha256 =
                value.CommunityCenterDenominatorSha256,
            CommunityCenterSourceStateHash =
                value.CommunityCenterSourceStateHash,
            CommunityCenterSnapshotSha256 =
                value.CommunityCenterSnapshotSha256
        };
    }

    public static bool Equal(
        AcquisitionRouteCommunityCenterProvenance? left,
        AcquisitionRouteCommunityCenterProvenance? right) =>
        left is not null && right is not null &&
        left.UsesCurrentCommunityCenterDenominator ==
            right.UsesCurrentCommunityCenterDenominator &&
        left.CommunityCenterBundleMode == right.CommunityCenterBundleMode &&
        left.CommunityCenterDenominatorSha256 ==
            right.CommunityCenterDenominatorSha256 &&
        left.CommunityCenterSourceStateHash ==
            right.CommunityCenterSourceStateHash &&
        left.CommunityCenterSnapshotSha256 ==
            right.CommunityCenterSnapshotSha256;

    public static bool SameDenominator(
        AcquisitionRouteCommunityCenterProvenance? left,
        AcquisitionRouteCommunityCenterProvenance? right) =>
        left is not null && right is not null &&
        left.UsesCurrentCommunityCenterDenominator ==
            right.UsesCurrentCommunityCenterDenominator &&
        left.CommunityCenterBundleMode == right.CommunityCenterBundleMode &&
        left.CommunityCenterDenominatorSha256 ==
            right.CommunityCenterDenominatorSha256;

    public static void Validate(
        AcquisitionRouteCommunityCenterProvenance value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.UsesCurrentCommunityCenterDenominator)
        {
            if (value.CommunityCenterBundleMode is not ("standard" or
                    "remixed") ||
                !IsSha256(value.CommunityCenterDenominatorSha256) ||
                string.IsNullOrWhiteSpace(
                    value.CommunityCenterSourceStateHash) ||
                !IsSha256(value.CommunityCenterSnapshotSha256))
            {
                throw new InvalidDataException(
                    "Current Community Center provenance is incomplete.");
            }
            return;
        }

        if (!string.IsNullOrEmpty(value.CommunityCenterBundleMode) ||
            !string.IsNullOrEmpty(value.CommunityCenterDenominatorSha256) ||
            !string.IsNullOrEmpty(value.CommunityCenterSourceStateHash) ||
            !string.IsNullOrEmpty(value.CommunityCenterSnapshotSha256))
        {
            throw new InvalidDataException(
                "Static route evidence carries current-save Community Center provenance.");
        }
    }

}
