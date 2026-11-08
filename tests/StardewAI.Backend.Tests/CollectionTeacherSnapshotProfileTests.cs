using StardewAI.TransparentBridge.State;

namespace StardewAI.Backend.Tests;

public sealed class CollectionTeacherSnapshotProfileTests
{
    [Theory]
    [InlineData("full", true)]
    [InlineData("daily", true)]
    [InlineData("collection_teacher", true)]
    [InlineData("light", false)]
    [InlineData("machine", false)]
    [InlineData("marriage_teacher", false)]
    public void HayResourceReadIsLimitedToRequestedPlanningProfiles(string profile, bool requested)
    {
        var previous = SnapshotProfileContext.Current;
        try { SnapshotProfileContext.Current = profile; Assert.Equal(requested, SnapshotProfileContext.IncludesHayStorage); }
        finally { SnapshotProfileContext.Current = previous; }
    }

    [Theory]
    [InlineData("fishing", true, false, false)]
    [InlineData("collection_teacher", true, true, false)]
    [InlineData("full", true, true, true)]
    [InlineData("social_future", false, true, true)]
    public void LocalTeacherAddsNativeDateRoutesWithoutEnablingNpcScheduleCatalog(
        string profile, bool inventory, bool dateRoutes, bool npcCatalog)
    {
        var previous = SnapshotProfileContext.Current;
        try
        {
            SnapshotProfileContext.Current = profile;
            Assert.Equal(inventory, SnapshotProfileContext.IncludesPersistentMaterialInventoryGraph);
            Assert.Equal(dateRoutes, SnapshotProfileContext.IncludesSocialFutureRouteDateEvidence);
            Assert.Equal(npcCatalog, SnapshotProfileContext.IncludesNpcScheduleCatalog);
            Assert.Equal(profile is "fishing" or "collection_teacher" or "full", SnapshotProfileContext.IncludesCrabPotNetwork);
        }
        finally { SnapshotProfileContext.Current = previous; }
    }

    [Fact]
    public void NewProfileReusesTheOriginalFishingDomains()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "StardewValleyAICompanion.sln"))) root = root.Parent;
        Assert.NotNull(root);
        var entry = File.ReadAllText(Path.Combine(root!.FullName, "src", "StardewAI.TransparentBridge", "ModEntry.cs"));
        Assert.Contains("return AllowedDomainsForProfile(\"fishing\");", entry);
        var backend = File.ReadAllText(Path.Combine(root.FullName, "src", "StardewAI.Backend", "Program.cs"));
        Assert.Contains("string.Equals(profile, \"collection_teacher\", StringComparison.OrdinalIgnoreCase)", backend);
    }
}
