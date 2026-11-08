using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Strategy;
using StardewAI.GoalConditionedBootstrap;
using Artifacts = StardewAI.LiveTrainingLoop.CropSeedRuntimeArtifacts;

namespace StardewAI.LiveTrainingLoop;

internal sealed record CropTeacherSeedLifecycleLiveBoundary(StrategyLedgerEvidence Evidence,
    ReservationPortfolioCommitRequest? ReleaseRequest, Artifacts Artifacts);

public static partial class CropTeacherSeedLifecycleRuntime
{
    internal static async Task<CropTeacherSeedLifecycleLiveBoundary> RunManagedAsync(HttpClient http,
        CropTeacherSeedLifecycleRuntimeOptions options, CropTeacherSeedLifecycleSupport scope,
        Func<JsonObject, ReservationPortfolioCommitRequest, Task<JsonObject>> commit,
        Func<string, string, Task<JsonObject>> post, Func<string, string, Task> write)
    {
        var expected = new Dictionary<string, string>(scope.InputHashes,
            OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        ReservationPortfolioCommitRequest? releaseRequest = null;
        var final = await RunAsync(http, options, scope, (diagnostic, request) =>
        {
            releaseRequest = JsonSerializer.Deserialize<ReservationPortfolioCommitRequest>(
                JsonSerializer.Serialize(request, JsonOptions), JsonOptions)!;
            return commit(diagnostic, request);
        }, post, async (path, json) =>
        {
            var hash = PortfolioMutationRequestJournal.Hash(Encoding.UTF8.GetBytes(json));
            await write(path, json).ConfigureAwait(false);
            expected[Path.GetFullPath(path)] = hash;
        }).ConfigureAwait(false);
        expected[Path.GetFullPath(final.Path)] = Path.GetFileNameWithoutExtension(final.Path);
        var artifacts = new Artifacts(expected);
        artifacts.Validate();
        return new(final, releaseRequest, artifacts);
    }
}
