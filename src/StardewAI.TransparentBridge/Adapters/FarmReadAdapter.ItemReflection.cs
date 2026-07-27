using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.GameData.Machines;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;
using StardewAI.TransparentBridge.State;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class FarmReadAdapter : ReadAdapterBase
{
    private static object? SummarizeItem(Item? item)
    {
        return item is null
            ? null
            : new
            {
                item_id = item.ItemId,
                qualified_item_id = item.QualifiedItemId,
                display_name = item.DisplayName,
                stack = item.Stack,
                quality = item.Quality,
                sale_price = item.salePrice(),
                special_state =
                    ReadItemSpecialState(item)
            };
    }

    private static string NormalizeObjectQualifiedId(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return string.Empty;
        }

        return itemId.StartsWith("(", StringComparison.Ordinal) ? itemId : "(O)" + itemId;
    }

    private static int? ReadItemSalePrice(string itemId)
    {
        var qualifiedId = NormalizeObjectQualifiedId(itemId);
        if (string.IsNullOrWhiteSpace(qualifiedId))
        {
            return null;
        }

        try
        {
            return ItemRegistry.Create(qualifiedId).salePrice();
        }
        catch
        {
            return null;
        }
    }

    private static string[] ReadStringList(object value, string property)
    {
        var propertyValue = ReadMemberValue(value, property);
        if (propertyValue is null)
        {
            return Array.Empty<string>();
        }

        return ((System.Collections.IEnumerable)propertyValue)
            .Cast<object?>()
            .Where(item => item is not null)
            .Select(item => item!.ToString() ?? string.Empty)
            .Where(item => item.Length > 0)
            .ToArray();
    }

    private static int[] ReadIntList(object value, string property)
    {
        var propertyValue = ReadMemberValue(value, property);
        if (propertyValue is null)
        {
            return Array.Empty<int>();
        }

        return ((System.Collections.IEnumerable)propertyValue)
            .Cast<object?>()
            .Select(item => item is null ? (int?)null : Convert.ToInt32(item))
            .Where(item => item.HasValue)
            .Select(item => item!.Value)
            .ToArray();
    }

    private static string? ReadString(object value, string property)
    {
        return ReadMemberValue(value, property)?.ToString();
    }

    private static int? ReadIntNullable(object value, string property)
    {
        var propertyValue = ReadMemberValue(value, property);
        return propertyValue is null ? null : Convert.ToInt32(propertyValue);
    }

    private static bool? ReadBoolNullable(object value, string property)
    {
        var propertyValue = ReadMemberValue(value, property);
        return propertyValue is null ? null : Convert.ToBoolean(propertyValue);
    }

    private static float? ReadFloatNullable(object value, string property)
    {
        var propertyValue = ReadMemberValue(value, property);
        return propertyValue is null ? null : Convert.ToSingle(propertyValue);
    }

    private static int ReadCount(object value, string property)
    {
        var propertyValue = ReadMemberValue(value, property);
        return propertyValue is System.Collections.ICollection collection ? collection.Count : 0;
    }

    private static object? ReadMemberValue(object value, string memberName)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public;
        var type = value.GetType();
        var property = type.GetProperty(memberName, flags);
        if (property is not null)
        {
            return property.GetValue(value);
        }

        var field = type.GetField(memberName, flags);
        return field?.GetValue(value);
    }}
