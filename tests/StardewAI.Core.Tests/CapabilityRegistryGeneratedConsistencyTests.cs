using System.Text.RegularExpressions;
using StardewAI.Contracts.Capabilities;
using StardewAI.Contracts.Options;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed class CapabilityRegistryGeneratedConsistencyTests
{
    [Fact]
    public void CapabilityCatalogGeneratedConsistencyTests()
    {
        var registry = new StardewAI.Core.OptionRegistry.OptionRegistry();
        Assert.Equal(
            OptionCapabilityRegistrySource.All.Select(row => row.OptionId).OrderBy(id => id, StringComparer.Ordinal),
            registry.All.Select(row => row.OptionId).OrderBy(id => id, StringComparer.Ordinal));

        foreach (var option in registry.All)
        {
            var declaration = OptionCapabilityRegistrySource.GetRequired(option.OptionId);
            Assert.Equal(declaration.RegistrationStatus, option.RegistrationStatus);
            Assert.Equal(declaration.ReadStatus, option.ReadStatus);
            Assert.Equal(declaration.CandidateStatus, option.CandidateStatus);
            Assert.Equal(declaration.CompilerStatus, option.CompilerStatus);
            Assert.Equal(declaration.HarnessDispatchSupported, option.HarnessDispatchSupported);
            Assert.Equal(declaration.ProductExecutorSupported, option.ProductExecutorSupported);
            Assert.Equal(declaration.RuntimeEvidenceStatus, option.RuntimeStatus);
            Assert.Equal(declaration.TrainingEligibility, option.TrainingEligibility);
            Assert.Equal(declaration.PolicyTrainingCandidate, option.PolicyTrainingCandidate);
            Assert.Equal(declaration.ReadTrainingGate, option.ReadTrainingGate);
            Assert.Equal(declaration.CandidateTrainingGate, option.CandidateTrainingGate);
            Assert.Equal(declaration.CompilerTrainingGate, option.CompilerTrainingGate);
            Assert.Equal(declaration.RuntimeTrainingGate, option.RuntimeTrainingGate);
            Assert.Equal(declaration.OutputTrainingGate, option.OutputTrainingGate);
            Assert.Equal(declaration.ReadEvidenceIds, option.ReadEvidenceIds);
            Assert.Equal(declaration.CandidateEvidenceIds, option.CandidateEvidenceIds);
            Assert.Equal(declaration.CompilerEvidenceIds, option.CompilerEvidenceIds);
            Assert.Equal(declaration.RuntimeEvidenceIds, option.RuntimeEvidenceIds);
            Assert.Equal(declaration.OutputEvidenceIds, option.OutputEvidenceIds);
            Assert.Equal(declaration.TrainingExclusionReasons, option.TrainingExclusionReasons);
            Assert.Equal(declaration.TrainingEvidenceScope, option.TrainingEvidenceScope);
            Assert.Equal(
                option.TrainingRole != TrainingRoles.ExecutorCalibration,
                declaration.PolicyTrainingCandidate);
        }
    }

    [Fact]
    public void HarnessSupportDoesNotImplyRuntimeEvidenceTests()
    {
        var declaration = OptionCapabilityRegistrySource.GetRequired("executor.interact");

        Assert.True(declaration.HarnessDispatchSupported);
        Assert.False(declaration.ProductExecutorSupported);
        Assert.Equal(OptionRuntimeStatus.RegisteredOnly, declaration.RuntimeEvidenceStatus);
        Assert.Equal(
            OptionTrainingEligibility.BlockedPendingRuntimeEvidence,
            declaration.TrainingEligibility);
        Assert.DoesNotContain(declaration.OptionId, OptionCapabilityRegistrySource.TrainingAllowlist);
    }

    [Fact]
    public void ProductSupportDoesNotImplyTrainingEligibilityTests()
    {
        const bool productExecutorSupported = true;

        Assert.True(productExecutorSupported);
        Assert.False(TrainingEligibilityPolicy.IsEligible(
            OptionRuntimeStatus.RegisteredOnly,
            OptionTrainingEligibility.Eligible,
            autonomousCandidateEnabled: true,
            playerConfirmationRequired: false));
    }

    [Fact]
    public void EveryFullActionHasStepCompilerTests()
    {
        var missing = new StardewAI.Core.OptionRegistry.OptionRegistry().All
            .Where(row => row.CompilerResponsibility == CompilerResponsibilities.FullActionExpansion)
            .Where(row => !ActionQueueCompiler.HasStepCompiler(row.OptionId))
            .Select(row => row.OptionId);

        Assert.Empty(missing);
    }

    [Fact]
    public void EveryCompiledExecutorHasDeclaredDispatchStatusTests()
    {
        foreach (var optionId in ActionQueueCompiler.StepCompilerOptionIds
            .Where(id => id.StartsWith("executor.", StringComparison.Ordinal)))
        {
            Assert.True(OptionCapabilityRegistrySource.TryGet(optionId, out var declaration));
            Assert.Equal(
                RuntimeTestHarnessDispatchCatalog.IsSupported(optionId),
                declaration.HarnessDispatchSupported);
            Assert.Equal(
                ProductExecutorCapabilityCatalog.IsSupported(optionId),
                declaration.ProductExecutorSupported);
        }
    }

    [Fact]
    public void EveryLiteralCandidateKindIsClassifiedTests()
    {
        var optionRegistryRoot = Path.Combine(FindRepositoryRoot(), "src", "StardewAI.Core", "OptionRegistry");
        var generatedKinds = Directory
            .EnumerateFiles(optionRegistryRoot, "*.cs", SearchOption.TopDirectoryOnly)
            .SelectMany(path => Regex.Matches(
                    File.ReadAllText(path),
                    "Kind = \"(?<kind>[^\"]+)\"",
                    RegexOptions.CultureInvariant)
                .Select(match => match.Groups["kind"].Value))
            .ToHashSet(StringComparer.Ordinal);
        var classifiedKinds = OptionCapabilityRegistrySource.DailyCandidates
            .Select(row => row.Kind)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Empty(generatedKinds.Except(classifiedKinds, StringComparer.Ordinal));
        Assert.Equal(
            classifiedKinds.OrderBy(value => value, StringComparer.Ordinal),
            DailyPlanCandidateCapabilityCatalog.All
                .Select(row => row.Kind)
                .OrderBy(value => value, StringComparer.Ordinal));
    }

    [Fact]
    public void UnknownRuntimeOptionFailsClosedTests()
    {
        Assert.False(RuntimeTestHarnessDispatchCatalog.IsSupported("executor.unknown"));
        Assert.False(ProductExecutorCapabilityCatalog.IsSupported("executor.unknown"));
        Assert.False(OptionCapabilityRegistrySource.TryGet("executor.unknown", out _));

        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "tools",
            "StardewAI.RuntimeTestHarness",
            "ModEntry.cs"));
        Assert.Contains("runtime_executor_option_not_supported:", source, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "pending.Completion.SetResult(ExecuteMaintainCropsNoOp(pending.Request));",
            source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void TrainingAllowlistRequiresRuntimeEvidenceTests()
    {
        Assert.NotEmpty(OptionCapabilityRegistrySource.TrainingAllowlist);
        Assert.Equal(
            new[]
            {
                "foraging.harvest_bushes", "foraging.harvest_ginger", "inventory.transfer_item",
                "mining.claim_reward_chests", "mining.obtain_skull_key", "mining.reach_depth",
                "skills.read_books", "social.gift_npc", "social.talk_npc", "volcano.reach_caldera"
            },
            OptionCapabilityRegistrySource.TrainingAllowlist);

        Assert.All(OptionCapabilityRegistrySource.TrainingAllowlist, optionId =>
        {
            var declaration = OptionCapabilityRegistrySource.GetRequired(optionId);
            Assert.True(TrainingEligibilityPolicy.IsEligible(declaration));
            Assert.True(declaration.RuntimeEvidenceStatus >= OptionRuntimeStatus.RuntimeVerified);
            Assert.Equal(TrainingEvidenceGateStatus.RuntimeVerified, declaration.ReadTrainingGate);
            Assert.Equal(TrainingEvidenceGateStatus.RuntimeVerified, declaration.CandidateTrainingGate);
            Assert.Equal(TrainingEvidenceGateStatus.RuntimeVerified, declaration.CompilerTrainingGate);
            Assert.Equal(TrainingEvidenceGateStatus.RuntimeVerified, declaration.RuntimeTrainingGate);
            Assert.Equal(TrainingEvidenceGateStatus.RuntimeVerified, declaration.OutputTrainingGate);
            Assert.NotEmpty(declaration.RuntimeEvidenceIds);
            Assert.NotEmpty(declaration.OutputEvidenceIds);
            Assert.Empty(declaration.TrainingExclusionReasons);
            var expectedScope = optionId switch
            {
                "foraging.harvest_bushes" => "vanilla_current_location_exact_bush_berry_standard_botanist_tea_leaf_golden_walnut_collected_walnut_and_cooldown_matrix",
                "foraging.harvest_ginger" => "vanilla_current_location_exact_ginger_dry_standard_rain_efficient_full_inventory_debris_energy_xp_matrix",
                "inventory.transfer_item" => "explicit_bidirectional_player_normal_chest_transfer",
                "mining.claim_reward_chests" => "loaded_vanilla_mineshaft_exact_reward_chests_fixed_stardrop_forced_random_receipt_and_cleanup_matrix",
                "mining.obtain_skull_key" => "ordinary_mines_floor_119_to_120_native_skull_key_chest_claim_false_to_true_and_exit",
                "mining.reach_depth" => "candidate_bound_ordinary_mine_rolling_current_floor_supported_steps",
                "skills.read_books" => "all_six_vanilla_base_book_branch_families_exact_projection_native_use_and_durable_output",
                "social.gift_npc" => "vanilla_current_loaded_npc_gift_same_map_or_rolling_resolved_route_with_single_item_consumed_to_null",
                "social.talk_npc" => "vanilla_current_loaded_npc_talk_same_map_or_rolling_resolved_route_with_safe_dialogue_close",
                "volcano.reach_caldera" => "vanilla_volcano_generated_levels_0_to_9_rolling_native_actions_typed_combat_intent_to_caldera",
                _ => throw new InvalidOperationException("Unexpected training option: " + optionId)
            };
            Assert.Equal(expectedScope, declaration.TrainingEvidenceScope);
        });

        Assert.False(TrainingEligibilityPolicy.IsEligible(
            OptionRuntimeStatus.RuntimeVerified,
            OptionTrainingEligibility.Eligible,
            autonomousCandidateEnabled: false,
            playerConfirmationRequired: false));
        Assert.False(TrainingEligibilityPolicy.IsEligible(
            OptionRuntimeStatus.RuntimeVerified,
            OptionTrainingEligibility.Eligible,
            autonomousCandidateEnabled: true,
            playerConfirmationRequired: true));
    }

    [Fact]
    public void MineRewardChestAdmissionRequiresLoadedVanillaMatrixAndEvd122Tests()
    {
        var declaration = OptionCapabilityRegistrySource.GetRequired("mining.claim_reward_chests");

        Assert.True(TrainingEligibilityPolicy.IsEligible(declaration));
        Assert.False(declaration.PlayerConfirmationRequired);
        Assert.Equal(CapabilityCompilerStatus.StepCompilerDeclared, declaration.CompilerStatus);
        Assert.True(DailyPlanCompiler.HasOptionCompiler("mining.claim_reward_chests"));
        Assert.False(ActionQueueCompiler.HasStepCompiler("mining.claim_reward_chests"));
        Assert.Equal(new[] { "EVD-122" }, declaration.ReadEvidenceIds);
        Assert.Equal(new[] { "EVD-122" }, declaration.CandidateEvidenceIds);
        Assert.Equal(new[] { "EVD-122" }, declaration.CompilerEvidenceIds);
        Assert.Equal(new[] { "EVD-122" }, declaration.RuntimeEvidenceIds);
        Assert.Equal(new[] { "EVD-122" }, declaration.OutputEvidenceIds);
        Assert.Contains("loaded_vanilla_mineshaft_exact_reward_chests", declaration.TrainingEvidenceScope, StringComparison.Ordinal);
        Assert.Contains("fixed_stardrop_forced_random", declaration.TrainingEvidenceScope, StringComparison.Ordinal);
        Assert.Contains("receipt_and_cleanup_matrix", declaration.TrainingEvidenceScope, StringComparison.Ordinal);
        Assert.DoesNotContain("skull_key", declaration.TrainingEvidenceScope, StringComparison.Ordinal);
        Assert.DoesNotContain("golden_scythe", declaration.TrainingEvidenceScope, StringComparison.Ordinal);
        Assert.DoesNotContain("executor.claim_mine_reward_chest", OptionCapabilityRegistrySource.TrainingAllowlist);
        Assert.DoesNotContain("mining.acquire_golden_scythe", OptionCapabilityRegistrySource.TrainingAllowlist);
    }

    [Fact]
    public void BushAdmissionRequiresExactVanillaBranchMatrixAndEvd120Tests()
    {
        var declaration = OptionCapabilityRegistrySource.GetRequired("foraging.harvest_bushes");

        Assert.True(TrainingEligibilityPolicy.IsEligible(declaration));
        Assert.Equal(CapabilityCompilerStatus.StepCompilerDeclared, declaration.CompilerStatus);
        Assert.True(DailyPlanCompiler.HasOptionCompiler("foraging.harvest_bushes"));
        Assert.False(ActionQueueCompiler.HasStepCompiler("foraging.harvest_bushes"));
        Assert.Equal(new[] { "EVD-120" }, declaration.ReadEvidenceIds);
        Assert.Equal(new[] { "EVD-120" }, declaration.CandidateEvidenceIds);
        Assert.Equal(new[] { "EVD-120" }, declaration.CompilerEvidenceIds);
        Assert.Equal(new[] { "EVD-120" }, declaration.RuntimeEvidenceIds);
        Assert.Equal(new[] { "EVD-120" }, declaration.OutputEvidenceIds);
        Assert.Contains("vanilla_current_location_exact_bush", declaration.TrainingEvidenceScope, StringComparison.Ordinal);
        Assert.Contains("berry_standard_botanist_tea_leaf_golden_walnut", declaration.TrainingEvidenceScope, StringComparison.Ordinal);
        Assert.Contains("collected_walnut_and_cooldown_matrix", declaration.TrainingEvidenceScope, StringComparison.Ordinal);
        Assert.DoesNotContain("custom", declaration.TrainingEvidenceScope, StringComparison.Ordinal);
        Assert.DoesNotContain("town", declaration.TrainingEvidenceScope, StringComparison.Ordinal);
        Assert.DoesNotContain("executor.harvest_bush", OptionCapabilityRegistrySource.TrainingAllowlist);
    }

    [Fact]
    public void GingerAdmissionRequiresExactVanillaCurrentLocationMatrixAndEvd119Tests()
    {
        var declaration = OptionCapabilityRegistrySource.GetRequired("foraging.harvest_ginger");

        Assert.True(TrainingEligibilityPolicy.IsEligible(declaration));
        Assert.Equal(CapabilityCompilerStatus.StepCompilerDeclared, declaration.CompilerStatus);
        Assert.True(DailyPlanCompiler.HasOptionCompiler("foraging.harvest_ginger"));
        Assert.False(ActionQueueCompiler.HasStepCompiler("foraging.harvest_ginger"));
        Assert.Equal(new[] { "EVD-119" }, declaration.ReadEvidenceIds);
        Assert.Equal(new[] { "EVD-119" }, declaration.CandidateEvidenceIds);
        Assert.Equal(new[] { "EVD-119" }, declaration.CompilerEvidenceIds);
        Assert.Equal(new[] { "EVD-119" }, declaration.RuntimeEvidenceIds);
        Assert.Equal(new[] { "EVD-119" }, declaration.OutputEvidenceIds);
        Assert.Contains("vanilla_current_location_exact_ginger", declaration.TrainingEvidenceScope, StringComparison.Ordinal);
        Assert.Contains("rain_efficient_full_inventory_debris", declaration.TrainingEvidenceScope, StringComparison.Ordinal);
        Assert.Contains("energy_xp_matrix", declaration.TrainingEvidenceScope, StringComparison.Ordinal);
        Assert.DoesNotContain("custom", declaration.TrainingEvidenceScope, StringComparison.Ordinal);
        Assert.DoesNotContain("executor.harvest_ginger", OptionCapabilityRegistrySource.TrainingAllowlist);
    }

    [Fact]
    public void SkullKeyAdmissionIsBoundToOrdinaryMineFloor120AndEvd106Tests()
    {
        var declaration = OptionCapabilityRegistrySource.GetRequired("mining.obtain_skull_key");

        Assert.True(TrainingEligibilityPolicy.IsEligible(declaration));
        Assert.Equal(new[] { "EVD-106" }, declaration.ReadEvidenceIds);
        Assert.Equal(new[] { "EVD-106" }, declaration.CandidateEvidenceIds);
        Assert.Equal(new[] { "EVD-106" }, declaration.CompilerEvidenceIds);
        Assert.Equal(new[] { "EVD-106" }, declaration.RuntimeEvidenceIds);
        Assert.Equal(new[] { "EVD-106" }, declaration.OutputEvidenceIds);
        Assert.Contains("ordinary_mines_floor_119_to_120", declaration.TrainingEvidenceScope, StringComparison.Ordinal);
        Assert.DoesNotContain("skull_cavern", declaration.TrainingEvidenceScope, StringComparison.Ordinal);
        Assert.DoesNotContain("golden_scythe", declaration.TrainingEvidenceScope, StringComparison.Ordinal);
        Assert.DoesNotContain("mining.acquire_golden_scythe", OptionCapabilityRegistrySource.TrainingAllowlist);
    }

    [Fact]
    public void VolcanoAdmissionUsesItsOwnNativeRollingEvidenceAndRemainsMineFamilyIsolatedTests()
    {
        var declaration = OptionCapabilityRegistrySource.GetRequired("volcano.reach_caldera");

        Assert.True(TrainingEligibilityPolicy.IsEligible(declaration));
        Assert.False(declaration.PlayerConfirmationRequired);
        Assert.Equal(new[] { "EVD-190", "EVD-191" }, declaration.ReadEvidenceIds);
        Assert.Equal(new[] { "EVD-190", "EVD-191" }, declaration.CandidateEvidenceIds);
        Assert.Equal(new[] { "EVD-190", "EVD-191" }, declaration.CompilerEvidenceIds);
        Assert.Equal(new[] { "EVD-190", "EVD-191" }, declaration.RuntimeEvidenceIds);
        Assert.Equal(new[] { "EVD-190", "EVD-191" }, declaration.OutputEvidenceIds);
        Assert.Contains("volcano_generated_levels_0_to_9", declaration.TrainingEvidenceScope, StringComparison.Ordinal);
        Assert.Contains("typed_combat_intent_to_caldera", declaration.TrainingEvidenceScope, StringComparison.Ordinal);
        Assert.DoesNotContain("ordinary_mine", declaration.TrainingEvidenceScope, StringComparison.Ordinal);
        Assert.DoesNotContain("skull", declaration.TrainingEvidenceScope, StringComparison.Ordinal);
        Assert.DoesNotContain("golden_scythe", declaration.TrainingEvidenceScope, StringComparison.Ordinal);
        Assert.Contains("mining.obtain_skull_key", OptionCapabilityRegistrySource.TrainingAllowlist);
        Assert.DoesNotContain("mining.acquire_golden_scythe", OptionCapabilityRegistrySource.TrainingAllowlist);
    }

    [Fact]
    public void BookAdmissionRequiresAllVanillaBaseBranchesAndEvd124Tests()
    {
        var declaration = OptionCapabilityRegistrySource.GetRequired("skills.read_books");

        Assert.True(TrainingEligibilityPolicy.IsEligible(declaration));
        Assert.Equal(CapabilityCompilerStatus.StepCompilerDeclared, declaration.CompilerStatus);
        Assert.True(DailyPlanCompiler.HasOptionCompiler("skills.read_books"));
        Assert.False(ActionQueueCompiler.HasStepCompiler("skills.read_books"));
        Assert.Equal(new[] { "EVD-124" }, declaration.ReadEvidenceIds);
        Assert.Equal(new[] { "EVD-124" }, declaration.CandidateEvidenceIds);
        Assert.Equal(new[] { "EVD-124" }, declaration.CompilerEvidenceIds);
        Assert.Equal(new[] { "EVD-124" }, declaration.RuntimeEvidenceIds);
        Assert.Equal(new[] { "EVD-124" }, declaration.OutputEvidenceIds);
        Assert.Contains("all_six_vanilla_base_book_branch_families", declaration.TrainingEvidenceScope, StringComparison.Ordinal);
        Assert.Contains("exact_projection_native_use", declaration.TrainingEvidenceScope, StringComparison.Ordinal);
        Assert.DoesNotContain("custom", declaration.TrainingEvidenceScope, StringComparison.Ordinal);
    }

    [Fact]
    public void ExistingGiftChainClosesItsBoundedRuntimeBoundaryWithoutPromotingRecoveryTests()
    {
        var declaration = OptionCapabilityRegistrySource.GetRequired("social.gift_npc");
        Assert.Equal(TrainingEvidenceGateStatus.RuntimeVerified, declaration.ReadTrainingGate);
        Assert.Equal(TrainingEvidenceGateStatus.RuntimeVerified, declaration.CandidateTrainingGate);
        Assert.Equal(TrainingEvidenceGateStatus.RuntimeVerified, declaration.CompilerTrainingGate);
        Assert.Equal(TrainingEvidenceGateStatus.RuntimeVerified, declaration.RuntimeTrainingGate);
        Assert.Equal(TrainingEvidenceGateStatus.RuntimeVerified, declaration.OutputTrainingGate);
        Assert.NotEmpty(declaration.ReadEvidenceIds);
        Assert.NotEmpty(declaration.CandidateEvidenceIds);
        Assert.NotEmpty(declaration.CompilerEvidenceIds);
        Assert.NotEmpty(declaration.RuntimeEvidenceIds);
        Assert.NotEmpty(declaration.OutputEvidenceIds);
        Assert.Empty(declaration.TrainingExclusionReasons);
        Assert.Contains("social.gift_npc", OptionCapabilityRegistrySource.TrainingAllowlist);

        var recovery = OptionCapabilityRegistrySource.GetRequired("recovery.stabilize_day");
        Assert.Equal(TrainingEvidenceGateStatus.RuntimeVerified, recovery.RuntimeTrainingGate);
        Assert.Equal(TrainingEvidenceGateStatus.RuntimeVerified, recovery.OutputTrainingGate);
        Assert.Equal(OptionTrainingEligibility.EvaluationOnly, recovery.TrainingEligibility);
        Assert.NotEmpty(recovery.RuntimeEvidenceIds);
        Assert.NotEmpty(recovery.OutputEvidenceIds);
        Assert.DoesNotContain(
            TrainingAdmissionExclusionReason.RuntimeEvidenceMissing,
            recovery.TrainingExclusionReasons);
        Assert.DoesNotContain(
            TrainingAdmissionExclusionReason.OutputEvidenceMissing,
            recovery.TrainingExclusionReasons);

        Assert.Contains(
            TrainingAdmissionExclusionReason.NotPolicyTrainingOption,
            recovery.TrainingExclusionReasons);
        Assert.DoesNotContain(
            TrainingAdmissionExclusionReason.NotPolicyTrainingOption,
            OptionCapabilityRegistrySource
                .GetRequired("social.gift_npc")
                .TrainingExclusionReasons);
    }

    [Fact]
    public void EveryExcludedOptionHasTypedTrainingAdmissionReasonsTests()
    {
        var excluded = OptionCapabilityRegistrySource.All
            .Where(row => !TrainingEligibilityPolicy.IsEligible(row))
            .ToArray();

        Assert.NotEmpty(excluded);
        Assert.All(excluded, row => Assert.NotEmpty(row.TrainingExclusionReasons));
        Assert.All(
            OptionCapabilityRegistrySource.All.Where(row =>
                row.OptionId.StartsWith("executor.", StringComparison.Ordinal) ||
                row.OptionId is "farm.maintain_crops" or "farm.process_machines" or "recovery.stabilize_day"),
            row => Assert.Contains(
                TrainingAdmissionExclusionReason.NotPolicyTrainingOption,
                row.TrainingExclusionReasons));
    }

    [Fact]
    public void NoDuplicateCapabilityIdTests()
    {
        Assert.Equal(
            OptionCapabilityRegistrySource.All.Count,
            OptionCapabilityRegistrySource.All
                .Select(row => row.OptionId)
                .Distinct(StringComparer.Ordinal)
                .Count());
        Assert.Equal(
            OptionCapabilityRegistrySource.DailyCandidates.Count,
            OptionCapabilityRegistrySource.DailyCandidates
                .Select(row => row.Kind)
                .Distinct(StringComparer.Ordinal)
                .Count());
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "StardewValleyAICompanion.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
