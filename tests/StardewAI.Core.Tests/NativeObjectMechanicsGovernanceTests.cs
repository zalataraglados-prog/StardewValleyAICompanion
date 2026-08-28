namespace StardewAI.Core.Tests;

public sealed class NativeObjectMechanicsGovernanceTests
{
    private static readonly string[] ActionFiles =
    {
        "HousePlant",
        "SingingStone",
        "SlimeBall",
        "FeedHopper",
        "AutoGrabber",
        "MiniObelisk"
    };

    [Fact]
    public void CandidateActionsUseOneSafeItemParserAndOneStableStandSelector()
    {
        foreach (var action in ActionFiles)
        {
            var source = ReadRepositoryFile(
                "src", "StardewAI.Core", "OptionRegistry",
                $"CandidateOptionAvailabilityEvaluator.{action}.cs");

            Assert.Contains("ReadNativeObjectSafeItemContext(snapshot)", source, StringComparison.Ordinal);
            Assert.Contains("SelectNearestAvailableNativeObjectStand(", source, StringComparison.Ordinal);
            Assert.DoesNotContain($"Select{action}Stand", source, StringComparison.Ordinal);
            Assert.DoesNotContain($"record {action}Stand", source, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void CompilerActionsUseOneExactProjectionAndSafeItemShell()
    {
        foreach (var action in ActionFiles)
        {
            var source = ReadRepositoryFile(
                "src", "StardewAI.Core", "Execution", $"ActionQueueCompiler.{action}.cs");

            Assert.Contains("SelectExactReadyNativeObjectProjection(", source, StringComparison.Ordinal);
            Assert.Contains("ReadNativeObjectCompilerSafeItemContext(snapshot)", source, StringComparison.Ordinal);
            Assert.DoesNotContain("ReadStateFieldValue(snapshot, \"current_location\", \"objects\")", source, StringComparison.Ordinal);
            Assert.DoesNotContain("TryGetProperty(\"stand_tiles\"", source, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void SlimeBallUsesSharedMovementWithoutOwningAnotherMovementLoop()
    {
        var source = ReadRepositoryFile(
            "tools", "StardewAI.RuntimeTestHarness", "ModEntry.SlimeBall.cs");

        Assert.Contains("ActiveSlimeBallCollection : INativeObjectInteractionMovement", source, StringComparison.Ordinal);
        Assert.Contains("AdvanceNativeObjectInteractionMovement(active, \"slime_ball\"", source, StringComparison.Ordinal);
        Assert.DoesNotContain("StartMoving(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("MovePlayerForTick(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("PathIndex++", source, StringComparison.Ordinal);
    }

    [Fact]
    public void DestructiveObjectTrapHasOneAuthoritativeDefinition()
    {
        var runtimeRoot = Path.Combine(FindRepositoryRoot(), "tools", "StardewAI.RuntimeTestHarness");
        var definitions = Directory.EnumerateFiles(runtimeRoot, "ModEntry*.cs")
            .Select(File.ReadAllText)
            .Sum(source => Count(source, "private static bool IsDestructiveObjectTrap("));

        Assert.Equal(1, definitions);
    }

    [Fact]
    public void RootRuntimeDelegatesNativeObjectStateTickAndResetToOneDomainOwner()
    {
        var root = ReadRepositoryFile("tools", "StardewAI.RuntimeTestHarness", "ModEntry.cs");
        var domain = ReadRepositoryFile(
            "tools", "StardewAI.RuntimeTestHarness", "ModEntry.NativeObjectInteractionDomain.cs");

        Assert.Contains("TickNativeObjectInteractionDomain();", root, StringComparison.Ordinal);
        Assert.Contains("ResetNativeObjectInteractionDomain();", root, StringComparison.Ordinal);
        Assert.Contains("nativeObjectInteractions.IsActive", root, StringComparison.Ordinal);
        Assert.DoesNotContain("activeHousePlantRotation", root, StringComparison.Ordinal);
        Assert.DoesNotContain("activeSlimeBallCollection", root, StringComparison.Ordinal);
        Assert.Contains("sealed class NativeObjectInteractionDomainState", domain, StringComparison.Ordinal);
    }

    [Fact]
    public void NativeObjectPayloadV2IsTypedAndRuntimeKeepsV1Fallback()
    {
        var request = new StardewAI.Contracts.Training.TrainingExecutionRequest
        {
            OptionId = "farming.collect_slime_ball",
            NativeObjectPayload = new StardewAI.Contracts.Training.NativeObjectExecutionPayload
            {
                Kind = "slime_ball",
                TargetTileX = 20,
                TargetTileY = 20,
                SlimeBall = new StardewAI.Contracts.Training.SlimeBallExecutionProjection
                {
                    RequiredFragility = 2,
                    ExpectedSlimeQuantity = 17
                }
            }
        };

        var json = System.Text.Json.JsonSerializer.Serialize(request);
        var roundTrip = System.Text.Json.JsonSerializer.Deserialize<
            StardewAI.Contracts.Training.TrainingExecutionRequest>(json)!;
        Assert.Equal("native_object_execution_payload.v2", roundTrip.NativeObjectPayload?.SchemaVersion);
        Assert.Equal(17, roundTrip.NativeObjectPayload?.SlimeBall?.ExpectedSlimeQuantity);

        var miniObelisk = new StardewAI.Contracts.Training.TrainingExecutionRequest
        {
            OptionId = "movement.use_mini_obelisk",
            NativeObjectPayload = new StardewAI.Contracts.Training.NativeObjectExecutionPayload
            {
                Kind = "mini_obelisk",
                MiniObelisk = new StardewAI.Contracts.Training.MiniObeliskExecutionProjection
                {
                    PairMemberIndex = 0,
                    DestinationTileX = 30,
                    LandingTileY = 31
                }
            }
        };
        var miniJson = System.Text.Json.JsonSerializer.Serialize(miniObelisk);
        var miniRoundTrip = System.Text.Json.JsonSerializer.Deserialize<
            StardewAI.Contracts.Training.TrainingExecutionRequest>(miniJson)!;
        Assert.Equal(30, miniRoundTrip.NativeObjectPayload?.MiniObelisk?.DestinationTileX);

        var ingress = ReadRepositoryFile(
            "tools", "StardewAI.RuntimeTestHarness", "ModEntry.NativeObjectPayload.cs");
        Assert.Contains("if (payload is null)", ingress, StringComparison.Ordinal);
        Assert.Contains("request.SlimeBallExpectedSlimeQuantity =", ingress, StringComparison.Ordinal);
        Assert.Contains("requires_exactly_one_projection", ingress, StringComparison.Ordinal);
        Assert.Contains("projection_kind_mismatch", ingress, StringComparison.Ordinal);
        Assert.Contains("\"house_plant\" => payload.HousePlant is not null", ingress, StringComparison.Ordinal);
    }

    [Fact]
    public void NativeObjectCapabilitiesHaveOneDeclarationPerOption()
    {
        var source = ReadRepositoryFile(
            "src", "StardewAI.Contracts", "Capabilities", "OptionCapabilityRegistrySource.cs");
        var optionIds = new[]
        {
            "world.rotate_house_plant",
            "world.play_singing_stone",
            "farming.collect_slime_ball",
            "animals.withdraw_feed_hopper_hay",
            "animals.collect_auto_grabber_contents",
            "movement.use_mini_obelisk"
        };

        foreach (var optionId in optionIds)
            Assert.Equal(1, Count(source, optionId));
    }

    [Fact]
    public void NativeObjectActionsRetainIndependentContractsAndReceipts()
    {
        var expected = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["HousePlant"] = "house_plant_native_receipt_mismatch",
            ["SingingStone"] = "singing_stone_native_receipt_mismatch",
            ["SlimeBall"] = "slime_ball_native_output_receipt_mismatch",
            ["FeedHopper"] = "feed_hopper_native_receipt_mismatch",
            ["AutoGrabber"] = "auto_grabber_native_receipt_mismatch",
            ["MiniObelisk"] = "mini_obelisk_native_action_return_mismatch"
        };

        foreach (var pair in expected)
        {
            var source = ReadRepositoryFile(
                "tools", "StardewAI.RuntimeTestHarness", $"ModEntry.{pair.Key}.cs");
            Assert.Contains("NativeContract", source, StringComparison.Ordinal);
            Assert.Contains(pair.Value, source, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void MachineCandidateImplementationStaysSplitByResponsibility()
    {
        var files = new[]
        {
            ReadRepositoryFile(
                "src", "StardewAI.Core", "OptionRegistry",
                "CandidateOptionAvailabilityEvaluator.Machines.cs"),
            ReadRepositoryFile(
                "src", "StardewAI.Core", "OptionRegistry",
                "CandidateOptionAvailabilityEvaluator.Machines.Input.cs"),
            ReadRepositoryFile(
                "src", "StardewAI.Core", "OptionRegistry",
                "CandidateOptionAvailabilityEvaluator.Machines.Prediction.cs")
        };

        Assert.All(files, source => Assert.True(
            source.Split('\n').Length < 800,
            "Machine candidate partial files must remain below 800 lines."));

        var combined = string.Concat(files);
        Assert.Contains("MachineProcessingCandidates", combined, StringComparison.Ordinal);
        Assert.Contains("MachineLoadInputCandidates", combined, StringComparison.Ordinal);
        Assert.Contains("PredictMachineOutputFromProbe", combined, StringComparison.Ordinal);
    }

    [Fact]
    public void KnowledgeCompilerSupportsReproducibleGenerationTimestamps()
    {
        var source = ReadRepositoryFile(
            "tools", "StardewAI.KnowledgeCompiler", "Program.cs");

        Assert.Contains("STARDEWAI_GENERATED_AT_UTC", source, StringComparison.Ordinal);
        Assert.Contains("SOURCE_DATE_EPOCH", source, StringComparison.Ordinal);
        Assert.Contains("generated_at_utc = GeneratedAtUtc", source, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "generated_at_utc = DateTimeOffset.UtcNow",
            source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void SlimeBallSmokeWaitsForAsynchronousFixtureWarpProjection()
    {
        var source = ReadRepositoryFile(
            "scripts", "Invoke-RuntimeSlimeBallSmoke.ps1");

        Assert.Contains("Wait-SlimeBallProjection", source, StringComparison.Ordinal);
        Assert.Contains("Timed out waiting for the Slime Ball fixture projection", source, StringComparison.Ordinal);
        Assert.Contains("$projected = Wait-SlimeBallProjection $snapshotUrl 30", source, StringComparison.Ordinal);
    }

    private static int Count(string source, string value) =>
        source.Split(value, StringSplitOptions.None).Length - 1;

    private static string ReadRepositoryFile(params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { FindRepositoryRoot() }.Concat(parts).ToArray()));

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "StardewValleyAICompanion.sln")))
                return current.FullName;
            current = current.Parent;
        }
        throw new DirectoryNotFoundException("Could not locate StardewAI repository root.");
    }
}
