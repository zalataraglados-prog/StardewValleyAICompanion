using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Core.OptionRegistry;

namespace StardewAI.Core.Training
{
    public sealed partial class CurrentSocialContactFrontierProducer
    {
        public CurrentSocialContactFrontier Produce(
            JsonElement snapshot,
            string timingCalibrationArtifactJson)
        {
            if (!TryReadInputs(
                    snapshot,
                    out var totalDays,
                    out var gameTime,
                    out var gameVersion,
                    out var startLocation,
                    out var startX,
                    out var startY,
                    out var catalogField,
                    out var catalog,
                    out var schedules,
                    out var routeGraph,
                    out var routeDateEvidence,
                    out var movementContext,
                    out var inputReason))
            {
                return Blocked(inputReason);
            }

            var timingLoad = new FutureRouteTimingCalibrationLoader().Load(
                timingCalibrationArtifactJson,
                movementContext,
                gameVersion,
                totalDays);
            if (timingLoad.Status != FutureRouteTimingCalibrationLoadStatus.Loaded ||
                timingLoad.Calibration is null)
            {
                return Blocked(timingLoad.BlockingReasons);
            }
            if (!FutureRouteDateEvidenceProducer.TryCreateContext(
                    routeGraph,
                    routeDateEvidence,
                    totalDays,
                    out var routeContext,
                    out var routeContextBlocks))
            {
                return Blocked(routeContextBlocks);
            }

            var catalogRows = catalog.GetProperty("villagers")
                .EnumerateArray()
                .Where(row => row.ValueKind == JsonValueKind.Object &&
                    ReadBool(row, "event_actor") != true)
                .ToArray();
            var names = catalogRows
                .Select(row => ReadString(row, "npc_name"))
                .Where(name => name.Length > 0)
                .ToArray();
            var duplicateNames = names
                .GroupBy(name => name, StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();
            var uniqueNames = names
                .Distinct(StringComparer.Ordinal)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();

            var opportunities = new List<CurrentSocialContactOpportunity>();
            var dynamicTrackingDirectives =
                new List<CurrentSocialDynamicTrackingDirective>();
            var dynamicTrackingIntents =
                new List<CurrentSocialDynamicTrackingIntent>();
            var coverage = new List<CurrentSocialNpcCoverage>();
            var routeProducer = new FutureRouteDateEvidenceProducer();
            foreach (var npcName in uniqueNames)
            {
                var npcCatalogRows = catalogRows.Where(row => string.Equals(
                        ReadString(row, "npc_name"),
                        npcName,
                        StringComparison.Ordinal))
                    .ToArray();
                if (npcCatalogRows.Length > 0 &&
                    npcCatalogRows.All(IsNativeSocializationDisabled))
                {
                    coverage.Add(new CurrentSocialNpcCoverage
                    {
                        NpcName = npcName,
                        Status = "resolved_no_action_allowed",
                        ScheduleProjectionStatus =
                            "NotRequiredNativeSocializationDisabled"
                    });
                    continue;
                }
                if (duplicateNames.Contains(npcName, StringComparer.Ordinal))
                {
                    coverage.Add(BlockedNpc(
                        npcName,
                        "current_social_catalog_npc_name_ambiguous"));
                    continue;
                }

                var projected = gameTime == 600
                    ? new NpcCurrentScheduleSnapshotProjectionResolver()
                        .Resolve(snapshot, npcName)
                    : null;
                NpcFuturePresenceWindowResolution presence;
                string projectionStatus;
                if (projected is not null &&
                    projected.Status == "pass" &&
                    projected.Projection is not null)
                {
                    presence = new NpcFuturePresenceWindowResolver().Resolve(
                        projected.Projection);
                    projectionStatus = projected.ProjectionStatus;
                    if (presence.Status != NpcFuturePresenceWindowResolutionStatus.Exact)
                    {
                        coverage.Add(BlockedNpc(
                            npcName,
                            presence.BlockingReasons,
                            projectionStatus));
                        continue;
                    }
                }
                else
                {
                    var loadedSchedule = gameTime == 600
                        ? null
                        : new NpcCurrentLoadedSchedulePresenceResolver()
                            .Resolve(snapshot, npcName);
                    if (loadedSchedule?.Status == "pass" &&
                        loadedSchedule.Presence is not null)
                    {
                        presence = loadedSchedule.Presence;
                        projectionStatus = loadedSchedule.ProjectionStatus;
                    }
                    else
                    {
                        var staticProjection =
                            new NpcCurrentStaticPresenceProjectionResolver()
                                .Resolve(snapshot, npcName);
                        if (staticProjection.Status != "pass" ||
                            staticProjection.Presence is null)
                        {
                            var dynamicSpouse =
                                new NpcCurrentSpouseDynamicTrackingResolver()
                                    .Resolve(snapshot, npcName);
                            if (dynamicSpouse.Status == "pass" &&
                                dynamicSpouse.Directive is not null)
                            {
                                var giftBinding =
                                    new SocialGiftInventoryBindingResolver()
                                        .Resolve(snapshot, npcName);
                                if (dynamicSpouse.Directive.CandidateFamilies.Contains(
                                        "social.gift_npc",
                                        StringComparer.Ordinal) &&
                                    giftBinding.Status != "exact")
                                {
                                    coverage.Add(BlockedNpc(
                                        npcName,
                                        giftBinding.BlockingReasons,
                                        dynamicSpouse.ProjectionStatus));
                                    continue;
                                }
                                if (!giftBinding.Bindings.Any(value =>
                                        value.Available))
                                {
                                    dynamicSpouse.Directive.CandidateFamilies =
                                        dynamicSpouse.Directive.CandidateFamilies
                                            .Where(value => value !=
                                                "social.gift_npc")
                                            .ToArray();
                                }
                                var dynamicCompile =
                                    new CurrentSocialDynamicTrackingIntentCompiler()
                                        .Compile(
                                            snapshot,
                                            dynamicSpouse.Directive);
                                if (dynamicCompile.Status != "pass")
                                {
                                    coverage.Add(BlockedNpc(
                                        npcName,
                                        dynamicCompile.BlockingReasons,
                                        dynamicSpouse.ProjectionStatus));
                                    continue;
                                }
                                dynamicTrackingDirectives.Add(
                                    dynamicSpouse.Directive);
                                dynamicTrackingIntents.AddRange(
                                    dynamicCompile.Intents);
                                coverage.Add(new CurrentSocialNpcCoverage
                                {
                                    NpcName = npcName,
                                    Status = "resolved_live_rebind_required",
                                    ScheduleProjectionStatus =
                                        dynamicSpouse.ProjectionStatus
                                });
                                continue;
                            }
                            coverage.Add(BlockedNpc(
                                npcName,
                                (projected?.Issues ?? Array.Empty<string>())
                                    .Concat(
                                        loadedSchedule?.BlockingReasons ??
                                        Array.Empty<string>())
                                    .Concat(staticProjection.BlockingReasons)
                                .Concat(dynamicSpouse.BlockingReasons)
                                .DefaultIfEmpty(
                                    "current_social_presence_projection_unavailable")
                                .Distinct(StringComparer.Ordinal)
                                .ToArray(),
                                projected?.ProjectionStatus ??
                                loadedSchedule?.ProjectionStatus ??
                                staticProjection.ProjectionStatus));
                            continue;
                        }
                        presence = staticProjection.Presence;
                        projectionStatus = staticProjection.ProjectionStatus;
                    }
                }

                var eligibility = new FutureNpcContactEligibilityProducer()
                    .Produce(catalogField, totalDays, presence);
                if (eligibility.Status != FutureNpcContactEligibilityProductionStatus.Exact)
                {
                    coverage.Add(BlockedNpc(
                        npcName,
                        eligibility.BlockingReasons,
                        projectionStatus));
                    continue;
                }

                var npcOpportunities = new List<CurrentSocialContactOpportunity>();
                var routeFailures = new List<string>();
                var excludedWindowOrdinals = new HashSet<int>();
                var exclusionReasons = new HashSet<string>(StringComparer.Ordinal);
                var giftBindingResolution =
                    new SocialGiftInventoryBindingResolver().Resolve(
                        snapshot,
                        npcName);
                var giftBindings = giftBindingResolution.Bindings
                    .Where(value => value.Available)
                    .ToArray();
                if (eligibility.Evidence.Any(value => value.GiftAllowed) &&
                    giftBindingResolution.Status != "exact")
                {
                    routeFailures.AddRange(
                        giftBindingResolution.BlockingReasons.Select(reason =>
                            "gift_binding:" + reason));
                }
                foreach (var window in presence.Windows.Where(value =>
                    value.HasStableInterval &&
                    value.EndpointBehaviorComplete))
                {
                    var eligibilityRows = eligibility.Evidence.Where(value =>
                        value.ScheduleEntryOrdinal == window.ScheduleEntryOrdinal &&
                        string.Equals(
                            value.LocationName,
                            window.LocationName,
                            StringComparison.Ordinal) &&
                        value.TileX == window.TileX &&
                        value.TileY == window.TileY)
                        .ToArray();
                    if (eligibilityRows.Length != 1)
                    {
                        routeFailures.Add(
                            "current_social_window_eligibility_missing_or_ambiguous:" +
                            window.ScheduleEntryOrdinal);
                        continue;
                    }
                    if (!eligibilityRows[0].TalkAllowed &&
                        !eligibilityRows[0].GiftAllowed)
                    {
                        continue;
                    }

                    var exactGiftAllowed =
                        eligibilityRows[0].GiftAllowed &&
                        giftBindingResolution.Status == "exact" &&
                        giftBindings.Length > 0;
                    if (eligibilityRows[0].GiftAllowed &&
                        giftBindingResolution.Status == "exact" &&
                        giftBindings.Length == 0)
                    {
                        excludedWindowOrdinals.Add(
                            window.ScheduleEntryOrdinal);
                        exclusionReasons.Add(
                            "gift:no_positive_owned_gift_binding:" +
                            window.ScheduleEntryOrdinal);
                    }
                    if (!eligibilityRows[0].TalkAllowed &&
                        !exactGiftAllowed)
                    {
                        continue;
                    }

                    var routeProduction = routeProducer.Produce(
                            routeContext,
                            new FutureRouteDateEvidenceRequest
                            {
                                TotalDays = totalDays,
                                StartLocation = startLocation,
                                StartTileX = startX,
                                StartTileY = startY,
                                EarliestDepartureTime = gameTime,
                                TargetLocation = window.LocationName,
                                TargetTileX = window.TileX,
                                TargetTileY = window.TileY
                            },
                            timingLoad.Calibration);
                    if (routeProduction.Status !=
                            FutureRouteDateEvidenceProductionStatus.Produced ||
                        routeProduction.Scenario is null)
                    {
                        if (routeProduction.BlockingReasons.Length > 0 &&
                            routeProduction.BlockingReasons.All(
                                IsResolvedRouteExclusion))
                        {
                            excludedWindowOrdinals.Add(window.ScheduleEntryOrdinal);
                            foreach (var reason in routeProduction.BlockingReasons)
                            {
                                exclusionReasons.Add(
                                    reason + ":" + window.ScheduleEntryOrdinal);
                            }
                        }
                        else
                        {
                            routeFailures.AddRange(
                                routeProduction.BlockingReasons.Select(reason =>
                                    reason + ":" + window.ScheduleEntryOrdinal));
                        }
                        continue;
                    }

                    var singlePresence = SingleWindowPresence(
                        presence,
                        window);
                    var beforeTalkCount = npcOpportunities.Count;
                    var talkResolution = AddOpportunity(
                        npcOpportunities,
                        routeGraph,
                        routeProduction.Scenario,
                        singlePresence,
                        eligibilityRows,
                        "talk",
                        timingLoad.Calibration.EvidenceId,
                        executionReady: true,
                        executionReadiness: "native_talk_ready");
                    if (eligibilityRows[0].TalkAllowed &&
                        npcOpportunities.Count == beforeTalkCount)
                    {
                        RecordContactFailure(
                            talkResolution,
                            "talk",
                            window.ScheduleEntryOrdinal,
                            routeFailures,
                            excludedWindowOrdinals,
                            exclusionReasons);
                    }
                    foreach (var giftBinding in giftBindings)
                    {
                        var beforeGiftCount = npcOpportunities.Count;
                        var giftResolution = AddOpportunity(
                            npcOpportunities,
                            routeGraph,
                            routeProduction.Scenario,
                            singlePresence,
                            eligibilityRows,
                            "gift",
                            timingLoad.Calibration.EvidenceId,
                            executionReady: true,
                            executionReadiness:
                                "native_gift_inventory_and_taste_binding_ready",
                            giftBinding);
                        if (exactGiftAllowed &&
                            npcOpportunities.Count == beforeGiftCount)
                        {
                            RecordContactFailure(
                                giftResolution,
                                "gift",
                                window.ScheduleEntryOrdinal,
                                routeFailures,
                                excludedWindowOrdinals,
                                exclusionReasons);
                        }
                    }
                }

                opportunities.AddRange(npcOpportunities);
                var stableWindows = presence.Windows.Count(value =>
                    value.HasStableInterval && value.EndpointBehaviorComplete);
                var noActionsAllowed = eligibility.Evidence.All(value =>
                    !value.TalkAllowed && !value.GiftAllowed);
                var fullyResolved =
                    routeFailures.Count == 0;
                coverage.Add(new CurrentSocialNpcCoverage
                {
                    NpcName = npcName,
                    Status = fullyResolved
                        ? noActionsAllowed
                            ? "resolved_no_action_allowed"
                            : npcOpportunities.Count > 0
                                ? "exact"
                                : "resolved_no_current_contact"
                        : "blocked",
                    ScheduleProjectionStatus = projectionStatus,
                    StableWindowCount = stableWindows,
                    TalkOpportunityCount = npcOpportunities.Count(value =>
                        value.InteractionKind == "talk"),
                    GiftSlotOpportunityCount = npcOpportunities.Count(value =>
                        value.InteractionKind == "gift"),
                    ExcludedWindowCount = excludedWindowOrdinals.Count,
                    ExclusionReasons = exclusionReasons
                        .OrderBy(value => value, StringComparer.Ordinal)
                        .ToArray(),
                    BlockingReasons = routeFailures
                        .Distinct(StringComparer.Ordinal)
                        .ToArray()
                });
            }

            var blockedCount = coverage.Count(row => row.Status == "blocked");
            var orderedOpportunities = opportunities
                .OrderBy(value => value.EarliestInteractionTime)
                .ThenBy(value => value.NpcName, StringComparer.Ordinal)
                .ThenBy(value => value.InteractionKind, StringComparer.Ordinal)
                .ThenBy(value => value.ScheduleEntryOrdinal)
                .ToArray();
            var rankingAdmissionBlocks = new List<string>();
            if (blockedCount > 0)
                rankingAdmissionBlocks.Add("current_social_npc_coverage_incomplete");
            if (orderedOpportunities.Any(value => !value.ExecutionReady))
            {
                rankingAdmissionBlocks.Add(
                    "current_social_opportunity_execution_binding_incomplete");
            }
            return new CurrentSocialContactFrontier
            {
                Status = orderedOpportunities.Length == 0
                    ? "blocked"
                    : blockedCount == 0
                        ? "pass"
                        : "partial",
                RankingAdmissionReady =
                    rankingAdmissionBlocks.Count == 0,
                TotalDays = totalDays,
                GameTime = gameTime,
                CatalogRowCount = catalogRows.Length,
                UniqueNpcNameCount = uniqueNames.Length,
                DuplicateNpcNames = duplicateNames,
                ExactScheduleProjectionCount = coverage.Count(row =>
                    row.ScheduleProjectionStatus is "Exact" or "Conditional"),
                FullyResolvedNpcCount = coverage.Count(row =>
                    row.Status != "blocked"),
                BlockedNpcCount = blockedCount,
                TalkOpportunityCount = orderedOpportunities.Count(value =>
                    value.InteractionKind == "talk"),
                GiftSlotOpportunityCount = orderedOpportunities.Count(value =>
                    value.InteractionKind == "gift"),
                DynamicTrackingDirectiveCount = dynamicTrackingDirectives.Count,
                DynamicTrackingIntentCount = dynamicTrackingIntents.Count,
                TimingEvidenceId = timingLoad.Calibration.EvidenceId,
                Opportunities = orderedOpportunities,
                DynamicTrackingDirectives = dynamicTrackingDirectives
                    .OrderBy(value => value.NpcName, StringComparer.Ordinal)
                    .ToArray(),
                DynamicTrackingIntents = dynamicTrackingIntents
                    .OrderBy(value => value.NpcName, StringComparer.Ordinal)
                    .ThenBy(value => value.OptionId, StringComparer.Ordinal)
                    .ThenBy(value => value.GiftSlotIndex)
                    .ToArray(),
                NpcCoverage = coverage
                    .OrderBy(value => value.NpcName, StringComparer.Ordinal)
                    .ToArray(),
                BlockingReasons = coverage
                    .Where(row => row.Status == "blocked")
                    .SelectMany(row => row.BlockingReasons.Select(reason =>
                        row.NpcName + ":" + reason))
                    .Distinct(StringComparer.Ordinal)
                    .ToArray(),
                RankingAdmissionBlockingReasons = rankingAdmissionBlocks.ToArray()
            };
        }

    }
}
