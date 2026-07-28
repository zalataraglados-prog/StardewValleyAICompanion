using StardewValley;
using System.Globalization;
using StardewValley.Constants;
using StardewValley.Extensions;
using StardewValley.Locations;
using StardewValley.Monsters;

namespace StardewAI.TransparentBridge.Adapters;

internal sealed class MiningMonsterDropProjection
{
    public string[] SelectedBaseDropQualifiedItemIds { get; set; } = Array.Empty<string>();

    public string[] GuaranteedDropQualifiedItemIds { get; set; } = Array.Empty<string>();

    public string[] ConditionalDropQualifiedItemIds { get; set; } = Array.Empty<string>();

    public string[][] GuaranteedOneOfQualifiedItemIdGroups { get; set; } = Array.Empty<string[]>();

    public string[] ConditionalDropCatalogKeys { get; set; } = Array.Empty<string>();

    public string[] PossibleDropQualifiedItemIds { get; set; } = Array.Empty<string>();

    public MiningDropItemProjection[] PossibleDropItems { get; set; } = Array.Empty<MiningDropItemProjection>();

    public string CurrentDeathTilePreviewQualifiedItemId { get; set; } = string.Empty;

    public string CurrentDeathTilePreviewStatus { get; set; } = "not_applicable";

    public object RuntimeExtraDropRuleInputs { get; set; } = new { };

    public string RuntimeExtraDropRuleCompleteness { get; set; } = string.Empty;

    public MiningMonsterDropProbabilityRule[] DropProbabilityRules { get; set; } = Array.Empty<MiningMonsterDropProbabilityRule>();

    public string DropProbabilityCompleteness { get; set; } = string.Empty;

    public string PrimaryDropStatus { get; set; } = string.Empty;

    public string ItemIdentityCompleteness { get; set; } = string.Empty;

    public string[] UnresolvedDynamicRules { get; set; } = Array.Empty<string>();

    public string Source { get; set; } = string.Empty;
}

internal sealed class MiningMonsterDropProbabilityRule
{
    public string Key { get; set; } = string.Empty;

    public string[] QualifiedItemIds { get; set; } = Array.Empty<string>();

    public string CatalogKey { get; set; } = string.Empty;

    public double EventChance { get; set; }

    public double EffectivePerKillChance { get; set; }

    public double? PerIdentityChance { get; set; }

    public int CallsPerBaseBranch { get; set; }

    public double ExpectedEventsPerKill { get; set; }

    public double? ExpectedQuantityPerKill { get; set; }

    public string QuantityStatus { get; set; } = string.Empty;

    public bool BookVoidDuplicationEligible { get; set; }

    public string ProbabilityStatus { get; set; } = string.Empty;

    public string ItemSelectionStatus { get; set; } = string.Empty;

    public string Source { get; set; } = string.Empty;
}

internal sealed class MiningDropCatalogProjection
{
    public string Key { get; set; } = string.Empty;

    public string[] PossibleQualifiedItemIds { get; set; } = Array.Empty<string>();

    public MiningDropCatalogEntryProjection[] SelectionProbabilityEntries { get; set; } = Array.Empty<MiningDropCatalogEntryProjection>();

    public bool Active { get; set; }

    public string ItemIdentityCompleteness { get; set; } = string.Empty;

    public string SelectionProbabilityCompleteness { get; set; } = string.Empty;

    public string Source { get; set; } = string.Empty;
}

internal sealed class MiningDropItemProjection
{
    public string QualifiedItemId { get; set; } = string.Empty;

    public string[] ContextTags { get; set; } = Array.Empty<string>();

    public string ContextTagStatus { get; set; } = string.Empty;
}

internal sealed class MiningDropCatalogEntryProjection
{
    public string QualifiedItemId { get; set; } = string.Empty;

    public double ConditionalSelectionChance { get; set; }

    public double ConditionalExpectedQuantity { get; set; } = 1d;

    public string ProbabilityStatus { get; set; } = string.Empty;
}
