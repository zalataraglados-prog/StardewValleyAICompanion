using System.Globalization;
using System.Xml.Linq;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class FriendshipTerminalSaveBoundaryBuilder
{
    private static void ApplyDeclaredCalendar(XElement root, XElement player, int? day)
    {
        if (!day.HasValue) return;
        XElement Required(XElement parent, string name) => parent.Elements(name).SingleOrDefault() ??
            throw new InvalidDataException("Friendship calendar fixture field missing: " + name);
        var values = Required(Required(player, "stats"), "Values");
        var entries = values.Elements("item").Where(row => row.Element("key")?.Element("string")?.Value == "daysPlayed").ToArray();
        var daysPlayed = entries.Length == 1 ? entries[0].Element("value")?.Element("unsignedInt") : null;
        Require(daysPlayed is not null && uint.TryParse(daysPlayed.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _),
            "Friendship calendar fixture needs one valid native daysPlayed statistic.");
        // Source/basis validation still uses the original date; only the fresh clone is seeded.
        Required(root, "year").Value = "2";
        Required(root, "currentSeason").Value = "winter";
        Required(root, "dayOfMonth").Value = day.Value.ToString(CultureInfo.InvariantCulture);
        daysPlayed!.Value = (112 + 3 * 28 + day.Value).ToString(CultureInfo.InvariantCulture);
    }
}
