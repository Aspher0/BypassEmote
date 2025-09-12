using BypassEmote.Helpers;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;
using System.Collections.Generic;

namespace BypassEmote;

internal static unsafe class EmotePlayer
{
    private static bool _updateHooked;

    // List of characters using a looped emote
    public static List<TrackedCharacter> TrackedCharacters = new List<TrackedCharacter>();

    public static void PlayEmote(ICharacter? chara, Emote emote)
    {
        if (chara == null)
            return;

        var emoteCategory = CommonHelper.GetEmoteCategory(emote);

        StopLoop(chara, false);

        if (Service.ClientState.LocalPlayer != null && chara.Address == Service.ClientState.LocalPlayer.Address)
            FaceTarget();

        switch (emoteCategory)
        {
            case CommonHelper.EmoteCategory.Looped:
                {
                    PlayEmote(Service.Player, chara, emote);
                    var trackedCharacter = CommonHelper.AddOrUpdateCharacterInTrackedList(chara.Address);

                    if (trackedCharacter == null) break;

                    trackedCharacter.UpdateLastPosition();

                    if (!_updateHooked && TrackedCharacters.Count > 0)
                    {
                        Service.Framework.Update += OnFrameworkUpdate;
                        _updateHooked = true;
                    }

                    break;
                }
            case CommonHelper.EmoteCategory.OneShot:
            default:
                {
                    ushort timelineId = (ushort)emote.ActionTimeline[0].RowId;
                    if (timelineId == 0)
                        return;
                    CommonHelper.RemoveCharacterFromTrackedListByCharacterAddress(chara.Address);
                    PlayOneShotEmote(chara, timelineId);
                    break;
                }
        }

        var local = Service.ClientState.LocalPlayer;
        if (local != null && local.Address == (nint)CommonHelper.GetCharacter(chara))
        {
            // Fire IPC event only if local player is the one playing the emote
            var provider = IpcProvider.Instance;
            provider?.LocalEmotePlayed?.Invoke(local, emote.RowId);
        }
    }

    public static void PlayEmoteById(ICharacter? chara, uint emoteId)
    {
        if (chara == null)
            return;

        var sheet = Service.DataManager.GetExcelSheet<Emote>();
        var emoteRow = sheet?.GetRow(emoteId);

        if (!emoteRow.HasValue)
            return;

        PlayEmote(chara, emoteRow.Value);
    }

    public static void PlayEmote(ActionTimelinePlayer player, ICharacter actor, Emote emote, bool blendIntro = true)
    {
        if (actor == null || emote.RowId == 0) return;

        ushort loop = (ushort)emote.ActionTimeline[0].RowId; // Standard/Loop
        ushort intro = (ushort)emote.ActionTimeline[1].RowId; // Intro
        ushort upper = (ushort)emote.ActionTimeline[4].RowId; // Upper-body (Blend)

        if (blendIntro && intro != 0)
            player.Blend(actor, intro);

        if (loop != 0)
        {
            player.Play(actor, loop, interrupt: true);
            return;
        }

        if (upper != 0)
        {
            player.Blend(actor, upper);
            return;
        }

        for (int i = 0; i < emote.ActionTimeline.Count; i++)
        {
            var id = (ushort)emote.ActionTimeline[i].RowId;
            if (id == 0) continue;

            if (i == 4)
                player.Blend(actor, id);
            else
                player.Play(actor, id, interrupt: true);
            return;
        }
    }

    public static void Stop(ActionTimelinePlayer player, ICharacter actor) => player.Stop(actor);

    public static void PlayOneShotEmote(ICharacter? chara, ushort timelineId)
    {
        if (chara == null)
            return;

        CommonHelper.GetCharacter(chara)->Timeline.TimelineSequencer.PlayTimeline(timelineId);
    }

    public static void StopLoop(ICharacter? chara, bool shouldRemoveFromList)
    {
        if (chara != null)
        {
            var character = CommonHelper.GetCharacter(chara);

            Stop(Service.Player, chara);

            var trackedCharacter = CommonHelper.TryGetCharacterFromTrackedList(chara.Address);

            if (trackedCharacter != null && shouldRemoveFromList)
            {
                CommonHelper.RemoveChracterFromTrackedListByID(trackedCharacter.UniqueId);

                if (TrackedCharacters.Count == 0 && _updateHooked)
                {
                    Service.Framework.Update -= OnFrameworkUpdate;
                    _updateHooked = false;
                }
            }
        }
    }

    private static void OnFrameworkUpdate(IFramework framework)
    {
        // Logging errors to debug issues
        foreach (var trackedCharacter in TrackedCharacters)
        {
            var character = CommonHelper.TryGetPlayerCharacterFromCID(trackedCharacter.CID);

            if (character == null)
            {
                Service.Log.Error("[BYPASSEMOTE] Character became null, removing it from list.");
                CommonHelper.RemoveChracterFromTrackedListByID(trackedCharacter.UniqueId);
                return;
            }

            if (CommonHelper.IsCharacterInObjectTable(trackedCharacter) == false)
            {
                Service.Log.Error("[BYPASSEMOTE] Character not in object table, removing it from list.");
                StopLoop(character, true);
                CommonHelper.RemoveChracterFromTrackedListByID(trackedCharacter.UniqueId);
                return;
            }

            // Check if position has changed
            var pos = character.Position;
            var delta = pos - trackedCharacter.LastPlayerPosition;
            if (delta.LengthSquared() > 1e-12f)
            {
                Service.Log.Error("[BYPASSEMOTE] Character moved, removing it from list.");
                StopLoop(character, true);
                CommonHelper.RemoveChracterFromTrackedListByID(trackedCharacter.UniqueId);
                return;
            }

            // Check if rotation has changed
            var rot = character.Rotation;
            if (Service.InterruptEmoteOnRotate && System.Math.Abs(rot - trackedCharacter.LastPlayerRotation) > 1e-7f)
            {
                Service.Log.Error("[BYPASSEMOTE] Character rotation changed, removing it from list.");
                StopLoop(character, true);
                CommonHelper.RemoveChracterFromTrackedListByID(trackedCharacter.UniqueId);
                return;
            }

            // Check if weapon drawn state has changed
            var isWeaponDrawn = CommonHelper.IsCharacterWeaponDrawn(character.Address);
            if (isWeaponDrawn != trackedCharacter.IsWeaponDrawn)
            {
                Service.Log.Error("[BYPASSEMOTE] Character weapon drawn flag changed, removing it from list.");
                StopLoop(character, true);
                CommonHelper.RemoveChracterFromTrackedListByID(trackedCharacter.UniqueId);
                return;
            }

            trackedCharacter.UpdateLastPosition();
        }
    }

    public unsafe static void FaceTarget()
    {
        if (Service.ClientState.LocalPlayer is not ICharacter localCharacter)
            return;

        if (Service.TargetManager.Target is not ICharacter targetCharacter)
            return;

        var rotToTarget = CommonHelper.GetRotationToTarget(localCharacter, targetCharacter);

        var c = CommonHelper.GetCharacter(localCharacter);
        c->Rotation = rotToTarget;
    }

    public static void Dispose()
    {
        foreach (var trackedCharacter in TrackedCharacters)
        {
            var character = CommonHelper.TryGetPlayerCharacterFromCID(trackedCharacter.CID);

            if (character != null)
                Stop(Service.Player, character);
        }

        Service.Player.Dispose();

        Service.Framework.Update -= OnFrameworkUpdate;
    }
}
