using StardewAI.TransparentBridge.State;

namespace StardewAI.Backend.Tests;

public sealed class MarriageTeacherProfileTests
{
    [Theory]
    [InlineData("marriage_teacher", true, true, true)]
    [InlineData("social_future", false, true, true)]
    [InlineData("social", false, true, false)]
    [InlineData("daily", true, false, false)]
    [InlineData("full", true, true, true)]
    [InlineData("collection_teacher", true, false, true)]
    public void NewPurposeProfileDoesNotWidenExistingProfileRequirements(string profile, bool material, bool catalog, bool future)
    {
        var old = SnapshotProfileContext.Current;
        try
        {
            SnapshotProfileContext.Current = profile;
            Assert.Equal(material, SnapshotProfileContext.IncludesPersistentMaterialInventoryGraph);
            Assert.Equal(catalog, SnapshotProfileContext.IncludesNpcScheduleCatalog);
            Assert.Equal(future, SnapshotProfileContext.IncludesSocialFutureRouteDateEvidence);
            if (profile == "marriage_teacher")
            {
                Assert.False(SnapshotProfileContext.IncludesCrabPotNetwork);
                Assert.False(SnapshotProfileContext.IncludesFarmSitePreparation);
            }
        }
        finally { SnapshotProfileContext.Current = old; }
    }
}
