namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    private static void VerifyRadioactiveOreCalendarResolution()
    {
        var route = new AcquisitionRequirementRouteLowering(
            "native_radioactive_ore_node",
            "GameLocation.breakStone",
            "decompiled native method",
            "stone 95 => (O)909",
            "policy_option",
            "native_outcome_domain_and_retry_bound",
            StageOneCollectionRouteDependencyAxes.Required.ToArray(),
            ["mining.reach_depth"],
            Array.Empty<string>(),
            true,
            true);
        var resolved = AcquisitionRouteCalendarResolutionBuilder
            .ResolveRadioactiveOreNodeWindow("(O)909", route, 336);
        var window = RequireSingleRadioactiveWindow(resolved.Windows);
        Require(
            resolved.Status ==
                "resolved_static_source_window_target_date_pending" &&
            resolved.EvidenceClass ==
                "native_radioactive_ore_node_calendar_invariant" &&
            resolved.BlockingReasons.Length == 0 &&
            window.SourceKind == "radioactive_ore_node" &&
            window.SourceKey == "GameLocation.breakStone" &&
            window.RuleId == "stone:95" &&
            window.FirstTotalDay == 0 &&
            window.LastTotalDay == 335 &&
            window.RequiresLocationAccessEvidence &&
            window.RequiresExistingLiveCandidateMatch &&
            window.StochasticOutcome,
            "Radioactive ore node calendar contract drifted.");

        var rejected = AcquisitionRouteCalendarResolutionBuilder
            .ResolveRadioactiveOreNodeWindow(
                "(O)909",
                route with { SourcePath = "stone 95 => (O)390" },
                336);
        Require(
            rejected.Status ==
                "blocked_authoritative_radioactive_ore_node_source_invalid" &&
            rejected.Windows.Length == 0 &&
            rejected.BlockingReasons.SequenceEqual(new[]
            {
                "radioactive_ore_node_authoritative_source_identity_invalid"
            }),
            "Radioactive ore node calendar admitted a forged source identity.");
    }

    private static T RequireSingleRadioactiveWindow<T>(T[] values)
    {
        Require(values.Length == 1,
            "Radioactive ore node calendar emitted an invalid window count.");
        return values[0];
    }
}
