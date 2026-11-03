using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Core.Infrastructure;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    public static void RunCurrentNativeCalendar(string outputRoot) => new StageOneCollectionFrontierFixture(outputRoot).RunCurrentNativeCalendar();

    private sealed partial class StageOneCollectionFrontierFixture
    {
        public void RunCurrentNativeCalendar()
        {
            var inputs = PrepareCurrentNativeGeode();
            var snapshot = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(inputs.BeforeSnapshotPath, "Native calendar fixture");
            var passed = new List<string>();
            var legacy = AcquisitionRouteCalendarResolutionBuilder.Build(inventoryPath, loweringPath, windowsPath);
            Require(legacy.Routes.Single(IsGeode).Status != "resolved_static_source_window_target_date_pending",
                "The regression fixture must reproduce loss of live geode sources in static calendar mode.");
            passed.Add("static_calendar_drops_live_geode_regression");
            var source = AcquisitionRouteCalendarResolutionBuilder.BuildCurrentSources(inventoryPath, loweringPath, windowsPath, inputs.BeforeSnapshotPath);
            Write(routeCalendarPath, source);
            Require(source.Routes.Single(IsGeode).Status == "resolved_static_source_window_target_date_pending" &&
                source.SupportedRouteKinds.Contains("native_geode_drop"), "Snapshot-aware source mode did not preserve the geode route.");
            passed.Add("current_native_geode_preserved");
            var cc = source.Routes.Where(row => row.RequirementSetId == CurrentCommunityCenterRequirementAuthorityBuilder.RequirementSetId).ToArray();
            Require(cc.Length > 0 && cc.All(row => row.BlockingReasons.Contains("current_community_center_denominator_not_requested")) &&
                source.Routes.Count(row => row.RequirementSetId == "museum_collection") == 95 && source.RouteOccurrenceInventoryComplete &&
                source.Routes.Length == legacy.Routes.Length && !source.UsesCurrentCommunityCenterDenominator,
                "CC must be explicitly blocked while the complete museum and full route denominator are retained.");
            passed.Add("full_denominator_and_explicit_cc_exclusion");
            Require(source.CurrentNativeSourceBinding is { Scope: "non_community_center" } binding &&
                binding.StateHash == snapshot.StateHash && binding.SnapshotSha256 == HashFile(inputs.BeforeSnapshotPath) &&
                binding.SaveId == snapshot.SaveId.Value && binding.PlayerId == snapshot.PlayerId.Value && binding.GameVersion == snapshot.GameVersion,
                "Native source mode must bind real source hash, state, actor, save and version.");
            passed.Add("native_snapshot_identity_bound");
            var target = AcquisitionRouteTargetDateCalendarBuilder.BuildCurrentSources(inventoryPath, loweringPath, windowsPath,
                routeCalendarPath, inputs.BeforeSnapshotPath, 0);
            Write(targetDateCalendarPath, target);
            Require(target.Routes.Single(row => row.RequirementId == "museum:item:538").StaticWindowMatchesTargetDate &&
                target.CurrentNativeSourceBinding == source.CurrentNativeSourceBinding && !target.UsesCurrentCommunityCenterDenominator,
                "The target calendar must preserve the independently rebuilt current-source scope.");
            passed.Add("target_calendar_preserves_current_binding");
            var unlock = AcquisitionRouteTargetDateUnlockBuilder.Build(inventoryPath, loweringPath, windowsPath,
                routeCalendarPath, targetDateCalendarPath, inputs.BeforeSnapshotPath);
            Require(unlock.RouteOccurrenceInventoryComplete && unlock.SnapshotStateHash == snapshot.StateHash,
                "Downstream rebuilding must honor native-source mode rather than returning to static/global-CC mode.");
            passed.Add("downstream_independent_native_rebuild");
            var sourceCli = Path.Combine(root, "native-calendar-cli.json");
            RunControlCli(inputs, "build-current-native-acquisition-route-calendar-resolution", sourceCli);
            Require(EqualJson(source, CurrentTeacherFrontierSupport.Read<AcquisitionRouteCalendarResolutionReport>(sourceCli, "Native calendar CLI")),
                "The actual source CLI must rebuild the same current native context.");
            passed.Add("actual_native_source_cli");
            var targetCli = Path.Combine(root, "native-target-cli.json");
            RunControlCli(inputs, "build-current-native-acquisition-route-target-date-calendar", targetCli, "--target-total-day", "0");
            Require(EqualJson(target, CurrentTeacherFrontierSupport.Read<AcquisitionRouteTargetDateCalendarReport>(targetCli, "Native target CLI")),
                "The actual target CLI must preserve source scope.");
            passed.Add("actual_native_target_cli");
            var forged = JsonSerializer.SerializeToNode(source, JsonDefaults.Options)!;
            forged["current_native_source_binding"]!["scope"] = "community_center_standard";
            Write(routeCalendarPath, forged);
            Reject("forged_scope_rejected", RebuildTarget);
            Write(routeCalendarPath, source);
            var missing = JsonSerializer.SerializeToNode(target, JsonDefaults.Options)!;
            missing.AsObject().Remove("current_native_source_binding");
            Write(targetDateCalendarPath, missing);
            Reject("missing_binding_cannot_fall_back_to_static", () => AcquisitionRouteTargetDateUnlockBuilder.Build(
                inventoryPath, loweringPath, windowsPath, routeCalendarPath, targetDateCalendarPath, inputs.BeforeSnapshotPath));
            Write(targetDateCalendarPath, target);
            var changed = CloneSnapshot(snapshot);
            var player = JsonSerializer.SerializeToNode(changed.State["player"])!;
            player["geode_processing"]!["value"]!["money_before"] = 999;
            changed.State["player"] = JsonSerializer.SerializeToElement(player);
            changed.StateHash = SnapshotHash.ComputeStateHash(changed.State);
            Write(inputs.BeforeSnapshotPath, changed);
            Reject("fresh_snapshot_invalidates_old_calendar", RebuildTarget);
            Write(inputs.BeforeSnapshotPath, snapshot);
            changed = CloneSnapshot(snapshot);
            changed.PlayerId.Value = "2";
            Write(inputs.BeforeSnapshotPath, changed);
            Reject("foreign_outer_actor_rejected", RebuildSource);
            Write(inputs.BeforeSnapshotPath, snapshot);
            changed = CloneSnapshot(snapshot);
            changed.GameVersion = "1.6.14";
            Write(inputs.BeforeSnapshotPath, changed);
            Reject("different_native_version_rejected", RebuildSource);
            Write(inputs.BeforeSnapshotPath, snapshot);
            Reject("full_current_cc_mode_stays_strict", () => AcquisitionRouteCalendarResolutionBuilder.BuildCurrent(
                inventoryPath, loweringPath, windowsPath, inputs.BeforeSnapshotPath));
            var retainedSnapshotSha = HashFile(inputs.BeforeSnapshotPath);
            new StageOneCollectionFrontierFixture(Path.Combine(Path.GetDirectoryName(root)!, "pause"))
                .VerifyCurrentNativeOwnedPauseLocation(passed);
            Require(HashFile(inputs.BeforeSnapshotPath) == retainedSnapshotSha,
                "Owned-pause negative cases changed the retained fixture used by runtime source checks.");
            passed.Add("shared_fixture_snapshot_preserved");
            Write(Path.Combine(root, "current-native-calendar-self-test.json"), new
            {
                status = "passed", passed_count = passed.Count, passed, native_game_run = false,
                criterion_promoted = false, formal_training_authorized = false
            });
            Console.WriteLine("Current native calendar self-test passed: " + passed.Count);

            void RebuildSource() => _ = AcquisitionRouteCalendarResolutionBuilder.BuildCurrentSources(inventoryPath, loweringPath, windowsPath, inputs.BeforeSnapshotPath);
            void RebuildTarget() => _ = AcquisitionRouteTargetDateCalendarBuilder.BuildCurrentSources(inventoryPath, loweringPath, windowsPath, routeCalendarPath, inputs.BeforeSnapshotPath, 0);
            void Reject(string name, Action action)
            {
                var rejected = false;
                try { action(); } catch (Exception exception) when (exception is InvalidDataException or InvalidOperationException) { rejected = true; }
                Require(rejected, "Native source mode accepted " + name);
                passed.Add(name);
            }
            static bool IsGeode(AcquisitionRouteCalendarResolution row) => row.RequirementId == "museum:item:538";
        }
    }
}
