using System.Globalization;
using System.Text.Json;

namespace StardewAI.GoalConditionedBootstrap;

internal sealed class AcquisitionUnlockConditionEvaluator
{
    public const string UnlockAxis = "unlock";
    public const string CalendarAxis = "calendar";
    public const string StochasticAxis = "stochastic";
    public const string ResourceAxis = "resource";
    public const string LocationAxis = "location";
    public const string UnsupportedAxis = "unsupported";

    private static readonly HashSet<string> UnlockPredicates = new(
        StringComparer.Ordinal)
    {
        "IS_ISLAND_NORTH_BRIDGE_FIXED",
        "PLAYER_HAS_MAIL",
        "PLAYER_SPECIAL_ORDER_ACTIVE",
        "PLAYER_SPECIAL_ORDER_RULE_ACTIVE",
        "PLAYER_STAT"
    };

    private readonly UnlockSnapshotState state;

    public AcquisitionUnlockConditionEvaluator(JsonElement snapshot)
    {
        state = UnlockSnapshotState.Read(snapshot);
    }

    public static string Classify(string condition)
    {
        var name = PredicateName(condition);
        if (UnlockPredicates.Contains(name))
            return UnlockAxis;
        return name switch
        {
            "DAYS_PLAYED" or "IS_FESTIVAL_DAY" or
                "IS_PASSIVE_FESTIVAL_OPEN" => CalendarAxis,
            "RANDOM" or "SYNCED_CHOICE" or "SYNCED_RANDOM" =>
                StochasticAxis,
            "PLAYER_HAS_ITEM" => ResourceAxis,
            "PLAYER_LOCATION_NAME" => LocationAxis,
            _ => UnsupportedAxis
        };
    }

    public AcquisitionUnlockConditionEvaluation Evaluate(string condition)
    {
        var tokens = Tokens(condition);
        if (tokens.Length == 0)
            return Blocked(condition, string.Empty, false, string.Empty,
                "empty_dynamic_condition");

        var negated = tokens[0].StartsWith('!');
        var name = negated ? tokens[0][1..] : tokens[0];
        if (!UnlockPredicates.Contains(name))
            return Blocked(condition, name, negated, string.Empty,
                "condition_not_owned_by_unlock_axis");
        if (!state.Available)
            return Blocked(condition, name, negated, PlayerSelector(tokens),
                state.BlockingReason);

        return name switch
        {
            "IS_ISLAND_NORTH_BRIDGE_FIXED" =>
                EvaluateIslandBridge(condition, tokens, negated),
            "PLAYER_HAS_MAIL" => EvaluatePlayerPredicate(
                condition, tokens, negated, 3, 4, EvaluateMail),
            "PLAYER_SPECIAL_ORDER_ACTIVE" => EvaluatePlayerPredicate(
                condition, tokens, negated, 3, int.MaxValue,
                (player, values) => values.Any(player.ActiveSpecialOrderIds.Contains)),
            "PLAYER_SPECIAL_ORDER_RULE_ACTIVE" => EvaluatePlayerPredicate(
                condition, tokens, negated, 3, int.MaxValue,
                (player, values) => values.Any(player.ActiveSpecialOrderRules.Contains)),
            "PLAYER_STAT" => EvaluatePlayerPredicate(
                condition, tokens, negated, 4, 5, EvaluateStat),
            _ => Blocked(condition, name, negated, PlayerSelector(tokens),
                "unsupported_unlock_predicate")
        };
    }

    private AcquisitionUnlockConditionEvaluation EvaluateIslandBridge(
        string condition,
        string[] tokens,
        bool negated)
    {
        if (tokens.Length != 1)
            return Blocked(condition, PredicateName(condition), negated,
                string.Empty, "invalid_island_bridge_condition_arguments");
        if (!state.IslandNorthBridgeFixed.HasValue)
            return Blocked(condition, "IS_ISLAND_NORTH_BRIDGE_FIXED", negated,
                string.Empty, "island_north_bridge_state_unavailable");
        return Resolved(
            condition,
            "IS_ISLAND_NORTH_BRIDGE_FIXED",
            negated,
            string.Empty,
            state.IslandNorthBridgeFixed.Value,
            new[]
            {
                "world_progress.game_state_query_unlock_state." +
                "island_north_bridge_fixed"
            });
    }

