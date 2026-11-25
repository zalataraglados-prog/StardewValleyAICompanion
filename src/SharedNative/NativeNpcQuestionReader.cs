using System;
using System.Linq;
using StardewAI.Contracts.State;
using StardewValley;
using StardewValley.Menus;

namespace StardewAI.SharedNative;

internal static class NativeNpcQuestionReader
{
    public static NativeNpcQuestionEvidence Read(DialogueBox menu)
    {
        var dialogue = menu.characterDialogue;
        var speaker = dialogue?.speaker;
        var options = dialogue?.getNPCResponseOptions();
        var evidence = new NativeNpcQuestionEvidence { Speaker = speaker?.Name ?? string.Empty };
        if (Game1.eventUp || Game1.isFestival() || !menu.isQuestion ||
            dialogue is null || speaker is null || dialogue.answerQuestionBehavior is not null ||
            options is null || menu.responses is null || options.Count != menu.responses.Length ||
            options.Any(row => row.extraArgument is not null || row.id is null ||
                !speaker.Dialogue.ContainsKey(row.responseKey)) ||
            !options.Select(row => row.responseKey).SequenceEqual(menu.responses.Select(row => row.responseKey)))
            return evidence;
        // DialogueBox dispatches a character-owned question to chooseResponse;
        // the location's lastQuestionKey may still describe an earlier menu.
        evidence.OrdinaryQuestion = true;
        evidence.Responses = options.Select(row => new NativeNpcQuestionAnswer
        {
            Key = row.responseKey, SeenId = row.id, FriendshipChange = row.friendshipChange,
            Reply = speaker.Dialogue[row.responseKey]
        }).ToArray();
        return evidence;
    }
}
