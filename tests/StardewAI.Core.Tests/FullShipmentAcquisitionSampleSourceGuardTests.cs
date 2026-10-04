namespace StardewAI.Core.Tests;

public sealed class FullShipmentAcquisitionSampleSourceGuardTests
{
    [Fact]
    public void BerryBushSampleReusesNativeBushAcquisitionChain()
    {
        var source = ReadRepositoryFile(
            "scripts",
            "Invoke-RuntimeFullShipmentSapPrefixSmoke.ps1");

        Assert.Contains(
            "\"berry_bush_harvest_sample\"",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"berry_bush_harvest_sample\" { \"full_shipment:item:296\" }",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"berry_bush_harvest_sample\" { \"(O)296\" }",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"berry_bush_harvest_sample\" { \"foraging.harvest_bushes\" }",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "option_id = \"debug.setup_forage_source_fixture\"",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "fixture_bush_profile = \"berry_standard\"",
            source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void SampleDateDoesNotRelaxNativeSapRecurrenceRoot()
    {
        var source = ReadRepositoryFile(
            "scripts",
            "Invoke-RuntimeFullShipmentSapPrefixSmoke.ps1");

        Assert.Contains(
            "$sampleProofOnly = $Scenario -ne \"sap_prefix\"",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "if (-not $sampleProofOnly -and $initialTotalDay -ne 0)",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"--target-total-day\", ([string]$initialTotalDay)",
            source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ResumeProofTakesSampleIdentityFromVerifiedBinding()
    {
        var source = ReadRepositoryFile(
            "scripts",
            "Resume-RuntimeFullShipmentAcquisitionProof.ps1");

        Assert.Contains(
            "$requirementId = [string]$binding.requirement_id",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "$qualifiedItemId = [string]$binding.qualified_item_id",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "full_shipment:item:296|(O)296|native_bush_shake",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "scenario = $scenario",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "requirement_id = $requirementId",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "qualified_item_id = $qualifiedItemId",
            source,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "requirement_id = \"full_shipment:item:24\"",
            source,
            StringComparison.Ordinal);
    }

    private static string ReadRepositoryFile(params string[] segments)
    {
        var directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        while (directory is not null &&
            !File.Exists(Path.Combine(
                directory.FullName,
                "StardewValleyAICompanion.sln")))
        {
            directory = directory.Parent;
        }

        return File.ReadAllText(Path.Combine(
            directory?.FullName ?? throw new InvalidOperationException(
                "Cannot find repository root."),
            Path.Combine(segments)));
    }
}
