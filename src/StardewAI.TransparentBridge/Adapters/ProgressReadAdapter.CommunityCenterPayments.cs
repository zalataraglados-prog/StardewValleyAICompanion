using Microsoft.Xna.Framework;
using StardewAI.Contracts.State;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Menus;
using StardewValley.Network;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class WorldProgressReadAdapter
{
    private static CommunityCenterBundleMoneyPaymentRef ReadCommunityCenterMoneyPayment(
        NetWorldState world,
        CommunityCenter communityCenter,
        string dataKey,
        int areaId,
        int bundleId,
        string[] ingredientParts,
        IReadOnlyList<BundleIngredientDescription> ingredients,
        int requiredSlots,
        int completedCount,
        bool noteAppears,
        Point? interactionTile,
        string routeState,
        bool menuClear,
        bool areaMutexLocked)
    {
        if (areaId != 4)
        {
            return new CommunityCenterBundleMoneyPaymentRef
            {
                ProjectionStatus = "not_applicable",
                ActionStatus = "community_center_bundle_not_native_money_payment"
            };
        }
        if (ingredientParts.Length != 3 || ingredients.Count != 1 ||
            ingredientParts[0] != "-1" || requiredSlots != 1 ||
            ingredients[0].stack < 1 || completedCount is < 0 or > 1)
        {
            return new CommunityCenterBundleMoneyPaymentRef
            {
                ProjectionStatus = "unavailable",
                ProjectionFailure = "vault_money_ingredient_shape_invalid",
                ActionStatus = "community_center_vault_payment_projection_unavailable"
            };
        }

        var amount = ingredients[0].stack;
        var projection = ProjectCommunityCenterDonation(
            world,
            communityCenter,
            areaId,
            bundleId,
            0,
            ingredients.Count,
            requiredSlots,
            completedCount);
        var moneyBefore = Game1.player.Money;
        var affordable = moneyBefore >= amount;
        var actionStatus = routeState == "conflicting_irreversible_flags"
            ? "community_center_route_state_conflict"
            : routeState == "joja_locked"
                ? "community_center_route_locked_out_by_joja"
                : completedCount >= requiredSlots
                    ? "community_center_vault_bundle_already_complete"
                    : !ReferenceEquals(Game1.currentLocation, communityCenter)
                        ? "community_center_not_current_location"
                        : !Game1.player.hasOrWillReceiveMail("canReadJunimoText")
                            ? "community_center_junimo_text_not_readable"
                            : !menuClear
                                ? "community_center_menu_or_dialogue_not_clear"
                                : !noteAppears || interactionTile is null
                                    ? "community_center_area_note_unavailable"
                                    : areaMutexLocked
                                        ? "community_center_area_mutex_locked"
                                        : !affordable
                                            ? "community_center_vault_payment_unaffordable"
                                            : "ready";

        return new CommunityCenterBundleMoneyPaymentRef
        {
            ProjectionStatus = "exact",
            ProjectionFailure = string.Empty,
            IngredientIndex = 0,
            RequiredMoney = amount,
            MoneyBefore = moneyBefore,
            MoneyAfter = moneyBefore - amount,
            Affordable = affordable,
            CompletedIngredientCountBefore = completedCount,
            CompletedIngredientCountAfter = projection.CompletedIngredientCountAfter,
            CompletesBundle = projection.CompletesBundle,
            ExpectedBundleRewardAvailableAfter = projection.ExpectedBundleRewardAvailableAfter,
            ExpectedCompleteBundleCountAfter = projection.ExpectedCompleteBundleCountAfter,
            CompletesArea = projection.CompletesArea,
            ExpectedAreaCompleteAfter = projection.ExpectedAreaCompleteAfter,
            ExpectedAreaCompletionMailPendingAfter = projection.ExpectedAreaCompletionMailPendingAfter,
            ExpectedBulletinThankYouPendingAfter = projection.ExpectedBulletinThankYouPendingAfter,
            ExpectedAllAreasCompleteAfter = projection.ExpectedAllAreasCompleteAfter,
            NewlyAppearingNoteAreaIds = projection.NewlyAppearingNoteAreaIds,
            ActionStatus = actionStatus,
            AuthoritativeRouteSources = new[]
            {
                new CommunityCenterAuthoritativeRouteSourceRef
                {
                    RouteKind = "native_money_payment",
                    SourceId = "money",
                    QualifiedItemId = string.Empty,
                    SourceAsset = "Data/Bundles",
                    SourcePath = "payload." + dataKey + "[ingredients:0]",
                    NativeConsumer = "JunimoNoteMenu.receiveLeftClick/purchaseButton"
                }
            }
        };
    }
}
