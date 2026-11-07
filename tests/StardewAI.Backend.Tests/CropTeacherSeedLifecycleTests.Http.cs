using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StardewAI.Contracts.Strategy;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedLifecycleTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActualOriginalEndpointsCloseReleaseAndReadBackWithoutNativeActions(bool completion)
    {
        using var f = new Fixture(due: completion);
        var scope = f.Open();
        using var app = new WebApplicationFactory<global::Program>().WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IStrategyCommitmentRepository>();
            services.AddSingleton<IStrategyCommitmentRepository>(f.Repository);
        }));
        using var client = app.CreateClient();
        app.Services.GetRequiredService<StateStore>().Snapshots[f.Snapshot.StateHash] = f.Snapshot;
        var latest = "/api/v1/strategy/commitments/latest?stateHash=" + f.Snapshot.StateHash;
        StardewAI.GoalConditionedBootstrap.CropTeacherSeedRelease release;
        if (completion) release = scope.VerifyCompletionReadback((await client.GetFromJsonAsync<StrategyCommitmentLedger>(latest))!);
        else
        {
            var cancel = scope.BeginCancellation("explicit_http_cancel");
            using var reply = await client.PostAsJsonAsync("/api/v1/strategy/commitments/crops/" + Uri.EscapeDataString(f.Id) + "/cancel", cancel.Request);
            Assert.True(reply.IsSuccessStatusCode, await reply.Content.ReadAsStringAsync());
            release = cancel.VerifyReply((await reply.Content.ReadFromJsonAsync<StrategyCommitmentMutationResult>())!);
        }
        using var released = await client.PostAsJsonAsync("/api/v1/strategy/commitments/reservation-portfolios/commit", release.Request);
        Assert.True(released.IsSuccessStatusCode, await released.Content.ReadAsStringAsync());
        release.VerifyReply((await released.Content.ReadFromJsonAsync<ReservationPortfolioCommitResult>())!);
        var final = release.VerifyReadback((await client.GetFromJsonAsync<StrategyCommitmentLedger>(latest))!);
        Assert.Equal(completion ? "completed" : "cancelled", final.CropPlantingCommitments.Single().Status);
        Assert.All(final.MaterialReservations.Where(row => row.SourceDecisionId == "other-root"), row => Assert.Equal("active", row.Status));
    }
}
