using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using StardewAI.Contracts.State;

namespace StardewAI.Backend.Tests;

public sealed class SnapshotHashTests
{
    [Fact]
    public void DirectStateHashMatchesLegacyCanonicalRootHash()
    {
        var state = new Dictionary<string, JsonElement>
        {
            ["zeta"] = JsonSerializer.SerializeToElement(new
            {
                nested = new Dictionary<string, object?>
                {
                    ["b"] = "line\nquote\"unicode-\u6e38\u620f",
                    ["a"] = 1.25m
                }
            }),
            ["alpha"] = JsonSerializer.SerializeToElement(new object?[] { true, null, -42, "value" })
        };

        var legacyCanonical = SnapshotHash.Canonicalize(JsonSerializer.SerializeToElement(state));
        var expected = ToLowerHex(SHA256.HashData(Encoding.UTF8.GetBytes(legacyCanonical)));

        Assert.Equal(expected, SnapshotHash.ComputeStateHash(state));
    }

    [Fact]
    public void DirectStateHashRemainsIndependentOfRootInsertionOrder()
    {
        var alpha = JsonSerializer.SerializeToElement(new { value = 1 });
        var zeta = JsonSerializer.SerializeToElement(new { value = 2 });
        var first = new Dictionary<string, JsonElement>
        {
            ["zeta"] = zeta,
            ["alpha"] = alpha
        };
        var second = new Dictionary<string, JsonElement>
        {
            ["alpha"] = alpha,
            ["zeta"] = zeta
        };

        Assert.Equal(SnapshotHash.ComputeStateHash(first), SnapshotHash.ComputeStateHash(second));
    }

    [Fact]
    public void StateHashIgnoresMachineProbeCacheTickButNotMachineState()
    {
        var first = new Dictionary<string, JsonElement>
        {
            ["farm"] = JsonSerializer.SerializeToElement(new
            {
                machines = new[]
                {
                    new
                    {
                        qualified_item_id = "(BC)13",
                        machine_probe_cache_tick = 1179,
                        ready_for_harvest = false
                    }
                }
            })
        };
        var refreshed = new Dictionary<string, JsonElement>
        {
            ["farm"] = JsonSerializer.SerializeToElement(new
            {
                machines = new[]
                {
                    new
                    {
                        qualified_item_id = "(BC)13",
                        machine_probe_cache_tick = 1181,
                        ready_for_harvest = false
                    }
                }
            })
        };
        var changed = new Dictionary<string, JsonElement>
        {
            ["farm"] = JsonSerializer.SerializeToElement(new
            {
                machines = new[]
                {
                    new
                    {
                        qualified_item_id = "(BC)13",
                        machine_probe_cache_tick = 1181,
                        ready_for_harvest = true
                    }
                }
            })
        };

        Assert.Equal(
            SnapshotHash.ComputeStateHash(first),
            SnapshotHash.ComputeStateHash(refreshed));
        Assert.NotEqual(
            SnapshotHash.ComputeLegacyStateHash(first),
            SnapshotHash.ComputeLegacyStateHash(refreshed));
        Assert.NotEqual(
            SnapshotHash.ComputeStateHash(first),
            SnapshotHash.ComputeStateHash(changed));
        Assert.True(SnapshotHash.MatchesStateHash(
            first,
            SnapshotHash.ComputeLegacyStateHash(first)));
    }

    private static string ToLowerHex(byte[] bytes)
    {
        var builder = new StringBuilder(bytes.Length * 2);
        foreach (var value in bytes)
        {
            builder.Append(value.ToString("x2"));
        }

        return builder.ToString();
    }
}
