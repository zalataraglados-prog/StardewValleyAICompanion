namespace StardewAI.GoalConditionedBootstrap;

internal static class MoneyRevenueCropResourceReservationCommands
{
    internal static void Write(Arguments options, Action<string, object> write, string mode)
    {
        var output = Path.GetFullPath(options.Required("output"));
        Require(output.EndsWith(".json", StringComparison.OrdinalIgnoreCase) &&
                !File.Exists(output) && !Directory.Exists(output),
            "Crop resource reservation output must be a new JSON file.");
        var receipt = mode == "build" ?
            MoneyRevenueCropResourceReservationBuilder.Build(options.Required("decision")) :
            MoneyRevenueCropResourceReservationBuilder.Verify(options.Required("receipt"));
        write(output, mode == "ledger" ? receipt.ReservationResult.Ledger! : receipt);
    }
}
