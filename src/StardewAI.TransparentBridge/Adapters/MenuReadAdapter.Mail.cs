using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using StardewValley;
using StardewValley.Menus;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class MenuReadAdapter
{
    private static object ReadLetterViewerMenuState(LetterViewerMenu menu)
    {
        var attachments = menu.itemsToGrab
            .Select((component, index) => new
            {
                index,
                present = component.item is not null,
                visible = component.visible,
                qualified_item_id = component.item?.QualifiedItemId,
                item_id = component.item?.ItemId,
                display_name = component.item?.DisplayName,
                stack = component.item?.Stack,
                quality = component.item?.Quality,
                maximum_stack_size = component.item?.maximumStackSize(),
                runtime_type = component.item?.GetType().FullName,
                special_state = component.item is null ? null : FarmReadAdapter.ReadItemSpecialState(component.item)
            })
            .ToArray();
        var pages = menu.mailMessage.ToArray();
        var identitySource = string.Join("\n", new[]
        {
            menu.mailTitle ?? string.Empty,
            menu.isMail.ToString(),
            menu.isFromCollection.ToString(),
            menu.page.ToString(),
            pages.Length.ToString(),
            menu.questID ?? string.Empty,
            menu.specialOrderId ?? string.Empty,
            string.Join(";", attachments.Select(item => item.index + ":" + item.qualified_item_id + ":" + item.stack + ":" + item.quality))
        });
        return new
        {
            kind = "letter_viewer",
            mail_title = menu.mailTitle,
            is_mail = menu.isMail,
            is_from_collection = menu.isFromCollection,
            page = menu.page,
            page_count = pages.Length,
            message_pages = pages,
            scale = menu.scale,
            can_receive_input = menu.scale >= 1f,
            ready_to_close = menu.readyToClose(),
            has_interactable = menu.HasInteractable(),
            should_show_interactable = menu.ShouldShowInteractable(),
            items_left_to_grab = menu.itemsLeftToGrab(),
            attachment_count = attachments.Count(item => item.present),
            attachments,
            quest_id = menu.questID,
            special_order_id = menu.specialOrderId,
            has_quest_or_special_order = menu.HasQuestOrSpecialOrder,
            money_included = menu.moneyIncluded,
            learned_recipe = menu.learnedRecipe,
            cooking_or_crafting = menu.cookingOrCrafting,
            secret_note_image = menu.secretNoteImage,
            which_bg = menu.whichBG,
            destroy = menu.destroy,
            menu_identity_sha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identitySource))).ToLowerInvariant()
        };
    }
}
