using StardewValley;
using System.Globalization;
using StardewValley.Constants;
using StardewValley.Extensions;
using StardewValley.Locations;
using StardewValley.Monsters;

namespace StardewAI.TransparentBridge.Adapters;

internal static partial class MiningMonsterDropResolver
{
    private static string? PreviewSpecialItem(MineShaft mine, int x, int y)
    {
        var level = mine.mineLevel;
        var random = Utility.CreateRandom(level, Game1.stats.DaysPlayed, x, (double)y * 9999.0);
        if (Game1.mine is null)
        {
            return "(O)388";
        }
        if (Game1.mine.GetAdditionalDifficulty() > 0)
        {
            if (random.NextDouble() < 0.02)
            {
                return "(BC)272";
            }
            return random.Next(7) switch
            {
                0 => "(W)61",
                1 => "(O)910",
                2 => "(O)913",
                3 => "(O)915",
                4 => "(O)527",
                5 => "(O)858",
                _ => null
            };
        }
        if (level < 20)
        {
            return new[] { "(W)16", "(W)24", "(B)504", "(B)505", "(O)516", "(O)518" }[random.Next(6)];
        }
        if (level < 40)
        {
            return new[] { "(W)22", "(W)24", "(B)504", "(B)505", "(O)516", "(O)518", "(W)15" }[random.Next(7)];
        }
        if (level < 60)
        {
            return new[] { "(W)6", "(W)26", "(W)15", "(B)510", "(O)517", "(O)519", "(W)27" }[random.Next(7)];
        }
        if (level < 80)
        {
            return new[] { "(W)26", "(W)27", "(B)508", "(B)510", "(O)517", "(O)519", "(W)19" }[random.Next(7)];
        }
        if (level < 100)
        {
            return new[] { "(W)48", "(W)48", "(B)511", "(B)513", "(W)18", "(W)28", "(W)52", "(W)3" }[random.Next(8)];
        }
        if (level < 120)
        {
            return new[] { "(W)19", "(W)50", "(B)511", "(B)513", "(W)18", "(W)46", "(O)887", "(W)3" }[random.Next(8)];
        }
        return new[] { "(W)45", "(W)50", "(B)511", "(B)513", "(W)18", "(W)28", "(W)52", "(O)787", "(B)878", "(O)856", "(O)859", "(O)887" }[random.Next(12)];
    }

    private static string[] PossibleSpecialItems(MineShaft mine)
    {
        if (Game1.mine is null)
        {
            return new[] { "(O)388" };
        }
        if (Game1.mine.GetAdditionalDifficulty() > 0)
        {
            return new[] { "(BC)272", "(W)61", "(O)910", "(O)913", "(O)915", "(O)527", "(O)858" };
        }
        var level = mine.mineLevel;
        if (level < 20)
        {
            return new[] { "(W)16", "(W)24", "(B)504", "(B)505", "(O)516", "(O)518" };
        }
        if (level < 40)
        {
            return new[] { "(W)22", "(W)24", "(B)504", "(B)505", "(O)516", "(O)518", "(W)15" };
        }
        if (level < 60)
        {
            return new[] { "(W)6", "(W)26", "(W)15", "(B)510", "(O)517", "(O)519", "(W)27" };
        }
        if (level < 80)
        {
            return new[] { "(W)26", "(W)27", "(B)508", "(B)510", "(O)517", "(O)519", "(W)19" };
        }
        if (level < 100)
        {
            return new[] { "(W)48", "(B)511", "(B)513", "(W)18", "(W)28", "(W)52", "(W)3" };
        }
        if (level < 120)
        {
            return new[] { "(W)19", "(W)50", "(B)511", "(B)513", "(W)18", "(W)46", "(O)887", "(W)3" };
        }
        return new[] { "(W)45", "(W)50", "(B)511", "(B)513", "(W)18", "(W)28", "(W)52", "(O)787", "(B)878", "(O)856", "(O)859", "(O)887" };
    }

    private static bool HasUnseenSecretNote(Farmer player)
    {
        if (!player.hasMagnifyingGlass)
        {
            return false;
        }
        var unseen = Utility.GetUnseenSecretNotes(player, journal: false, out _);
        return unseen.Length - player.Items.CountId("(O)79") > 0;
    }

    private static string QualifyDropId(string itemId)
    {
        if (itemId.StartsWith("-", StringComparison.Ordinal) && int.TryParse(itemId, out var resourceType))
        {
            return Math.Abs(resourceType) switch
            {
                0 => "(O)378",
                2 => "(O)380",
                4 => "(O)382",
                6 => "(O)384",
                10 => "(O)386",
                12 => "(O)388",
                14 => "(O)390",
                var id => "(O)" + id
            };
        }
        return ItemRegistry.QualifyItemId(itemId) ?? (itemId.StartsWith("(", StringComparison.Ordinal) ? itemId : "(O)" + itemId);
    }

    private static string[] Ordered(IEnumerable<string> ids)
    {
        return ids.Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal).ToArray();
    }}
