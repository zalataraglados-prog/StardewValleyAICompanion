using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StardewAI.Contracts.Training;
using StardewAI.Core.Training;
using StardewAI.Testing;

namespace StardewAI.Backend.Tests;

public sealed class StructuredPolicySupervisionEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> factory;
    public StructuredPolicySupervisionEndpointTests(WebApplicationFactory<Program> factory) => this.factory = factory;

    [Fact]
    public async Task BackendRejectsSelectedOnlyEvenWithRehashedManifestWithoutChangingCheckpoint()
    {
        var root = Root();
        var dataset = Dataset(root);
        PolicySupervisionFixture.StripTeacherFromTrainingPartition(dataset);
        var checkpoint = Path.Combine(root, "checkpoint.json");
        File.WriteAllText(checkpoint, "unchanged-checkpoint");
        var before = File.ReadAllBytes(dataset.ManifestPath);
        var response = await factory.CreateClient().PostAsJsonAsync("/api/v1/training/structured/train",
            new StructuredPolicyTrainingRequest { DatasetManifestPath = dataset.ManifestPath, CheckpointPath = checkpoint });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("independent_teacher_preference_required", await response.Content.ReadAsStringAsync());
        Assert.Equal("unchanged-checkpoint", File.ReadAllText(checkpoint));
        Assert.Equal(before, File.ReadAllBytes(dataset.ManifestPath));
    }

    [Fact]
    public async Task BackendDoesNotTrustAClaimedTeacherIdentityWithoutItsConfiguredVerifier()
    {
        var root = Root();
        var dataset = Dataset(root);
        var checkpoint = Path.Combine(root, "checkpoint.json");
        var response = await factory.CreateClient().PostAsJsonAsync("/api/v1/training/structured/train",
            new StructuredPolicyTrainingRequest { DatasetManifestPath = dataset.ManifestPath, CheckpointPath = checkpoint });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("teacher_supervision_identity_mismatch", await response.Content.ReadAsStringAsync());
        Assert.False(File.Exists(checkpoint));
    }

    [Fact]
    public async Task BackendUsesTheConfiguredIndependentReplayPortForValidExplicitPairs()
    {
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IPolicyTeacherSupervisionReplayVerifier>();
            services.AddSingleton<IPolicyTeacherSupervisionReplayVerifier>(PolicySupervisionFixture.Verifier);
        }));
        var root = Root();
        var dataset = Dataset(root);
        var response = await host.CreateClient().PostAsJsonAsync("/api/v1/training/structured/train",
            new StructuredPolicyTrainingRequest
            {
                DatasetManifestPath = dataset.ManifestPath, CheckpointPath = Path.Combine(root, "checkpoint.json")
            });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<StructuredPolicyTrainingResult>();
        Assert.NotNull(result);
        Assert.Equal(1, result.Checkpoint.Training.TrainPairs);
        Assert.Equal(PolicyTrainingObjectives.IndependentTeacherPairwise, result.Checkpoint.Training.SupervisionObjective);
    }

    [Fact]
    public async Task ActualLiveTrainingMethodRejectsObservationWithoutWritingFormalArtifacts()
    {
        var root = Root();
        var checkpoint = Path.Combine(root, "checkpoint.json");
        var manifest = Path.Combine(root, "formal-manifest.json");
        File.WriteAllText(checkpoint, "unchanged-checkpoint");
        File.WriteAllText(manifest, "unchanged-formal-manifest");
        var options = new LiveTrainingOptions
        {
            Root = root, RunId = "fixture-run", PolicyCheckpointPath = checkpoint,
            ManifestPath = manifest, RequireStructuredPolicy = true, SkipTraining = false,
            TrainEvery = 1, MaxAttempts = 1
        };
        Directory.CreateDirectory(Path.GetDirectoryName(options.PolicyTrajectoryDatasetPath)!);
        File.WriteAllText(options.PolicyTrajectoryDatasetPath,
            JsonSerializer.Serialize(PolicySupervisionFixture.Observation()) + "\n");
        var program = typeof(LiveTrainingOptions).Assembly.GetType("Program", throwOnError: true)!;
        var method = program.GetMethod("TrainIfNeededAsync", BindingFlags.Static | BindingFlags.NonPublic)!;
        using var http = new HttpClient();
        var task = (Task)method.Invoke(null, new object[] { http, options, 1 })!;
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => task);
        Assert.Contains("independent_teacher_preference_required", error.Message);
        Assert.Equal("unchanged-checkpoint", File.ReadAllText(checkpoint));
        Assert.Equal("unchanged-formal-manifest", File.ReadAllText(manifest));
        Assert.False(File.Exists(Path.Combine(options.StructuredPolicyDatasetRoot, "policy-dataset-manifest.json")));
    }

    [Theory]
    [InlineData("StardewAI.PolicyDataset")]
    [InlineData("StardewAI.PolicyModel")]
    public async Task ActualCliRejectsSelectedOnlyAndPreservesExistingOutput(string project)
    {
        var root = Root();
        var dataset = Dataset(root);
        PolicySupervisionFixture.StripTeacherFromTrainingPartition(dataset);
        var output = Path.Combine(root, "cli-output");
        Directory.CreateDirectory(output);
        var checkpoint = Path.Combine(output, "checkpoint.json");
        var manifest = Path.Combine(output, "policy-dataset-manifest.json");
        File.WriteAllText(checkpoint, "unchanged-checkpoint");
        File.WriteAllText(manifest, "unchanged-manifest");
        var repository = new DirectoryInfo(AppContext.BaseDirectory);
        while (repository is not null && !File.Exists(Path.Combine(repository.FullName, "StardewValleyAICompanion.sln")))
            repository = repository.Parent;
        Assert.NotNull(repository);
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var dll = Path.Combine(repository.FullName, "tools", project, "bin", configuration, "net8.0", project + ".dll");
        Assert.True(File.Exists(dll), "Build policy CLI entry points before running their integration tests: " + dll);
        var start = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true
        };
        start.ArgumentList.Add(dll);
        var arguments = project == "StardewAI.PolicyDataset"
            ? new[] { "--input", dataset.Manifest.Partitions.Single(value => value.Partition == "train").Path,
                "--output-root", output, "--no-horizon-observations" }
            : new[] { "--dataset-manifest", dataset.ManifestPath, "--checkpoint", checkpoint };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch { if (!process.HasExited) process.Kill(entireProcessTree: true); throw; }
        await stdout;
        var errorText = await stderr;
        Assert.Equal(2, process.ExitCode);
        Assert.Contains("\"status\":\"rejected\"", errorText);
        Assert.Contains("independent_teacher_preference_required", errorText);
        Assert.Equal("unchanged-checkpoint", File.ReadAllText(checkpoint));
        Assert.Equal("unchanged-manifest", File.ReadAllText(manifest));
    }

    private static PolicyDatasetBuildResult Dataset(string root) => PolicySupervisionFixture.Dataset(root,
        PolicySupervisionFixture.Attach(PolicySupervisionFixture.Observation(), "gift", "talk"));

    private static string Root()
    {
        var root = Path.Combine(Path.GetTempPath(), "stardewai-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }
}
