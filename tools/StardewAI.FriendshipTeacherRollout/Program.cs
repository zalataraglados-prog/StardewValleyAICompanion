using System.Text.Json;
using StardewAI.FriendshipTeacherRollout;

try
{
    var options = RolloutOptions.Parse(args);
    var coordinator = new FriendshipTeacherRolloutCoordinator(options);
    var summary = await coordinator.RunAsync(CancellationToken.None);
    Console.WriteLine(JsonSerializer.Serialize(summary, RolloutJson.Options));
    Environment.ExitCode = summary.Status is "goal_satisfied" or
        "bounded_evidence_complete"
            ? 0
            : 2;
}
catch (Exception error)
{
    Console.Error.WriteLine(error.GetType().Name + ": " + error.Message);
    Environment.ExitCode = 1;
}
