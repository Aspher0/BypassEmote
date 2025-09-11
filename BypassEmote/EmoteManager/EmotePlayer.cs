using BypassEmote.Helpers;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Excel.Sheets;
using System.Collections.Generic;

namespace BypassEmote;

internal static unsafe class EmotePlayer
{
    private static bool _updateHooked;

    // List of characters using a looped emote
    public static List<TrackedCharacter> TrackedCharacters = new List<TrackedCharacter>();

    public static void PlayEmote(IPlayerCharacter? chara, Emote emote)
    {
        if (chara == null)
            return;

        var emoteCategory = CommonHelper.TryGetEmoteCategory(emote);

        if (emoteCategory == null)
        {
            var categoryString = emote.EmoteCategory.Value.Name.ToString();

            if (categoryString == "Special")
                emoteCategory = CommonHelper.EmoteCategory.Looped;
            else
                emoteCategory = CommonHelper.EmoteCategory.OneShot;
        }

        StopLoop(chara, false);

        if (Service.ClientState.LocalPlayer != null && chara.Address == Service.ClientState.LocalPlayer.Address)
            FaceTarget();

        switch (emoteCategory)
        {
            case CommonHelper.EmoteCategory.Looped:
                {
                    var loopTimeline = TimelineResolver.ResolveLoopingTimeline(emote);
                    var introTimeline = TimelineResolver.ResolveIntroTimeline(emote);
                    StartLoopTimeline(chara, loopTimeline, introTimeline);
                    break;
                }
            case CommonHelper.EmoteCategory.OneShot:
            default:
                {
                    ushort timelineId = TimelineResolver.ResolveNonLoopingTimeline(emote);
                    if (timelineId == 0)
                        return;

                    PlayTimeline(chara, timelineId);
                    break;
                }
        }

        // if local player do stuff
        var local = Service.ClientState.LocalPlayer;
        if (local != null && local.Address == (nint)CommonHelper.GetCharacter(chara))
        {
            // Fire IPC event for Mare Syncing
            var provider = IpcProvider.Instance;
            provider?.LocalEmotePlayed?.Invoke(local, emote.RowId);
        }
    }

    public static void PlayEmoteById(IPlayerCharacter? chara, uint emoteId)
    {
        if (chara == null)
            return;

        var sheet = Service.DataManager.GetExcelSheet<Emote>();
        var emoteRow = sheet?.GetRow(emoteId);

        if (!emoteRow.HasValue)
            return;

        PlayEmote(chara, emoteRow.Value);
    }

    public static void PlayTimeline(IPlayerCharacter? chara, ushort timelineId)
    {
        if (chara == null)
            return;

        CommonHelper.GetCharacter(chara)->Timeline.TimelineSequencer.PlayTimeline(timelineId);
    }

    private static void StartLoopTimeline(IPlayerCharacter? chara, ushort loopTimelineId, ushort introTimelineId = 0)
    {
        if (chara == null)
            return;

        if (loopTimelineId == 0)
            return;

        var trackedCharacter = CommonHelper.AddOrUpdateCharacterInTrackedList(chara, loopTimelineId);

        if (trackedCharacter == null)
            return;

        StopLoop(chara, false);

        var character = CommonHelper.GetCharacter(chara);

        character->Timeline.BaseOverride = loopTimelineId;

        if (introTimelineId != 0)
        {
            character->Timeline.TimelineSequencer.PlayTimeline(introTimelineId);
        }
        else
        {
            character->Timeline.TimelineSequencer.PlayTimeline(loopTimelineId);
        }

        trackedCharacter.ActiveLoopTimelineId = loopTimelineId;
        trackedCharacter.UpdateLastPosition();

        if (!_updateHooked && TrackedCharacters.Count > 0)
        {
            Service.Framework.Update += OnFrameworkUpdate;
            _updateHooked = true;
        }
    }

    public static void StopLoop(IPlayerCharacter? chara, bool shouldRemoveFromList)
    {
        if (chara != null)
        {
            var character = CommonHelper.GetCharacter(chara);
            character->Timeline.BaseOverride = 0;
            character->Timeline.TimelineSequencer.PlayTimeline(3);
            CleanupLoopState(chara, shouldRemoveFromList);
        }
    }

    private static void CleanupLoopState(IPlayerCharacter? chara, bool shouldRemoveFromList)
    {
        var trackedCharacter = CommonHelper.TryGetCharacterFromTrackedList(chara);

        if (trackedCharacter != null)
        {
            trackedCharacter.ActiveLoopTimelineId = 0;

            if (shouldRemoveFromList)
                TrackedCharacters.Remove(trackedCharacter);
        }

        if (TrackedCharacters.Count == 0 && _updateHooked)
        {
            Service.Framework.Update -= OnFrameworkUpdate;
            _updateHooked = false;
        }
    }

    private static void OnFrameworkUpdate(IFramework framework)
    {
        foreach (var trackedCharacter in TrackedCharacters)
        {
            if (trackedCharacter.Character == null)
            {
                CommonHelper.RemoveChracterFromTrackedListByID(trackedCharacter.UniqueId);
                return;
            }

            if (CommonHelper.IsCharacterInObjectTable(trackedCharacter) == false)
            {
                StopLoop(trackedCharacter.Character, true);
                CommonHelper.RemoveChracterFromTrackedListByID(trackedCharacter.UniqueId);
                return;
            }

            var chara = CommonHelper.GetCharacter(trackedCharacter.Character);

            if (chara->Timeline.BaseOverride != trackedCharacter.ActiveLoopTimelineId)
            {
                StopLoop(trackedCharacter.Character, true);
                CommonHelper.RemoveChracterFromTrackedListByID(trackedCharacter.UniqueId);
                return;
            }

            var pos = trackedCharacter.Character.Position;
            var delta = pos - trackedCharacter.LastPlayerPosition;
            if (delta.LengthSquared() > 1e-12f)
            {
                StopLoop(trackedCharacter.Character, true);
                CommonHelper.RemoveChracterFromTrackedListByID(trackedCharacter.UniqueId);
                return;
            }

            //Uncomment to also stop on rotation change
            //var rot = trackedCharacter.Character.Rotation;
            //if (System.Math.Abs(rot - trackedCharacter.LastPlayerRotation) > 1e-7f)
            //{
            //    StopLoop(trackedCharacter.Character, true);
            //    CommonHelper.RemoveChracterFromTrackedListByID(trackedCharacter.UniqueId);
            //    return;
            //}

            trackedCharacter.UpdateLastPosition();
        }
    }

    public static void FaceTarget()
    {
        ChatHelper.SendMessage("/facetarget");
    }

    public static void Dispose()
    {
        Service.Framework.Update -= OnFrameworkUpdate;
        _updateHooked = false;

        foreach (var trackedCharacter in TrackedCharacters)
        {
            if (trackedCharacter.Character != null)
            {
                StopLoop(trackedCharacter.Character, true);
            }
        }
    }
}
