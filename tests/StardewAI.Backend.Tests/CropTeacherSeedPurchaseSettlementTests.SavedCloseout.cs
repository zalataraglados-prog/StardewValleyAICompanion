using System.Reflection;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    private static Task<CropTeacherManagedSavedCloseoutEvidence> ReadSavedCloseout(CropTeacherManagedSeedOwner owner, string manifest) =>
        (Task<CropTeacherManagedSavedCloseoutEvidence>)typeof(CropTeacherManagedSeedOwner)
            .GetMethod("ReadSavedTerminalCloseoutAsync", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(owner, new object[] { manifest })!;

    [Fact]
    public async Task SavedCloseoutDoesNotPromoteAcceptedLoadWithoutOriginalTerminalDeposits()
    {
        using var f = new SavedReadbackOwnerFixture();
        await using var owner = await f.Night.Open();
        await f.CompleteNight(owner);
        var readback = await f.Admit(owner);
        await f.Run(owner, readback);
        var output = Path.Combine(f.Night.Options.EvidenceDirectory, "not-a-terminal-proof");
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => ReadSavedCloseout(owner, Path.Combine(output, "terminal-evidence.json")));
        Assert.Contains("completed original terminal deposits", error.Message);
        Assert.False(Directory.Exists(output)); Assert.Equal(4, f.Night.Shared.Runtime.Handler.Commits);
    }

    [Fact]
    public async Task SavedCloseoutDoesNotPromoteFinishedDepositWithoutAcceptedSaveAndLoad()
    {
        var f = new TerminalOutcomeFixture(quantity: 1);
        using var host = f.Shared.Runtime;
        await using var owner = await f.Shared.Files.Open();
        var deposit = await AdmitTerminal(owner, f);
        await owner.ExecuteOriginalTerminalAsync(deposit, host.Client, () => Task.FromResult(EmitTerminal(f)));
        await RebindTerminal(owner, deposit, f);
        Assert.Equal(1, owner.AcceptedTerminalDepositCount);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => ReadSavedCloseout(owner,
            Path.Combine(f.Shared.Files.OwnerRoot, "absent", "terminal-evidence.json")));
        Assert.Contains("accepted load", error.Message); Assert.Equal(1, host.Handler.Commits);
    }
}
