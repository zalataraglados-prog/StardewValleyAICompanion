using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed partial class NativeSocialRuntimeSmokeSourceGuardTests
{
    [Fact]
    public void SmokeScriptDoesNotRequireSocialPlanOnSocialInteract()
    {
        var source = SocialSmokeSource;

        Assert.DoesNotContain("social_plan is null", source, StringComparison.Ordinal);
        Assert.DoesNotContain("social_plan.action_kind", source, StringComparison.Ordinal);
        Assert.DoesNotContain("social_plan.requested_npc_name", source, StringComparison.Ordinal);
        Assert.DoesNotContain("social_plan.requested_slot_index", source, StringComparison.Ordinal);
        Assert.DoesNotContain("social_plan.requested_qualified_item_id", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptStillRequiresNormalizedTalkParameters()
    {
        var source = SocialSmokeSource;

        Assert.Contains("npc_name", source, StringComparison.Ordinal);
        Assert.Contains("social_action_kind", source, StringComparison.Ordinal);
        Assert.Contains("target_location", source, StringComparison.Ordinal);
        Assert.Contains("npc_tile_x", source, StringComparison.Ordinal);
        Assert.Contains("npc_tile_y", source, StringComparison.Ordinal);
        Assert.Contains("stand_tile_x", source, StringComparison.Ordinal);
        Assert.Contains("stand_tile_y", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptStillRequiresNormalizedGiftParameters()
    {
        var source = SocialSmokeSource;

        Assert.Contains("slot_index", source, StringComparison.Ordinal);
        Assert.Contains("qualified_item_id", source, StringComparison.Ordinal);
        Assert.Contains("item_stack_before", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptVerifiesNormalizedParametersMatchCandidate()
    {
        var source = SocialSmokeSource;

        Assert.Contains("Queue social item npc_name mismatch", source, StringComparison.Ordinal);
        Assert.Contains("Queue social item action_kind", source, StringComparison.Ordinal);
        Assert.Contains("Queue social item target_location mismatch", source, StringComparison.Ordinal);
        Assert.Contains("Queue social item missing npc_tile_x", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptVerifiesNormalizedParametersMatchCandidateGift()
    {
        var source = SocialSmokeSource;

        Assert.Contains("Gift queue npc_name mismatch", source, StringComparison.Ordinal);
        Assert.Contains("Gift queue action_kind", source, StringComparison.Ordinal);
        Assert.Contains("Gift queue target_location mismatch", source, StringComparison.Ordinal);
        Assert.Contains("Gift queue social item missing slot_index", source, StringComparison.Ordinal);
        Assert.Contains("Gift queue social item missing qualified_item_id", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptReadsActiveMenuAsObjectNotScalarString()
    {
        var source = SocialSmokeSource;

        Assert.Contains("Get-SnapshotObject", source, StringComparison.Ordinal);

        var lines = source.Split('\n');
        foreach (var line in lines)
        {
            if (line.Contains("Get-SnapshotString", StringComparison.Ordinal) &&
                line.Contains("active_menu", StringComparison.Ordinal))
            {
                Assert.Fail("Get-SnapshotString used with active_menu: " + line.Trim());
            }
        }
    }

    [Fact]
    public void SmokeScriptChecksActiveMenuIsOpenViaPropertyNotStringCompare()
    {
        var source = SocialSmokeSource;

        Assert.Contains("activeMenu.is_open", source, StringComparison.Ordinal);
        Assert.Contains("afterCloseActiveMenu.is_open", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptFailsClosedOnMissingActiveMenuAfterTalk()
    {
        var source = SocialSmokeSource;

        Assert.Contains("active_menu missing or null after talk", source, StringComparison.Ordinal);
        Assert.Contains("active_menu.is_open missing or null after talk", source, StringComparison.Ordinal);
        Assert.Contains("active_menu.is_open must be boolean after talk", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptFailsClosedOnMissingActiveMenuAfterClose()
    {
        var source = SocialSmokeSource;

        Assert.Contains("active_menu missing or null after close", source, StringComparison.Ordinal);
        Assert.Contains("active_menu.is_open missing or null after close", source, StringComparison.Ordinal);
        Assert.Contains("active_menu.is_open must be boolean after close", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptGetSnapshotObjectHasNoDefaultParameter()
    {
        var source = SocialSmokeSource;

        var functionBody = ExtractFunctionBody(source, "Get-SnapshotObject");
        Assert.DoesNotContain("$Default", functionBody, StringComparison.Ordinal);
        Assert.DoesNotContain("[int]", functionBody, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptActiveMenuIsOpenCheckedWithIsNotBoolGuard()
    {
        var source = SocialSmokeSource;

        Assert.Contains("is_open -isnot [bool]", source, StringComparison.Ordinal);
        Assert.Contains("-isnot [bool]", source, StringComparison.Ordinal);
    }

    [Fact]
    public void PlayerAdapterEncodesKnownNoSpouseAsAvailableCanonicalEmptyString()
    {
        var source = File.ReadAllText(FindRepositoryFile("src", "StardewAI.TransparentBridge", "Adapters", "PlayerReadAdapter.cs"));

        Assert.Contains("Context.IsWorldReady && player is not null ? (player.spouse ?? string.Empty) : null", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Context.IsWorldReady ? player?.spouse : null", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptRequiresReadableNonNullSpouseProof()
    {
        var source = SocialSmokeSource;

        Assert.Contains("Snapshot after wait does not have readable player.spouse", source, StringComparison.Ordinal);
        Assert.Contains("known no-spouse state as null instead of canonical empty string", source, StringComparison.Ordinal);
    }

    private static string ExtractFunctionBody(string source, string functionName)
    {
        var lines = source.Split('\n');
        var inside = false;
        var braceDepth = 0;
        var body = new System.Collections.Generic.List<string>();
        foreach (var line in lines)
        {
            if (!inside)
            {
                if (line.TrimStart().StartsWith("function " + functionName, StringComparison.Ordinal))
                {
                    inside = true;
                }
                continue;
            }

            body.Add(line);
            braceDepth += line.Count(c => c == '{') - line.Count(c => c == '}');
            if (braceDepth <= 0)
            {
                break;
            }
        }

        return string.Join("\n", body);
    }

    private static string FindRepositoryFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(new[] { directory.FullName }.Concat(parts).ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not locate repository file", Path.Combine(parts));
    }}
