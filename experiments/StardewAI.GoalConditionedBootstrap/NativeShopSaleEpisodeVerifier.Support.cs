using System.Globalization;
namespace StardewAI.GoalConditionedBootstrap;

internal static partial class NativeShopSaleEpisodeVerifier
{
    private static bool TrySingleNonEmpty(
        IReadOnlyDictionary<string, string[]> parameters,
        string name,
        out string value)
    {
        value = string.Empty;
        if (!parameters.TryGetValue(name, out var values) ||
            values.Length != 1 ||
            string.IsNullOrWhiteSpace(values[0]))
        {
            return false;
        }
        value = values[0];
        return true;
    }

    private static bool TrySingleNonNegativeInt(
        IReadOnlyDictionary<string, string[]> parameters,
        string name,
        out int value) =>
        TrySingleInt(parameters, name, out value) && value >= 0;

    private static bool TrySinglePositiveInt(
        IReadOnlyDictionary<string, string[]> parameters,
        string name,
        out int value) =>
        TrySingleInt(parameters, name, out value) && value > 0;

    private static bool TrySingleInt(
        IReadOnlyDictionary<string, string[]> parameters,
        string name,
        out int value)
    {
        value = 0;
        return parameters.TryGetValue(name, out var values) &&
            values.Length == 1 &&
            int.TryParse(
                values[0],
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out value);
    }

    private static string RequiredFile(string path, string label)
    {
        var fullPath = string.IsNullOrWhiteSpace(path)
            ? throw new InvalidDataException(label + " path is required.")
            : Path.GetFullPath(path);
        return File.Exists(fullPath)
            ? fullPath
            : throw new FileNotFoundException(label + " is missing.", fullPath);
    }

    private sealed record ShopSaleCommand(
        string ShopId,
        string QualifiedItemId,
        int SlotIndex,
        int Quantity,
        int UnitPrice);
}
