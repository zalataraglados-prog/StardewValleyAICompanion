namespace StardewAI.KnowledgeCompiler;

internal static class WikiVerificationCatalog
{
    public static IReadOnlyList<WikiVerificationSource> Sources { get; } = new[]
    {
        Source(
            "grandpa_evaluation",
            "https://stardewvalleywiki.com/mediawiki/index.php?title=Grandpa&oldid=182923",
            "Grandpa evaluation timing, all 21 available points, milestone thresholds, and four-candle distinction",
            "runtime/decompile must remain authoritative for exact thresholds and event ordering"),
        Source(
            "shop_schedules",
            "https://stardewvalleywiki.com/mediawiki/index.php?title=Shop_Schedules&oldid=188150",
            "shop opening windows, regular closures, festival closures, and Key To The Town exceptions",
            "live Data/Shops, NPC state, location gates, festival state, and native shop builders override this table"),
        Source(
            "shop_data_format",
            "https://stardewvalleywiki.com/mediawiki/index.php?title=Modding:Shops&oldid=192231",
            "Data/Shops stock, price, trade, condition, stock-limit, and OpenShop action structure",
            "page revision postdates 1.6.15 and is format corroboration only"),
        Source(
            "building_data_format",
            "https://stardewvalleywiki.com/mediawiki/index.php?title=Modding:Buildings&oldid=191928",
            "Data/Buildings construction cost, materials, days, placement footprint, entrance, and condition structure",
            "page revision postdates 1.6.15 and is format corroboration only"),
        Source(
            "event_data_format",
            "https://stardewvalleywiki.com/mediawiki/index.php?title=Modding:Event_data&oldid=191688",
            "event keys, preconditions, game-state-query preconditions, commands, and extensible handlers",
            "registered handlers and executable semantics must be read from the installed assembly"),
        Source(
            "festival_data_format",
            "https://stardewvalleywiki.com/mediawiki/index.php?title=Modding:Festival_data&oldid=189122",
            "Data/Festivals dates, entry windows, setup/main scripts, shops, year variants, and hardcoded exceptions",
            "explicitly incomplete for hardcoded festival logic; decompile evidence is mandatory"),
        Source(
            "recipe_data_format",
            "https://stardewvalleywiki.com/mediawiki/index.php?title=Modding:Recipe_data&oldid=193377",
            "1.6.15 cooking/crafting recipe records, ingredients, output, yield, unlock rules, and hardcoded exceptions",
            "runtime export is authoritative; hardcoded unlock exceptions require decompiled evidence"),
        Source(
            "game_state_queries",
            "https://stardewvalleywiki.com/mediawiki/index.php?title=Modding:Game_state_queries&oldid=193553",
            "query grammar, built-in query families, target contexts, negation, randomization, and item predicates",
            "revision includes later-version material; only handlers present in the indexed 1.6.15 assembly are admissible")
    };

    private static WikiVerificationSource Source(string id, string url, string scope, string limitation) =>
        new(id, url, "Stardew Valley Wiki", "2026-07-19", "secondary_only", scope, limitation);
}

internal sealed record WikiVerificationSource(
    string Id,
    string Url,
    string Publisher,
    string CheckedOn,
    string AuthorityLevel,
    string CorroborationScope,
    string Limitation);
