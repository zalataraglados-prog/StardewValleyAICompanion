using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StardewAI.Testing;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherRuntimeInstallationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OriginalHttpEndpointsInstallAndRecoverWithoutChangingExecutors(bool loseReplies)
    {
        using var files = new Files();
        using var fixture = new MoneyCropTeacherFixture("474", 3);
        var decision = Prepare(fixture, files);
        await using var app = new WebApplicationFactory<global::Program>().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IStrategyCommitmentRepository>();
                services.AddSingleton<IStrategyCommitmentRepository>(new FileStrategyCommitmentRepository(files.Repository));
            }));
        using var setup = app.CreateClient();
        var store = app.Services.GetRequiredService<StateStore>();
        store.Snapshots[fixture.SourceSnapshot.StateHash] = fixture.SourceSnapshot;
        using var transport = new InstallationLossHandler(app.Server.CreateHandler(), loseReplies);
        using var client = new HttpClient(transport);
        Assert.Equal(0, await Install(client, decision, files.Output));
        Assert.Equal(1, transport.CropPosts);
        Assert.Equal(1, transport.ResourcePosts);
        Assert.Equal(loseReplies ? 1 : 0, transport.Lookups);
        Assert.Equal(4, new FileStrategyCommitmentRepository(files.Repository).Get(fixture.SourceSnapshot).Revision);
        Assert.Equal(2, store.Audit.Count(row => row.EventType is "CropStrategyCommitmentUpserted" or "ReservationPortfolioCommitted"));
    }

    private sealed class InstallationLossHandler(HttpMessageHandler inner, bool loseReplies) : DelegatingHandler(inner)
    {
        public int CropPosts { get; private set; }
        public int ResourcePosts { get; private set; }
        public int Lookups { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            var crop = request.RequestUri!.AbsolutePath.EndsWith("/crops/upsert", StringComparison.Ordinal);
            var resources = request.RequestUri.AbsolutePath.EndsWith("/reservation-portfolios/commit", StringComparison.Ordinal);
            if (crop) CropPosts++;
            if (resources) ResourcePosts++;
            if (request.RequestUri.AbsolutePath.EndsWith("/lookup-mutation", StringComparison.Ordinal)) Lookups++;
            var reply = await base.SendAsync(request, token);
            if (loseReplies && (crop || resources))
            {
                Assert.True(reply.IsSuccessStatusCode);
                reply.Dispose();
                throw new HttpRequestException("Original endpoint committed; transport lost reply.");
            }
            return reply;
        }
    }
}
