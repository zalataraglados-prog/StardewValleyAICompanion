using System;
using System.Collections.Generic;

namespace StardewAI.Core.Training
{
    public sealed partial class FriendshipDayTransitionSimulator
    {
        public FriendshipDayTransitionResult Simulate(FriendshipDayTransitionInput input)
        {
            if (!TryValidate(input, out var issue))
                return Blocked(input?.NpcName ?? string.Empty, issue);
            if (input.FriendshipRowExists == false)
            {
                return new FriendshipDayTransitionResult
                {
                    Status = "exact_no_friendship_row",
                    NpcName = input.NpcName,
                    FriendshipRowExistsAfter = false
                };
            }

            var pointsBefore = input.Points!.Value;
            var points = pointsBefore;
            var talked = input.TalkedToToday!.Value;
            var giftsToday = input.GiftsToday!.Value;
            var giftsThisWeek = input.GiftsThisWeek!.Value;
            var transitions = new List<FriendshipPointTransition>();

            if (input.CharacterExists == true)
            {
                var usesLowerDecayCap = input.IsDatable == true &&
                    input.IsDating == false &&
                    input.IsNpcMarried == false;
                if (input.IsPlayerSpouse == true && !talked)
                    ApplyChange(-20, "spouse_not_talked");
                else if (input.IsDating == true && !talked && points < 2500)
                    ApplyChange(-8, "dating_not_talked_below_2500");

                if (talked)
                {
                    talked = false;
                }
                else if ((!usesLowerDecayCap && points < 2500) ||
                         (usesLowerDecayCap && points < 2000))
                {
                    ApplyChange(-2, usesLowerDecayCap
                        ? "ordinary_not_talked_below_2000"
                        : "ordinary_not_talked_below_2500");
                }
            }

            if (input.TransitionDateTotalDays != input.LastGiftDateTotalDays)
                giftsToday = 0;
            if (input.TransitionDateTotalSundayWeeks != input.LastGiftDateTotalSundayWeeks)
            {
                if (giftsThisWeek >= 2)
                    ApplyChange(10, "weekly_two_gift_bonus");
                giftsThisWeek = 0;
            }

            return new FriendshipDayTransitionResult
            {
                Status = "exact_native_rule_projection_runtime_pending",
                NpcName = input.NpcName,
                FriendshipRowExistsAfter = true,
                PointsBefore = pointsBefore,
                PointsAfter = points,
                TalkedToTodayAfter = talked,
                GiftsTodayAfter = giftsToday,
                GiftsThisWeekAfter = giftsThisWeek,
                PointTransitions = transitions.ToArray()
            };

            void ApplyChange(int requestedDelta, string reason)
            {
                var before = points;
                var effective = requestedDelta;
                var modifierStatus = "applied";
                if (input.CharacterExists != true ||
                    (input.IsChild != true && input.IsVillager != true))
                {
                    transitions.Add(new FriendshipPointTransition
                    {
                        Sequence = transitions.Count,
                        Reason = reason,
                        RequestedDelta = requestedDelta,
                        EffectiveDeltaBeforeClamp = 0,
                        PointsBefore = before,
                        PointsAfter = before,
                        AppliedDelta = 0,
                        ModifierStatus = "character_not_supported_by_change_friendship"
                    });
                    return;
                }
                if (effective > 0 && input.PlayerHasFriendshipBook == true)
                {
                    effective = (int)(effective * 1.1f);
                    modifierStatus = "friendship_book_applied";
                }
                if (effective > 0 && input.SpeaksDwarvish == true &&
                    input.PlayerCanUnderstandDwarves == false)
                {
                    effective = 0;
                    modifierStatus = "dwarvish_positive_change_rejected";
                }
                else if (effective > 0 && input.IsDivorced == true)
                {
                    effective = 0;
                    modifierStatus = "divorced_positive_change_rejected";
                }
                else if (input.IsPlayerSpouse == true)
                {
                    effective = (int)(effective * 0.66f);
                    modifierStatus = modifierStatus == "friendship_book_applied"
                        ? "friendship_book_then_spouse_multiplier"
                        : "spouse_multiplier_applied";
                }

                var maximumPoints = checked((input.MaximumHearts!.Value + 1) * 250 - 1);
                points = Math.Max(0, Math.Min(points + effective, maximumPoints));
                transitions.Add(new FriendshipPointTransition
                {
                    Sequence = transitions.Count,
                    Reason = reason,
                    RequestedDelta = requestedDelta,
                    EffectiveDeltaBeforeClamp = effective,
                    PointsBefore = before,
                    PointsAfter = points,
                    AppliedDelta = points - before,
                    ModifierStatus = modifierStatus
                });
            }
        }

        private static bool TryValidate(FriendshipDayTransitionInput? input, out string issue)
        {
            issue = string.Empty;
            if (input is null)
                issue = "friendship_day_transition_input_missing";
            else if (string.IsNullOrWhiteSpace(input.NpcName))
                issue = "friendship_day_transition_npc_name_missing";
            else if (!input.FriendshipRowExists.HasValue)
                issue = "friendship_day_transition_row_presence_missing";
            else if (input.FriendshipRowExists == false)
                return true;
            else if (!input.CharacterExists.HasValue ||
                     !input.TalkedToToday.HasValue ||
                     !input.Points.HasValue || input.Points.Value < 0 ||
                     !input.GiftsToday.HasValue || input.GiftsToday.Value < 0 ||
                     !input.GiftsThisWeek.HasValue || input.GiftsThisWeek.Value < 0 ||
                     !input.LastGiftDateStateComplete ||
                     !input.TransitionDateTotalDays.HasValue ||
                     !input.TransitionDateTotalSundayWeeks.HasValue)
            {
                issue = "friendship_day_transition_row_state_incomplete";
            }
            else if (input.CharacterExists == true &&
                     (!input.IsVillager.HasValue ||
                      !input.IsChild.HasValue ||
                      !input.IsDatable.HasValue ||
                      !input.IsNpcMarried.HasValue ||
                      !input.IsPlayerSpouse.HasValue ||
                      !input.IsDating.HasValue ||
                      !input.IsDivorced.HasValue ||
                      !input.SpeaksDwarvish.HasValue ||
                      !input.PlayerCanUnderstandDwarves.HasValue ||
                      !input.PlayerHasFriendshipBook.HasValue ||
                      !input.MaximumHearts.HasValue || input.MaximumHearts.Value < 0))
            {
                issue = "friendship_day_transition_character_state_incomplete";
            }

            return issue.Length == 0;
        }

        private static FriendshipDayTransitionResult Blocked(string npcName, string issue)
        {
            return new FriendshipDayTransitionResult
            {
                Status = "blocked",
                NpcName = npcName,
                Issues = new[] { issue }
            };
        }
    }
}
