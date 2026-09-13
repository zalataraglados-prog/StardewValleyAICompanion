using System.Collections.Generic;
using System.Linq;

namespace StardewAI.Contracts.Strategy;

public static class NativeShopCurrencies
{
    public const int Money = 0;
    public const int StarTokens = 1;
    public const int ClubCoins = 2;
    public const int QiGems = 4;

    private static readonly NativeShopCurrencyDefinition[] Values =
    {
        new NativeShopCurrencyDefinition(Money, "money"),
        new NativeShopCurrencyDefinition(StarTokens, "star_tokens"),
        new NativeShopCurrencyDefinition(ClubCoins, "club_coins"),
        new NativeShopCurrencyDefinition(QiGems, "qi_gems")
    };

    public static IReadOnlyList<NativeShopCurrencyDefinition> All => Values;

    public static bool TryGetKey(int currencyId, out string key)
    {
        var definition = Values.SingleOrDefault(row => row.Id == currencyId);
        key = definition?.Key ?? string.Empty;
        return definition is not null;
    }
}

public sealed class NativeShopCurrencyDefinition
{
    public NativeShopCurrencyDefinition(int id, string key)
    {
        Id = id;
        Key = key;
    }

    public int Id { get; }
    public string Key { get; }
}
