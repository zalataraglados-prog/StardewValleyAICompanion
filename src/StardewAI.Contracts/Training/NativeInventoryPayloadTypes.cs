namespace StardewAI.Contracts.Training;

public enum NativeInventoryPayloadIdentityRule
{
    FixedQualifier,
    ObjectBigCraftable,
    ClothingType
}

public readonly struct NativeInventoryPayloadType
{
    internal NativeInventoryPayloadType(string runtimeType, bool isTool, string? qualifier,
        NativeInventoryPayloadIdentityRule identityRule = NativeInventoryPayloadIdentityRule.FixedQualifier)
    {
        XmlRoot = runtimeType.Substring(runtimeType.LastIndexOf('.') + 1);
        IsTool = isTool;
        FixedQualifier = qualifier;
        IdentityRule = identityRule;
    }

    public string XmlRoot { get; }
    public bool IsTool { get; }
    public string? FixedQualifier { get; }
    public NativeInventoryPayloadIdentityRule IdentityRule { get; }
}

// The bounded persisted-payload producer and consumer share this exact vanilla domain.
// This is not the full game item catalog or a claim of transient-state coverage.
public static class NativeInventoryPayloadTypes
{
    public static bool TryGet(string? runtimeType, out NativeInventoryPayloadType descriptor)
    {
        descriptor = runtimeType switch
        {
            "StardewValley.Object" or "StardewValley.Objects.ColoredObject" =>
                new(runtimeType, false, null, NativeInventoryPayloadIdentityRule.ObjectBigCraftable),
            "StardewValley.Objects.Clothing" =>
                new(runtimeType, false, null, NativeInventoryPayloadIdentityRule.ClothingType),
            "StardewValley.Objects.Boots" => new(runtimeType, false, "(B)"),
            "StardewValley.Objects.Hat" => new(runtimeType, false, "(H)"),
            "StardewValley.Objects.Ring" or "StardewValley.Objects.CombinedRing" or
                "StardewValley.Objects.SpecialItem" => new(runtimeType, false, "(O)"),
            "StardewValley.Tools.MeleeWeapon" or "StardewValley.Tools.Slingshot" => new(runtimeType, true, "(W)"),
            "StardewValley.Tools.Axe" or "StardewValley.Tools.ErrorTool" or "StardewValley.Tools.FishingRod" or
                "StardewValley.Tools.GenericTool" or "StardewValley.Tools.Hoe" or "StardewValley.Tools.MilkPail" or
                "StardewValley.Tools.Pan" or "StardewValley.Tools.Pickaxe" or "StardewValley.Tools.Shears" or
                "StardewValley.Tools.Wand" or "StardewValley.Tools.WateringCan" => new(runtimeType, true, "(T)"),
            _ => default
        };
        return descriptor.XmlRoot is not null;
    }
}
