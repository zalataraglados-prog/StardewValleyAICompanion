using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using StardewAI.Contracts.Strategy;
using StardewAI.Contracts.Training;

namespace StardewAI.Backend.Tests;

public sealed class StageOneTeacherLedgerEvidenceTests
{
    [Fact]
    public void OptionsConstructionDoesNotImplicitlyResumeAnUnrelatedRun()
    {
        var root = Path.Combine(Path.GetTempPath(), "isolated-options");
        var options = new LiveTrainingOptions { Root = root, RunId = "explicit-run" };
        Assert.Empty(options.ManifestPath);
        Assert.Equal(Path.Combine(root, "runs", "explicit-run"), options.RunDir);
    }

    [Fact]
    public void ExplicitCliIdentityDoesNotBorrowTheHistoricalManifestPointer()
    {
        var root = Path.Combine(Path.GetTempPath(), "isolated-options");
        var options = LiveTrainingOptions.Parse(new[] { "--root", root, "--run-id", "explicit-run", "--skip-training" });
        Assert.Empty(options.ManifestPath);
        Assert.Equal(Path.Combine(root, "runs", "explicit-run"), options.RunDir);
    }

    [Fact]
    public async Task LiveTeacherLedgerEvidenceDeduplicatesIdenticalContentAndPreservesPreviousVersions()
    {
        using var handler = new LedgerHandler();
        using var client = new HttpClient(handler);
        var options = Options();
        var first = await Read(client, options);
        Assert.StartsWith(Path.GetFullPath(options.Root) + Path.DirectorySeparatorChar,
            Path.GetFullPath(first.Path!), StringComparison.OrdinalIgnoreCase);
        var bytes = await File.ReadAllBytesAsync(first.Path!);
        Assert.Equal(Path.GetFileNameWithoutExtension(first.Path),
            Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant());
        var second = await Read(client, options);
        Assert.Equal(first.Path, second.Path);
        handler.Revision++;
        var revised = await Read(client, options);
        Assert.NotEqual(first.Path, revised.Path);
        Assert.Equal(bytes, await File.ReadAllBytesAsync(first.Path!));
        Assert.Equal(2, Directory.GetFiles(Path.GetDirectoryName(first.Path!)!).Length);
    }

    [Fact]
    public async Task LiveTeacherRejectsChangedContentAtAnImmutableLedgerEvidencePath()
    {
        using var handler = new LedgerHandler();
        using var client = new HttpClient(handler);
        var options = Options();
        var first = await Read(client, options);
        await File.WriteAllTextAsync(first.Path!, "changed");
        await Assert.ThrowsAsync<InvalidDataException>(() => Read(client, options));
    }

    private static Task<(string? Path, StrategyCommitmentLedger? Ledger)> Read(HttpClient client, LiveTrainingOptions options)
    {
        var program = typeof(LiveTrainingOptions).Assembly.GetType("Program", throwOnError: true)!;
        var method = program.GetMethod("ReadStageOneTeacherLedgerAsync", BindingFlags.NonPublic | BindingFlags.Static)!;
        return (Task<(string?, StrategyCommitmentLedger?)>)method.Invoke(null, new object[]
        {
            client, options, "fixture-state", new AvailabilityAwarePolicyPredictionEnvelope
            {
                RankedEventCandidates = new[] { new PolicyEventCandidatePrediction { OptionId = "museum.donate_items" } }
            }
        })!;
    }

    private static LiveTrainingOptions Options()
    {
        var root = Path.Combine(Path.GetTempPath(), "stardewai-tests", Guid.NewGuid().ToString("N"));
        return new LiveTrainingOptions
        {
            Root = root, ManifestPath = Path.Combine(root, "fixture-manifest.json"),
            RunId = "ledger-fixture", BackendUrl = "http://ledger-fixture.invalid"
        };
    }

    private sealed class LedgerHandler : HttpMessageHandler
    {
        public int Revision { get; set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Contains("/api/v1/strategy/commitments/latest?stateHash=fixture-state", request.RequestUri!.ToString());
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new StrategyCommitmentLedger
                {
                    LedgerId = "fixture-ledger", SaveId = "fixture-save", PlayerId = "1",
                    Revision = Revision, SourceStateHash = "fixture-state"
                })
            });
        }
    }
}
