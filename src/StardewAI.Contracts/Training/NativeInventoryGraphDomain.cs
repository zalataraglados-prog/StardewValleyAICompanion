namespace StardewAI.Contracts.Training;

public static class NativeInventoryGraphLimits
{
    public const int MaximumRoots = 512;
    public const int MaximumDepth = 16;
    public const int MaximumRootOccurrences = 256;
    public const int MaximumNodeOccurrences = 4096;
    public const int MaximumEdgeOccurrences = 8192;
    public const int MaximumCollection = 256;
    public const int MaximumCharacters = 1024 * 1024;
}

public static class NativeInventoryGraphRoles
{
    public const string HeldObject = "held_object";
    public const string LastInputItem = "last_input_item";
    public const string Attachments = "attachments";
    public const string Enchantments = "enchantments";
    public const string CombinedRings = "combined_rings";
}

// Exact concrete types in the pinned 1.6.15 native assembly, not a namespace wildcard.
public static class NativeInventoryEnchantmentTypes
{
    public static bool IsSupported(string? runtimeType) => runtimeType is
        "StardewValley.Enchantments.AmethystEnchantment" or
        "StardewValley.Enchantments.AquamarineEnchantment" or
        "StardewValley.Enchantments.ArchaeologistEnchantment" or
        "StardewValley.Enchantments.ArtfulEnchantment" or
        "StardewValley.Enchantments.AttackEnchantment" or
        "StardewValley.Enchantments.AutoHookEnchantment" or
        "StardewValley.Enchantments.AxeEnchantment" or
        "StardewValley.Enchantments.BaseEnchantment" or
        "StardewValley.Enchantments.BaseWeaponEnchantment" or
        "StardewValley.Enchantments.BottomlessEnchantment" or
        "StardewValley.Enchantments.BugKillerEnchantment" or
        "StardewValley.Enchantments.CritEnchantment" or
        "StardewValley.Enchantments.CritPowerEnchantment" or
        "StardewValley.Enchantments.CrusaderEnchantment" or
        "StardewValley.Enchantments.DefenseEnchantment" or
        "StardewValley.Enchantments.DiamondEnchantment" or
        "StardewValley.Enchantments.EfficientToolEnchantment" or
        "StardewValley.Enchantments.EmeraldEnchantment" or
        "StardewValley.Enchantments.FisherEnchantment" or
        "StardewValley.Enchantments.FishingRodEnchantment" or
        "StardewValley.Enchantments.GalaxySoulEnchantment" or
        "StardewValley.Enchantments.GenerousEnchantment" or
        "StardewValley.Enchantments.HaymakerEnchantment" or
        "StardewValley.Enchantments.HoeEnchantment" or
        "StardewValley.Enchantments.JadeEnchantment" or
        "StardewValley.Enchantments.LightweightEnchantment" or
        "StardewValley.Enchantments.MagicEnchantment" or
        "StardewValley.Enchantments.MasterEnchantment" or
        "StardewValley.Enchantments.MilkPailEnchantment" or
        "StardewValley.Enchantments.PanEnchantment" or
        "StardewValley.Enchantments.PickaxeEnchantment" or
        "StardewValley.Enchantments.PowerfulEnchantment" or
        "StardewValley.Enchantments.PreservingEnchantment" or
        "StardewValley.Enchantments.ReachingToolEnchantment" or
        "StardewValley.Enchantments.RubyEnchantment" or
        "StardewValley.Enchantments.ShavingEnchantment" or
        "StardewValley.Enchantments.ShearsEnchantment" or
        "StardewValley.Enchantments.SlimeGathererEnchantment" or
        "StardewValley.Enchantments.SlimeSlayerEnchantment" or
        "StardewValley.Enchantments.SwiftToolEnchantment" or
        "StardewValley.Enchantments.TopazEnchantment" or
        "StardewValley.Enchantments.VampiricEnchantment" or
        "StardewValley.Enchantments.WateringCanEnchantment" or
        "StardewValley.Enchantments.WeaponSpeedEnchantment";
}
