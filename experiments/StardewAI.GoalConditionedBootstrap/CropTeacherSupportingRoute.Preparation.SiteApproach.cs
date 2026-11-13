using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Plans;
using StardewAI.Core.Execution;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.Training;

namespace StardewAI.GoalConditionedBootstrap;

public sealed partial class CropTeacherSupportingRoute
{
    public sealed partial class Preparation
    {
        private readonly string? siteObservationPath;

        private Preparation(string runRequestPath, string sourcePath, string ledgerPath, IReadOnlyCollection<string> commitmentIds,
            string commitmentId, string stepId, string observationPath, string modelPlanPath, string queuePath)
        {
            var paths = new[] { runRequestPath, sourcePath, ledgerPath, observationPath, modelPlanPath, queuePath }
                .Select(Path.GetFullPath).Distinct(CropTeacherSeedTransferNativeOutcome.PathComparer).ToArray();
            using var reads = InvocationReadSession.Start(paths);
            inputs = new(paths);
            var observation = CropTeacherSiteApproachObservation.Open(runRequestPath, sourcePath, ledgerPath,
                commitmentIds, commitmentId, stepId, observationPath);
            Require(observation.Target is not null && !observation.AlreadyAtStand,
                "Owned site approach requires an actual selected stand, not arrival.");
            SiteApproachTarget = new(observation.Target!.TileX, observation.Target.TileY);
            source = CropTeacherSupportingSource.Read(runRequestPath, sourcePath, ledgerPath, commitmentIds, stepId);
            this.runRequestPath = Path.GetFullPath(runRequestPath); this.ledgerPath = Path.GetFullPath(ledgerPath);
            this.commitmentIds = commitmentIds.ToArray(); this.stepId = stepId;
            siteObservationPath = Path.GetFullPath(observationPath);
            SourcePath = Path.GetFullPath(sourcePath); QueuePath = Path.GetFullPath(queuePath);
            Kind = "supporting_route_to_crop";
            FinalTargetLocation = ExpectedHopTargetLocation = source.CropLocation;
            TotalDay = observation.TotalDay;
            var plan = CurrentTeacherFrontierSupport.Read<SmallModelPlanEnvelope>(modelPlanPath, "original site approach plan");
            const string prefix = "plan:crop-site-approach:";
            var suffix = $":{SiteApproachTarget.TileX},{SiteApproachTarget.TileY}";
            Require(plan.PlanId.StartsWith(prefix, StringComparison.Ordinal) && plan.PlanId.EndsWith(suffix, StringComparison.Ordinal) &&
                    plan.PlanId.Length > prefix.Length + suffix.Length &&
                    int.TryParse(plan.PlanId[prefix.Length..^suffix.Length], NumberStyles.None, CultureInfo.InvariantCulture, out var iteration) && iteration > 0,
                "Owned site approach lacks its original candidate prefix/iteration/stand coordinates.");
            CandidateId = plan.PlanId["plan:".Length..];
            var expected = CropBoundedApproachPlanBuilder.Build(source.Snapshot.StateHash, CandidateId,
                "authoritative_crop_site_approach.v1", "site_preparation_starts_only_after_approach",
                SiteApproachTarget.TileX, SiteApproachTarget.TileY, ExecutionTargetProfiles.TrainingSingleplayer,
                JsonSerializer.SerializeToNode(ExecutionTargetProfiles.CreateActor(ExecutionTargetProfiles.TrainingSingleplayer), JsonDefaults.Options)!);
            var raw = CurrentTeacherFrontierSupport.Read<JsonElement>(modelPlanPath, "original site approach model bytes");
            Require(JsonNode.DeepEquals(expected, JsonNode.Parse(raw.GetRawText())),
                "Owned site approach differs from its original full-source model/actor/stand/repair recipe.");
            var queue = CurrentTeacherFrontierSupport.Read<ActionQueueEnvelope>(queuePath, "original site approach compilation");
            var rebuilt = new ActionQueueCompiler().Compile(plan, source.Snapshot, source.Ledger);
            CropTeacherOriginalQueueCompilation.Verify(queue, rebuilt, "Owned site approach");
            Require(queue.Status == "pending" && queue.CompilerDiagnostics.Length == 0 && queue.Items is [var item] &&
                    item.Status == "pending" && item.OptionId == "executor.move_to_tile" && item.BlockingReasons.Length == 0 &&
                    item.MissingStateFactors.Length == 0 && item.NormalizedCommand.StateHash == source.Snapshot.StateHash,
                "Owned site approach requires its original single pending neutral move, never a clear/connector/resource queue.");
            observation.ValidateInputsUnchanged();
            inputs.Validate();
        }

        public static Preparation OpenSiteApproach(string runRequestPath, string sourcePath, string ledgerPath,
            IReadOnlyCollection<string> commitmentIds, string commitmentId, string stepId, string observationPath,
            string modelPlanPath, string queuePath) =>
            new(runRequestPath, sourcePath, ledgerPath, commitmentIds, commitmentId, stepId, observationPath, modelPlanPath, queuePath);

        public bool IsSiteApproach => SiteApproachTarget is not null;
        public MoneyRevenueCropPlantingTile? SiteApproachTarget { get; }
    }
}
