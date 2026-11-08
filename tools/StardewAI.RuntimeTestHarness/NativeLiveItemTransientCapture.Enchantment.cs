using System.Reflection;
using Netcode;
using StardewAI.Contracts.Training;
using StardewValley.Enchantments;

namespace StardewAI.RuntimeTestHarness;

internal sealed record NativeLiveEnchantmentTransientState(bool Applied, string? DisplayName, int Level, int Characters);

internal static partial class NativeLiveItemTransientCapture
{
    private static readonly FieldInfo EnchantmentAppliedField = RequireField(typeof(BaseEnchantment), "_applied", typeof(bool));
    private static readonly FieldInfo EnchantmentDisplayNameField = RequireField(typeof(BaseEnchantment), "_displayName", typeof(string));
    private static readonly FieldInfo EnchantmentLevelField = RequireField(typeof(BaseEnchantment), "level", typeof(NetInt));

    internal static NativeLiveEnchantmentTransientState CaptureEnchantment(BaseEnchantment enchantment, int maximumCharacters)
    {
        if (maximumCharacters is < 0 or > 65536)
            throw new ArgumentOutOfRangeException(nameof(maximumCharacters));
        ArgumentNullException.ThrowIfNull(enchantment);
        var type = enchantment.GetType();
        if (type.Assembly != typeof(BaseEnchantment).Assembly || !NativeInventoryEnchantmentTypes.IsSupported(type.FullName))
            throw new InvalidDataException("Native enchantment transient runtime type is unsupported: " + type.FullName);
        var budget = new CharacterBudget(maximumCharacters);
        var applied = ReadValue<bool>(EnchantmentAppliedField, enchantment);
        var display = budget.String(ReadNullable<string>(EnchantmentDisplayNameField, enchantment));
        var level = ReadNullable<NetInt>(EnchantmentLevelField, enchantment)
            ?? throw new InvalidDataException("Native enchantment level wrapper is unavailable.");
        if (level.GetType() != typeof(NetInt))
            throw new InvalidDataException("Native enchantment level wrapper runtime type is unsupported.");
        return new(applied, display, level.Value, budget.Characters);
    }
}
