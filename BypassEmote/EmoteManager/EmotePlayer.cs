using BypassEmote.Data;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using Lumina.Excel.Sheets;
using NoireLib;
using NoireLib.Helpers;
using System.Collections.Generic;

namespace BypassEmote;

internal static unsafe class EmotePlayer
{
    private static bool UpdateHooked;

    // List of characters using a looped emote
    public static List<TrackedCharacter> TrackedCharacters = new List<TrackedCharacter>();

    public static void PlayEmote(ICharacter? chara, Emote emote)
    {
        if (chara == null)
            return;

        var native = CharacterHelper.GetCharacterAddress(chara);

        var emotePlayType = Helpers.CommonHelper.GetEmotePlayType(emote);

        if (emotePlayType == EmoteData.EmotePlayType.DoNotPlay)
            return;

        if (chara is not INpc && (native->Mode != CharacterModes.Normal || CharacterHelper.IsCharacterSleeping(chara)))
        {
            if (CharacterHelper.IsCharacterSleeping(chara) || (native->Mode != CharacterModes.EmoteLoop && native->Mode != CharacterModes.InPositionLoop && native->Mode != CharacterModes.Mounted && native->Mode != CharacterModes.RidingPillion))
            {
                // Block: Not in allowed modes
                if (NoireService.ClientState.LocalPlayer != null && chara.Address == NoireService.ClientState.LocalPlayer.Address)
                    NoireLogger.PrintToChat("You cannot bypass this emote right now.");
                return;
            }

            if (emotePlayType != EmoteData.EmotePlayType.OneShot)
            {
                // Block: In EmoteLoop/InPositionLoop but not OneShot
                if (NoireService.ClientState.LocalPlayer != null && chara.Address == NoireService.ClientState.LocalPlayer.Address)
                    NoireLogger.PrintToChat("You cannot bypass this emote right now.");
                return;
            }
        }

        StopLoop(chara, false);

        if (NoireService.ClientState.LocalPlayer != null && chara.Address == NoireService.ClientState.LocalPlayer.Address)
            FaceTarget();

        switch (emotePlayType)
        {
            case EmoteData.EmotePlayType.Looped:
                {
                    PlayEmote(Service.Player, chara, emote);
                    var trackedCharacter = Helpers.CommonHelper.AddOrUpdateCharacterInTrackedList(chara.Address);

                    if (trackedCharacter == null) break;

                    trackedCharacter.UpdateLastPosition();

                    if (!UpdateHooked && TrackedCharacters.Count > 0)
                    {
                        NoireService.Framework.Update += OnFrameworkUpdate;
                        UpdateHooked = true;
                    }

                    break;
                }
            case EmoteData.EmotePlayType.OneShot:
            default:
                {
                    ushort timelineId = (ushort)emote.ActionTimeline[0].RowId;
                    if (timelineId == 0)
                        return;
                    Helpers.CommonHelper.RemoveCharacterFromTrackedListByAddress(chara.Address);
                    PlayOneShotEmote(chara, timelineId);
                    break;
                }
        }

        var local = NoireService.ClientState.LocalPlayer;
        if (local != null && local.Address == (nint)CharacterHelper.GetCharacterAddress(chara))
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

        var sheet = NoireService.DataManager.GetExcelSheet<Emote>();
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
            player.Play(actor, loop, false);
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
                player.Play(actor, id, false);
            return;
        }
    }

    public static void Stop(ActionTimelinePlayer player, ICharacter actor) => player.Stop(actor);
    public static void StopToIdle(ActionTimelinePlayer player, ICharacter actor) => player.StopToIdle(actor);

    public static void PlayOneShotEmote(ICharacter? chara, ushort timelineId)
    {
        if (chara == null) return;

        var native = CharacterHelper.GetCharacterAddress(chara);

        if (CharacterHelper.IsCharacterSleeping(chara))
            return;

        bool isSitting = CharacterHelper.IsCharacterChairSitting(chara) || CharacterHelper.IsCharacterGroundSitting(chara);
        bool isMounted = CharacterHelper.IsCharacterMounted(chara);
        bool isRidingPillion = CharacterHelper.IsCharacterRidingPillion(chara);

        if (isSitting || isMounted || isRidingPillion)
        {
            var sheet = Service.EmoteSheet;
            if (sheet != null)
            {
                foreach (var emote in sheet)
                {
                    if ((ushort)emote.ActionTimeline[0].RowId == timelineId)
                    {
                        ushort upperBody = (ushort)emote.ActionTimeline[4].RowId;
                        if (upperBody != 0)
                        {
                            native->Timeline.TimelineSequencer.PlayTimeline(upperBody);
                            return;
                        }
                        break;
                    }
                }
            }
            return;
        }

        native->Timeline.TimelineSequencer.PlayTimeline(timelineId);
    }

    public static void StopLoop(ICharacter? chara, bool shouldRemoveFromList)
    {
        if (chara == null) return;

        var trackedCharacter = Helpers.CommonHelper.TryGetTrackedCharacterFromAddress(chara.Address);

        if (trackedCharacter == null) return;

        if (chara is INpc)
            Stop(Service.Player, chara);
        else
            StopToIdle(Service.Player, chara);

        if (shouldRemoveFromList)
        {
            Helpers.CommonHelper.RemoveCharacterFromTrackedListByUniqueID(trackedCharacter.UniqueId);

            if (TrackedCharacters.Count == 0 && UpdateHooked)
            {
                NoireService.Framework.Update -= OnFrameworkUpdate;
                UpdateHooked = false;
            }
        }
    }

    private static void OnFrameworkUpdate(IFramework framework)
    {
        foreach (var trackedCharacter in TrackedCharacters)
        {
            var character = Helpers.CommonHelper.TryGetCharacterFromTrackedCharacter(trackedCharacter);

            if (character == null)
            {
                Helpers.CommonHelper.RemoveCharacterFromTrackedListByUniqueID(trackedCharacter.UniqueId);
                return;
            }

            var charaName = character.Name.TextValue;
            var trackedChara = Helpers.CommonHelper.TryGetCharacterFromTrackedCharacter(trackedCharacter);

            if (trackedChara == null || !CharacterHelper.IsCharacterInObjectTable(trackedChara))
            {
                StopLoop(character, true);
                return;
            }

            // Check if position has changed
            var pos = character.Position;
            var delta = pos - trackedCharacter.LastPlayerPosition;
            if (delta.LengthSquared() > 1e-3f)
            {
                StopLoop(character, true);
                return;
            }

            // Check if rotation has changed
            var rot = character.Rotation;
            if (Service.InterruptEmoteOnRotate && System.Math.Abs(rot - trackedCharacter.LastPlayerRotation) > 1e-7f)
            {
                StopLoop(character, true);
                return;
            }

            // Check if weapon drawn state has changed
            var isWeaponDrawn = CharacterHelper.IsCharacterWeaponDrawn(character.Address);
            if (isWeaponDrawn != trackedCharacter.IsWeaponDrawn)
            {
                StopLoop(character, true);
                return;
            }
        }
    }

    public unsafe static void FaceTarget()
    {
        if (NoireService.ClientState.LocalPlayer is not ICharacter localCharacter ||
            NoireService.TargetManager.Target is not ICharacter targetCharacter)
            return;

        if (CharacterHelper.IsCharacterChairSitting(localCharacter) ||
            CharacterHelper.IsCharacterGroundSitting(localCharacter) ||
            CharacterHelper.IsCharacterSleeping(localCharacter))
            return;

        var rotToTarget = Helpers.CommonHelper.GetRotationToTarget(localCharacter, targetCharacter);

        var character = CharacterHelper.GetCharacterAddress(localCharacter);
        character->Rotation = rotToTarget;
    }

    public static void Dispose()
    {
        foreach (var trackedCharacter in TrackedCharacters)
        {
            var character = Helpers.CommonHelper.TryGetCharacterFromTrackedCharacter(trackedCharacter);

            if (character is IPlayerCharacter)
                StopToIdle(Service.Player, character);
            else if (character is INpc)
                Stop(Service.Player, character);
        }

        Service.Player.Dispose();

        NoireService.Framework.Update -= OnFrameworkUpdate;
    }
}
