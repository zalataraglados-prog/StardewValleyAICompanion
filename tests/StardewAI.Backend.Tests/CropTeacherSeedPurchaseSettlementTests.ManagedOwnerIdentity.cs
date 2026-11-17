using System.Security.Cryptography;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Strategy;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NextOwnerInTheSameAuthorizedRunDoesNotReuseAnEarlierSeedMarker(bool openedBeforeAdmission)
    {
        using var f = new Fixture(otherSeeds: 39);
        using var runtime = new RuntimeFixture(f);
        runtime.LiveOptions.RunId = "run.repeated-crop";
        AddEarlierOwnerMarker(f, runtime.LiveOptions.RunId);
        using var inputs = new OwnerInputs(f);
        using var files = new OwnerFiles(f, runtime, inputs);
        if (openedBeforeAdmission) await using (var unused = await files.Open()) { Assert.Null(unused.Pending); }
        var expected = ScopedMarker(files.Options.RunId, inputs.PlanPath);
        await using (var owner = await files.Open())
        {
            var step = CropTeacherRunPlanner.Evaluate(inputs.Plan, inputs.Request, f.Before, resourceLedger: f.Ledger);
            var operation = await owner.AdmitAsync(step, f.SourcePath, f.LedgerPath, files.Native);
            Assert.Equal(expected, operation.SupportStepId);
            Assert.Equal(expected, operation.Scopes.Purchase!.MarkerRequest.PortfolioId);
            Assert.Equal(files.Options.RunId, JsonNode.Parse(File.ReadAllText(Path.Combine(files.OwnerRoot, "operation-000001-intent.json")))!["run_id"]!.GetValue<string>());
        }
        await using var reopened = await files.Open();
        Assert.Equal(expected, reopened.Pending!.SupportStepId);
        Assert.Equal(1, reopened.Pending.Sequence);
        Assert.Equal(0, runtime.Handler.Commits);
        Assert.Equal(0, runtime.Handler.LedgerReads);
    }

    [Fact]
    public async Task DifferentPinnedPlansHaveDifferentMarkersUnderTheSameRun()
    {
        using var f = new Fixture(otherSeeds: 39);
        using var runtime = new RuntimeFixture(f);
        runtime.LiveOptions.RunId = "run.repeated-crop";
        AddEarlierOwnerMarker(f, runtime.LiveOptions.RunId);
        using var firstPlan = new OwnerInputs(f);
        using var secondPlan = new OwnerInputs(f);
        using var firstFiles = new OwnerFiles(f, runtime, firstPlan);
        using var secondFiles = new OwnerFiles(f, runtime, secondPlan);
        await using var first = await firstFiles.Open();
        await using var second = await secondFiles.Open();
        var firstStep = CropTeacherRunPlanner.Evaluate(firstPlan.Plan, firstPlan.Request, f.Before, resourceLedger: f.Ledger);
        var secondStep = CropTeacherRunPlanner.Evaluate(secondPlan.Plan, secondPlan.Request, f.Before, resourceLedger: f.Ledger);
        var a = await first.AdmitAsync(firstStep, f.SourcePath, f.LedgerPath, firstFiles.Native);
        var b = await second.AdmitAsync(secondStep, f.SourcePath, f.LedgerPath, secondFiles.Native);
        Assert.NotEqual(a.SupportStepId, b.SupportStepId);
        Assert.Equal(ScopedMarker(firstFiles.Options.RunId, firstPlan.PlanPath), a.SupportStepId);
        Assert.Equal(ScopedMarker(secondFiles.Options.RunId, secondPlan.PlanPath), b.SupportStepId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LegacyOwnerIdsAndAnotherRunsHistoryKeepTheirOriginalFormat(bool otherRun)
    {
        using var f = new Fixture(otherSeeds: 39);
        using var runtime = new RuntimeFixture(f);
        runtime.LiveOptions.RunId = "run.legacy";
        if (otherRun) AddEarlierOwnerMarker(f, "run.someone-else");
        using var inputs = new OwnerInputs(f);
        using var files = new OwnerFiles(f, runtime, inputs);
        var expected = files.Options.RunId + ":owned-seed:000001";
        await using (var owner = await files.Open())
        {
            var step = CropTeacherRunPlanner.Evaluate(inputs.Plan, inputs.Request, f.Before, resourceLedger: f.Ledger);
            Assert.Equal(expected, (await owner.AdmitAsync(step, f.SourcePath, f.LedgerPath, files.Native)).SupportStepId);
        }
        await using var reopened = await files.Open();
        Assert.Equal(expected, reopened.Pending!.SupportStepId);
    }

    [Fact]
    public async Task ChangedPlanNamespaceCannotBeAdoptedOnReopen()
    {
        using var f = new Fixture(otherSeeds: 39);
        using var runtime = new RuntimeFixture(f);
        runtime.LiveOptions.RunId = "run.repeated-crop";
        AddEarlierOwnerMarker(f, runtime.LiveOptions.RunId);
        using var inputs = new OwnerInputs(f);
        using var files = new OwnerFiles(f, runtime, inputs);
        string record;
        await using (var owner = await files.Open())
        {
            var step = CropTeacherRunPlanner.Evaluate(inputs.Plan, inputs.Request, f.Before, resourceLedger: f.Ledger);
            await owner.AdmitAsync(step, f.SourcePath, f.LedgerPath, files.Native);
            record = Path.Combine(files.OwnerRoot, "operation-000001-intent.json");
        }
        var node = JsonNode.Parse(File.ReadAllText(record))!.AsObject();
        node["support_step_id"] = files.Options.RunId + ":owned-seed:plan:" + new string('0', 64) + ":000001";
        File.WriteAllText(record, node.ToJsonString());
        await Assert.ThrowsAsync<InvalidDataException>(() => files.Open());
    }

    private static string ScopedMarker(string run, string plan) => run + ":owned-seed:plan:" +
        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(plan))).ToLowerInvariant() + ":000001";

    private static void AddEarlierOwnerMarker(Fixture f, string run, FileStrategyCommitmentRepository? repository = null)
    {
        var id = run + ":owned-seed:000001";
        var request = new ReservationPortfolioCommitRequest
        {
            StateHash = f.Before.StateHash, ExpectedLedgerRevision = f.Ledger.Revision,
            PortfolioId = id, SourceDecisionId = id, GoalId = "earn_money"
        };
        var marker = repository is null
            ? new ReservationPortfolioLedgerService().Commit(f.Ledger, f.Before, request, RecordedAt)
            : repository.CommitReservationPortfolio(f.Before, request);
        Assert.True(marker.Accepted, string.Join(",", marker.Errors));
        f.Ledger = marker.Ledger!;
        Fixture.Save(f.LedgerPath, f.Ledger);
    }
}
