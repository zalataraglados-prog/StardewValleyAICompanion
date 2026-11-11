using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Fact]
    public void BackendTestHostUsesFixedJsonInputsWithoutConfigurationWatchers()
    {
        using var files = new Fixture();
        using var runtime = new RuntimeFixture(files);
        var configuration = Assert.IsAssignableFrom<IConfigurationRoot>(runtime.HostConfiguration);
        var jsonProviders = configuration.Providers.OfType<JsonConfigurationProvider>().ToArray();
        Assert.NotEmpty(jsonProviders);
        Assert.All(jsonProviders, provider => Assert.False(provider.Source.ReloadOnChange));
        Assert.Equal("false", Environment.GetEnvironmentVariable("DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE"));
    }
}