    private AcquisitionUnlockConditionEvaluation EvaluatePlayerPredicate(
        string condition,
        string[] tokens,
        bool negated,
        int minimumTokens,
        int maximumTokens,
        Func<UnlockPlayerState, string[], bool?> predicate)
    {
        var name = PredicateName(condition);
        var selector = PlayerSelector(tokens);
        if (tokens.Length < minimumTokens || tokens.Length > maximumTokens ||
            selector.Length == 0)
        {
            return Blocked(condition, name, negated, selector,
                "invalid_" + name.ToLowerInvariant() + "_arguments");
        }
        if (!TryResolvePlayers(selector, out var players, out var all, out var error))
            return Blocked(condition, name, negated, selector, error);

        var values = tokens.Skip(2).ToArray();
        var results = players.Select(player => predicate(player, values)).ToArray();
        if (results.Any(result => !result.HasValue))
        {
            return Blocked(condition, name, negated, selector,
                "invalid_" + name.ToLowerInvariant() + "_arguments");
        }
        var native = all
            ? results.All(result => result!.Value)
            : results.Any(result => result!.Value);
        return Resolved(condition, name, negated, selector, native,
            players.Select(player =>
                    "world_progress.game_state_query_unlock_state.players[" +
                    player.PlayerId + "]")
                .ToArray());
    }

    private static bool? EvaluateMail(
        UnlockPlayerState player,
        string[] values)
    {
        if (values.Length is not (1 or 2))
            return null;
        var mailId = values[0];
        var type = values.Length == 2
            ? values[1].ToLowerInvariant()
            : "any";
        return type switch
        {
            "mailbox" => player.Mailbox.Contains(mailId),
            "tomorrow" => player.MailForTomorrow.Contains(mailId),
            "received" => player.MailReceived.Contains(mailId),
            "any" => player.Mailbox.Contains(mailId) ||
                player.MailForTomorrow.Contains(mailId) ||
                player.MailForTomorrow.Contains(mailId + "%&NL&%") ||
                player.MailReceived.Contains(mailId),
            _ => null
        };
    }

    private static bool? EvaluateStat(
        UnlockPlayerState player,
        string[] values)
    {
        if (values.Length is not (2 or 3) ||
            !int.TryParse(values[1], NumberStyles.Integer,
                CultureInfo.InvariantCulture, out var minimum) ||
            (values.Length == 3 && !int.TryParse(values[2],
                NumberStyles.Integer, CultureInfo.InvariantCulture,
                out _)))
        {
            return null;
        }
        var maximum = values.Length == 3
            ? int.Parse(values[2], CultureInfo.InvariantCulture)
            : int.MaxValue;
        var value = player.Stats.TryGetValue(values[0], out var stat)
            ? stat
            : 0;
        return value >= minimum && value <= maximum;
    }

    private bool TryResolvePlayers(
        string selector,
        out UnlockPlayerState[] players,
        out bool requireAll,
        out string error)
    {
        players = Array.Empty<UnlockPlayerState>();
        requireAll = false;
        error = string.Empty;
        if (selector.Equals("Target", StringComparison.OrdinalIgnoreCase))
        {
            error = "target_player_context_unavailable";
            return false;
        }
        if (selector.Equals("Any", StringComparison.OrdinalIgnoreCase) ||
            selector.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            players = state.Players;
            requireAll = selector.Equals("All", StringComparison.OrdinalIgnoreCase);
            return players.Length > 0;
        }

        var id = selector.Equals("Current", StringComparison.OrdinalIgnoreCase)
            ? state.CurrentPlayerId
            : selector.Equals("Host", StringComparison.OrdinalIgnoreCase)
                ? state.HostPlayerId
                : long.TryParse(selector, NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out _)
                    ? selector
                    : string.Empty;
        if (id.Length == 0)
        {
            error = "invalid_player_selector:" + selector;
            return false;
        }
        players = state.Players.Where(player => player.PlayerId == id).ToArray();
        if (players.Length != 1)
        {
            error = "selected_player_state_unavailable:" + selector;
            return false;
        }
        return true;
    }

    private static AcquisitionUnlockConditionEvaluation Resolved(
        string condition,
        string name,
        bool negated,
        string selector,
        bool native,
        string[] evidencePaths)
    {
        var matches = negated ? !native : native;
        return new AcquisitionUnlockConditionEvaluation(
            condition,
            name,
            negated,
            selector,
            matches ? "resolved_match" : "resolved_miss",
            native,
            matches,
            evidencePaths,
            null);
    }

    private static AcquisitionUnlockConditionEvaluation Blocked(
        string condition,
        string name,
        bool negated,
        string selector,
        string reason) => new(
            condition,
            name,
            negated,
            selector,
            "blocked",
            null,
            null,
            Array.Empty<string>(),
            reason);

    private static string PredicateName(string condition)
    {
        var tokens = Tokens(condition);
        if (tokens.Length == 0)
            return string.Empty;
        return tokens[0].StartsWith('!') ? tokens[0][1..] : tokens[0];
    }

    private static string PlayerSelector(string[] tokens) =>
        tokens.Length > 1 ? tokens[1] : string.Empty;

    private static string[] Tokens(string condition) =>
        (condition ?? string.Empty).Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries |
            StringSplitOptions.TrimEntries);
}
