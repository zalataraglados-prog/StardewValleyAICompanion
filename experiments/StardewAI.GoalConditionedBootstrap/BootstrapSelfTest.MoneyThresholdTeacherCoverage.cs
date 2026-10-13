using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Training;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    private static string BuildMoneyThresholdTeacherCorpusFixture(
        string outputRoot)
    {
        var fixtureRoot = Path.Combine(
            outputRoot,
            "money-threshold-terminal-fixture");
        Directory.CreateDirectory(fixtureRoot);
        var sources = new List<MoneyThresholdTeacherCorpusSource>();
        foreach (var partition in new[]
                 {
                     PolicyDatasetPartitions.Train,
                     PolicyDatasetPartitions.Validation,
                     PolicyDatasetPartitions.Test
                 })
        {
            var suffix = 0;
            string saveId;
            const string playerId = "money-threshold-player";
            const int totalDay = 80;
            do
            {
                saveId = "money-threshold-" + partition + "-" + suffix++;
            } while (PolicyTrajectoryDatasetBuilder.PartitionFor(
                         saveId + ":" + playerId + ":" + totalDay) !=
                     partition);

            var sourceId = "money-threshold-" + partition;
            var sourceRoot = Path.Combine(fixtureRoot, sourceId);
            Directory.CreateDirectory(sourceRoot);
            var before = MoneyThresholdSnapshot(
                saveId,
                playerId,
                totalDay,
                gameTick: 100,
                money: 1_000,
                totalMoneyEarned: 49_990);
            var after = MoneyThresholdSnapshot(
                saveId,
                playerId,
                totalDay,
                gameTick: 130,
                money: 1_020,
                totalMoneyEarned: 50_010);
            var beforePath = Path.Combine(sourceRoot, "before-snapshot.json");
            var afterPath = Path.Combine(sourceRoot, "after-snapshot.json");
            var resultPath = Path.Combine(sourceRoot, "execution-result.json");
            var episodePath = Path.Combine(sourceRoot, "execution-episode.json");
            Write(beforePath, before);
            Write(afterPath, after);
            var changes = new[]
            {
                new SimulatedFactChange
                {
                    Path = "player.money",
                    Before = "1000",
                    After = "1020"
                },
                new SimulatedFactChange
                {
                    Path = "player.inventory.(O)24.count",
                    Before = "1",
                    After = "0"
                }
            };
            var reasons = new[]
            {
                "native_shop_menu_click_applied",
                "money_increased_by_exact_sale_value",
                "inventory_count_decreased_by_exact_stack"
            };
            var result = new TrainingExecutionResult
            {
                RunId = sourceId,
                QueueId = sourceId + "-queue",
                QueueItemId = sourceId + "-sale",
                BeforeStateHash = before.StateHash,
                OptionId = "executor.sell_shop_item",
                Status = "applied",
                FeedbackAvailable = true,
                PrimitiveKind = "sell_shop_item",
                PrimitiveVerificationStatus = "verified",
                PrimitiveVerificationReasons = reasons,
                RequestedEffect =
                    "shop_id=SeedShop;qualified_item_id=(O)24;quantity=1;expected_unit_price=20",
                ObservedEffect =
                    "menus.active_menu.type=ShopMenu;shop_id=SeedShop;money=1020",
                BlockReasons = Array.Empty<string>(),
                ChangedFacts = changes
            };
            Write(resultPath, result);
            Write(episodePath, new PlanExecutionEpisodeEnvelope
            {
                EpisodeId = sourceId + "-episode",
                RunId = result.RunId,
                SourceStateHash = before.StateHash,
                AfterStateHash = after.StateHash,
                StateHashChanged = true,
                BeforeGameTick = before.GameTick,
                AfterGameTick = after.GameTick,
                AfterSnapshotFresh = true,
                AfterSnapshotNote = "fresh",
                ExecutionResultPath = resultPath,
                BeforeSnapshotPath = beforePath,
                AfterSnapshotPath = afterPath,
                QueueId = result.QueueId,
                OptionId = result.OptionId,
                Status = result.Status,
                Success = true,
                TrainingRole = TrainingRoles.ExecutorCalibration,
                BlockReasons = Array.Empty<string>(),
                EffectiveQueueItem = JsonSerializer.SerializeToElement(new
                {
                    queue_item_id = result.QueueItemId,
                    option_id = result.OptionId,
                    status = "pending",
                    blocking_reasons = Array.Empty<string>(),
                    normalized_command = new
                    {
                        option_id = result.OptionId,
                        state_hash = before.StateHash,
                        parameters = new[]
                        {
                            new { name = "qualified_item_id", value = "(O)24" },
                            new { name = "quantity", value = "1" },
                            new { name = "expected_unit_price", value = "20" }
                        },
                        steps = new[]
                        {
                            new
                            {
                                step_id = result.QueueItemId + ":sell",
                                step_type = "sell_shop_item",
                                target = "SeedShop:(O)24",
                                expected_effect = "money_increased_by_exact_sale_value",
                                estimated_ticks = 30
                            }
                        }
                    }
                }),
                PrimitiveKind = result.PrimitiveKind,
                PrimitiveVerificationStatus =
                    result.PrimitiveVerificationStatus,
                PrimitiveVerificationReasons = reasons,
                RequestedEffect = result.RequestedEffect,
                ObservedEffect = result.ObservedEffect,
                ChangedFacts = JsonSerializer.SerializeToElement(changes)
            });
            sources.Add(new MoneyThresholdTeacherCorpusSource(
                sourceId,
                episodePath));
        }

        var requestPath = Path.Combine(
            fixtureRoot,
            "money-threshold-teacher-corpus-request.json");
        Write(requestPath, new MoneyThresholdTeacherCorpusRequest
        {
            CorpusId = "self-test-money-threshold-terminal-corpus",
            Sources = sources.ToArray(),
            FormalProductTrainingAuthorized = false
        });
        var corpusPath = Path.Combine(
            fixtureRoot,
            "money-threshold-teacher-corpus.json");
        var corpus = MoneyThresholdTeacherCorpusBuilder.Build(requestPath);
        Require(
            corpus.Status ==
                "verified_partial_money_threshold_teacher_corpus" &&
            corpus.RowCount == 3 &&
            corpus.CoveredCriterionCount == 1 &&
            corpus.Rows.All(row =>
                row.CrossedThresholds.Length == 1 &&
                row.CrossedThresholds[0].CriterionId == "money_50000" &&
                row.CrossedThresholds[0].TeacherGoalContextId ==
                    "grandpa.criterion:money_50000" &&
                row.CrossedThresholds[0].TerminalAssertion ==
                    "player.total_money_earned >= 50000" &&
                row.CrossedThresholds[0].TeacherComparisonVerified &&
                row.CrossedThresholds[0]
                    .NativeTerminalOutcomeVerified),
            "Money-threshold fixture did not preserve criterion-local coverage.");
        Write(corpusPath, corpus);
        MoneyThresholdTeacherCorpusBuilder.Verify(corpusPath);
        var tampered = JsonNode.Parse(File.ReadAllText(corpusPath))!
            .AsObject();
        tampered["rows"]![0]!["crossed_thresholds"]![0]!["criterion_id"] =
            "money_100000";
        var tamperedPath = Path.Combine(
            fixtureRoot,
            "tampered-money-threshold-teacher-corpus.json");
        File.WriteAllText(
            tamperedPath,
            tampered.ToJsonString(new JsonSerializerOptions
            {
                WriteIndented = true
            }) + Environment.NewLine);
        var rejected = false;
        try
        {
            MoneyThresholdTeacherCorpusBuilder.Verify(tamperedPath);
        }
        catch (InvalidDataException)
        {
            rejected = true;
        }
        Require(rejected,
            "Caller-authored money criterion attribution entered coverage.");
        var tamperedEpisode = JsonNode.Parse(File.ReadAllText(
                sources[0].ExecutionEpisodePath))!
            .AsObject();
        tamperedEpisode["effective_queue_item"]!["normalized_command"]!
            ["parameters"]![1]!["value"] = "2";
        var tamperedEpisodePath = Path.Combine(
            fixtureRoot,
            "tampered-sale-quantity-execution-episode.json");
        File.WriteAllText(
            tamperedEpisodePath,
            tamperedEpisode.ToJsonString(new JsonSerializerOptions
            {
                WriteIndented = true
            }) + Environment.NewLine);
        var tamperedRequestPath = Path.Combine(
            fixtureRoot,
            "tampered-sale-quantity-request.json");
        Write(tamperedRequestPath, new MoneyThresholdTeacherCorpusRequest
        {
            CorpusId = "self-test-money-threshold-tampered-sale",
            Sources = new[]
            {
                new MoneyThresholdTeacherCorpusSource(
                    "tampered-sale-quantity",
                    tamperedEpisodePath)
            },
            FormalProductTrainingAuthorized = false
        });
        rejected = false;
        try
        {
            MoneyThresholdTeacherCorpusBuilder.Build(tamperedRequestPath);
        }
        catch (InvalidDataException)
        {
            rejected = true;
        }
        Require(rejected,
            "A forged compiled sale quantity entered money coverage.");
        return corpusPath;
    }

    private static SnapshotEnvelope MoneyThresholdSnapshot(
        string saveId,
        string playerId,
        int totalDay,
        long gameTick,
        int money,
        uint totalMoneyEarned)
    {
        var state = JsonSerializer.Deserialize<
            Dictionary<string, JsonElement>>(
            JsonSerializer.Serialize(new
            {
                time = new
                {
                    total_days = Envelope(totalDay)
                },
                player = new
                {
                    money = Envelope(money),
                    total_money_earned = Envelope(totalMoneyEarned)
                },
                farm = new
                {
                    shipping_bins = Envelope(new[]
                    {
                        new
                        {
                            completed = true,
                            bin_scope = "personal",
                            player_id = playerId,
                            pending_settlement_value = 0,
                            pending_settlement_value_complete = true,
                            pending_settlement_timing =
                                "next_native_day_transition",
                            contents_signature = "empty-shipping-scope",
                            contents_truncated = false
                        }
                    })
                }
            }),
            JsonDefaults.Options)!;
        return new SnapshotEnvelope
        {
            SchemaVersion = "snapshot.v1",
            BridgeVersion = "0.1.0",
            GameVersion = "1.6.15",
            SaveId = Identity(saveId),
            PlayerId = Identity(playerId),
            GameTick = gameTick,
            RealTimestamp = "2026-10-14T00:00:00Z",
            Completeness = "complete",
            State = state,
            StateHash = SnapshotHash.ComputeStateHash(state)
        };
    }
}
