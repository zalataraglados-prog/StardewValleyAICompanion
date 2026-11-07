using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;

static partial class Program
{
    private static async Task<StrategyLedgerEvidence> CloseCropTeacherSeedLifecycleAsync(HttpClient http, LiveTrainingOptions options,
        CropTeacherSeedLifecycleSupport scope, string output, string mode, string reason = "")
    {
        var origin = CropTeacherSeedLifecycleRuntime.NormalizeBackendOrigin(options.BackendUrl);
        var portfolioOptions = new LiveTrainingOptions
        {
            BackendUrl = origin, ManifestPath = Path.Combine(options.RunDir, "lifecycle-intent.json")
        };
        return await CropTeacherSeedLifecycleRuntime.RunAsync(http, new(origin, output, options.RunId, mode, reason), scope,
            (diagnostic, request) => PostJournaledPortfolioCommitAsync(http, portfolioOptions, diagnostic, request),
            (endpoint, raw) => PostJsonStringAsync(http, endpoint, raw), WritePortfolioJournalFileAsync);
    }

    private static async Task<int> CloseCropSeedReservationsAsync(string[] arguments, HttpClient http)
    {
        var names = new[] { "--snapshot", "--ledger", "--commitment-id", "--release-step-id", "--run-id", "--mode", "--backend-url", "--output-dir" };
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 0; index < arguments.Length; index += 2)
            if (index + 1 >= arguments.Length || !names.Append("--reason").Contains(arguments[index], StringComparer.Ordinal) ||
                !values.TryAdd(arguments[index], arguments[index + 1]) || string.IsNullOrWhiteSpace(arguments[index + 1]))
                throw new ArgumentException("Crop lifecycle requires each named argument once, with an explicit cancellation-only reason.");
        if (names.Any(name => !values.ContainsKey(name))) throw new ArgumentException("Crop lifecycle arguments are incomplete.");
        var scope = CropTeacherSeedLifecycleSupport.Open(values["--snapshot"], values["--ledger"], values["--commitment-id"], values["--release-step-id"]);
        var output = Path.GetFullPath(values["--output-dir"]);
        var options = new LiveTrainingOptions { BackendUrl = values["--backend-url"], RunId = values["--run-id"], ManifestPath = Path.Combine(output, "lifecycle-intent.json") };
        var final = await CloseCropTeacherSeedLifecycleAsync(http, options, scope, output, values["--mode"], values.GetValueOrDefault("--reason", ""));
        Console.WriteLine("Crop seed allocations closed/released; no gameplay or training performed: " + final.Path);
        return 0;
    }
}
