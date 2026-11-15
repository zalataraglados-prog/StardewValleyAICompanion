using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Strategy;
using StardewAI.Core.Training;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;
using static StardewAI.Backend.Tests.CropTeacherSeedPurchaseSettlementTests;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedTransferTests
{
    private sealed class SelectedSaleStockFixture : IDisposable
    {
        internal SharedTransferReturnFixture Transfer { get; }
        internal OwnerInputs Inputs { get; }
        internal OwnerFiles OwnerFiles { get; }
        internal CropTeacherSharedTransferDecision Decision { get; private set; }
        internal SelectedQueueCandidateLock Candidate { get; private set; }
        internal CropSaleStockTransferPreparation Preview { get; }
        internal LiveTrainingOptions Options => Transfer.Runtime.Options;

        internal SelectedSaleStockFixture(string fault = "", bool partitioned = false)
        {
            Transfer = new(partitioned ? before =>
            {
                MaterialInventoryGraphReader.TryRead(before, out var graph);
                graph!.InventoryNodes.Single(row => row.NodeId == "player:123").Slots =
                    [new() { SlotIndex = 0, QualifiedItemId = "(O)330", Stack = 998, Quality = 0,
                        MaximumStackSize = 999, RuntimeType = "StardewValley.Object" }];
                var farm = JsonNode.Parse(before.State["farm"].GetRawText())!;
                farm["material_inventory_graph"]!["value"] = JsonSerializer.SerializeToNode(graph, JsonOptions);
                before.State["farm"] = JsonSerializer.SerializeToElement(farm, JsonOptions);
                var player = JsonNode.Parse(before.State["player"].GetRawText())!;
                player["inventory"]!["value"] = JsonSerializer.SerializeToNode(new[] {
                    new { slot_index = 0, qualified_item_id = "(O)330", stack = 998, quality = 0, is_empty = false } }, JsonOptions);
                before.State["player"] = JsonSerializer.SerializeToElement(player, JsonOptions);
            } : null);
            var f = Transfer;
            var ledger = f.Runtime.Repository.Get(f.Files.Before);
            const string producer = "declared-stock-main-source";
            var item = JsonSerializer.SerializeToElement(new { slot_index = 0, qualified_item_id = "(O)330", stack = 7, quality = 0, is_empty = false });
            var stock = f.Runtime.Repository.CommitReservationPortfolio(f.Files.Before, new ReservationPortfolioCommitRequest
            {
                StateHash = f.Files.Before.StateHash, ExpectedLedgerRevision = ledger.Revision,
                PortfolioId = "declare-stock-main-fixture", SourceDecisionId = producer, GoalId = "earn_money",
                MaterialClaims = [new()
                {
                    StateHash = f.Files.Before.StateHash, ExpectedLedgerRevision = ledger.Revision,
                    ReservationId = CropSaleStockReservationContract.ReservationId(producer, 0),
                    SourceDecisionId = producer, GoalId = "earn_money", NodeId = "chest:Farm:8,5", SlotIndex = 0,
                    QualifiedItemId = "(O)330", Quantity = 2,
                    Purpose = CropSaleStockReservationContract.TransferablePurpose("declared-stock-crop", "declared-stock-wave",
                        MoneyRevenueCropRetentionAccount.InventoryIdentity(item), 0)
                }]
            });
            Assert.True(stock.Accepted, string.Join(",", stock.Errors)); f.Files.AdoptLedger(stock.Ledger!);
            var intent = new MaterialTransferIntent { SourceNodeId = "chest:Farm:8,5", SourceSlotIndex = 0,
                DestinationNodeId = "player:123", QualifiedItemId = "(O)330", Quantity = 2, ExpectedSourceStack = 7 };
            var parameters = MaterialTransferIntentBinder.ToParameters(intent);
            Preview = CropSaleStockTransferPreparation.Create(f.Files.Before, stock.Ledger!, parameters, out var reasons)!;
            Assert.NotNull(Preview); Assert.Empty(reasons);
            var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(f.Files.Before,
                [new OptionAvailabilityCandidate { OptionId = "inventory.transfer_item", AllowCropSaleStockPreparationPreview = true,
                    Parameters = parameters }], commitmentLedger: stock.Ledger);
            Assert.True(Assert.Single(availability.Options).Available, string.Join(",", availability.Options.SelectMany(row => row.BlockingReasons)));
            var rows = new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability, "grandpa21");
            var plan = new DailyPlanCompiler().Compile(rows, f.Files.Before.StateHash, goalId: "grandpa21", maxCandidates: 1,
                availableMinutes: 600, energyBudget: 270);
            var ranking = new JsonObject { ["ranked_event_candidates"] = JsonSerializer.SerializeToNode(rows, JsonOptions) };
            Candidate = Assert.Single(SelectedQueueDecisionLease.CreateFromPlan(plan, ranking).Candidates);
            if (fault.Length > 0)
            {
                var raw = ranking["ranked_event_candidates"]![0]!["parameters"]!.AsArray();
                var marker = raw.Single(row => row!["name"]!.GetValue<string>() == CropSaleStockTransferPreparation.RequestParameter)!;
                if (fault == "unknown_control") raw.Add(new JsonObject { ["name"] = "crop_seed_transfer_version", ["value"] = "1" });
                if (fault == "duplicate_marker") raw.Add(marker.DeepClone());
                if (fault == "marker")
                {
                    var request = JsonNode.Parse(marker["value"]!.GetValue<string>())!;
                    request["portfolio_id"] = "different-preview"; marker["value"] = request.ToJsonString();
                }
                if (fault == "binding") raw.Single(row => row!["name"]!.GetValue<string>() == "prepared_material_transfer_sha256")!["value"] = new string('a', 64);
                Candidate = Assert.Single(SelectedQueueDecisionLease.CreateFromPlan(plan, ranking).Candidates);
            }
            Directory.CreateDirectory(f.Files.RuntimePath);
            var planPath = Path.Combine(f.Files.RuntimePath, "stock-original-daily-plan.json");
            var rankingPath = Path.Combine(f.Files.RuntimePath, "stock-original-ranking.json");
            Fixture.Save(planPath, plan); Fixture.Save(rankingPath, ranking);
            Decision = new(f.Files.SourcePath, planPath, rankingPath, Candidate.QueueIndex, []);
            Inputs = new(f.Files.SourcePath, f.Files.LedgerPath, f.Files.RequestPath);
            OwnerFiles = new(Options, f.Runtime.Client, f.Files.SourcePath, f.Files.LedgerPath, Inputs) { RetainArtifacts = true };
            Options.SkipTraining = true; Options.UseDailyPlan = true; Options.UseJointDailyCalendar = true;
        }

        internal async Task RetainDecision(CropTeacherManagedSeedOwner owner)
        {
            var reference = await (Task<CropTeacherDailyDecisionReference>)typeof(CropTeacherManagedSeedOwner)
                .GetMethod("SaveDailyDecisionAsync", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(owner,
                    [1, Decision.SnapshotPath, Decision.PlanPath, Decision.RankingPath, Array.Empty<string>()])!;
            Decision = Decision with { DecisionReference = reference };
        }

        internal async Task<object> PrepareMain(CropTeacherManagedSeedOwner owner, int iteration = 2)
        {
            var snapshot = JsonSerializer.Deserialize<SnapshotEnvelope>(File.ReadAllText(owner.CurrentSnapshotPath), JsonOptions)!;
            var plan = JsonSerializer.Deserialize<SmallModelPlanEnvelope>(File.ReadAllText(Decision.PlanPath), JsonOptions)!;
            var ranking = JsonNode.Parse(File.ReadAllText(Decision.RankingPath))!.AsObject();
            var type = typeof(LiveTrainingOptions).Assembly.GetType("Program")!.GetNestedType("CropTeacherDailySelection", BindingFlags.NonPublic)!;
            var selection = Activator.CreateInstance(type, snapshot.State["time"].GetProperty("total_days").GetProperty("value").GetInt32(),
                plan.GoalId, SelectedQueueDecisionLease.CreateFromPlan(plan, ranking), new HashSet<string>(StringComparer.Ordinal),
                Decision.PlanPath, Decision.RankingPath)!;
            type.GetProperty("SourceSnapshotPath")!.SetValue(selection, Decision.SnapshotPath);
            type.GetProperty("DecisionReference")!.SetValue(selection, Decision.DecisionReference);
            var task = InvokeLive<Task>("PrepareCropTeacherSelectedOtherGoalAsync", Transfer.Runtime.Client, Options, iteration,
                snapshot, selection, Candidate, owner);
            await task;
            return task.GetType().GetProperty("Result")!.GetValue(task)!;
        }

        public void Dispose() { OwnerFiles.Dispose(); Inputs.Dispose(); Transfer.Dispose(); }
    }
}
