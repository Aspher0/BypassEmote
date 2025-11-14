using BypassEmote.Helpers;
using BypassEmote.Models;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using Lumina.Excel.Sheets;
using NoireLib;
using NoireLib.Helpers;
using NoireLib.Internal;
using System.Collections.Generic;

namespace BypassEmote;

internal static unsafe class EmotePlayer
{
    private static bool UpdateHooked;

    // List of characters using a looped emote
    public static List<TrackedCharacter> TrackedCharacters = new List<TrackedCharacter>();
    private static DelayedTrigger delayedTrigger = new(500);

    public static void PlayEmote(ICharacter? chara, Emote emote)
    {
        if (chara == null)
            return;

        if (NoireService.ClientState.IsGPosing)
            return;

        if (NoireService.ClientState.LocalPlayer != null && chara.Address == NoireService.ClientState.LocalPlayer.Address)
        {
            if (NoireService.ClientState.LocalPlayer.IsCasting ||
                NoireService.ClientState.LocalPlayer.IsDead)
                return;

            if (NoireService.Condition.Any(
                ConditionFlag.OccupiedInCutSceneEvent,
                ConditionFlag.WatchingCutscene,
                ConditionFlag.WatchingCutscene78,
                ConditionFlag.OccupiedInEvent,
                ConditionFlag.OccupiedInQuestEvent))
                return;
        }

        var native = CharacterHelper.GetCharacterAddress(chara);

        var emotePlayType = CommonHelper.GetEmotePlayType(emote);

        if (emotePlayType == EmotePlayType.DoNotPlay)
            return;

        if (chara is not INpc && !IsCharacterInBypassedLoop(chara) && (native->Mode != CharacterModes.Normal || CharacterHelper.IsCharacterSleeping(chara)))
        {
            if (CharacterHelper.IsCharacterSleeping(chara) || (native->Mode != CharacterModes.EmoteLoop && native->Mode != CharacterModes.InPositionLoop && native->Mode != CharacterModes.Mounted && native->Mode != CharacterModes.RidingPillion))
            {
                // Block: Not in allowed modes
                if (NoireService.ClientState.LocalPlayer != null && chara.Address == NoireService.ClientState.LocalPlayer.Address)
                    NoireLogger.PrintToChat("You cannot bypass this emote right now.");
                return;
            }

            if (emotePlayType != EmotePlayType.OneShot)
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
            case EmotePlayType.Looped:
                {
                    PlayEmote(Service.Player, chara, emote);
                    var trackedCharacter = CommonHelper.AddOrUpdateCharacterInTrackedList(chara.Address, emote);

                    if (trackedCharacter == null) break;

                    trackedCharacter.UpdateLastPosition();

                    if (!UpdateHooked && TrackedCharacters.Count > 0)
                    {
                        NoireService.Framework.Update += OnFrameworkUpdate;
                        UpdateHooked = true;
                    }

                    break;
                }
            case EmotePlayType.OneShot:
            default:
                {
                    ushort timelineId = (ushort)emote.ActionTimeline[0].RowId;

                    var specifications = CommonHelper.TryGetEmoteSpecification(emote);
                    if (specifications != null && specifications.SpecificOneShotActionTimelineSlot.HasValue) // Some emotes have the one-shot timeline in slot 4 (i.e. waterflip)
                        timelineId = (ushort)emote.ActionTimeline[specifications.SpecificOneShotActionTimelineSlot.Value].RowId;

                    if (timelineId == 0)
                        return;
                    StopLoop(chara, true);
                    PlayOneShotEmote(chara, timelineId);
                    break;
                }
        }

        var local = NoireService.ClientState.LocalPlayer;
        if (local != null && local.Address == chara.Address)
        {
            // Fire IPC event after delay only if local player is the one playing the emote
            var provider = IpcProvider.Instance;

            delayedTrigger.Start(() =>
            {
                provider?.LocalEmotePlayed?.Invoke(local, emote.RowId);
            });
        }
    }

    public static bool IsCharacterInBypassedLoop(ICharacter chara)
    {
        var foundCharacter = CommonHelper.TryGetTrackedCharacterFromAddress(chara.Address);
        return foundCharacter != null;
    }

    public static void PlayEmoteById(ICharacter? chara, uint emoteId)
    {
        if (chara == null)
            return;

        var local = NoireService.ClientState.LocalPlayer;
        if (local != null && chara.Address == local.Address)
            return;

        if (emoteId == 0)
        {
            StopLoop(chara, true);
            return;
        }

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

        var specifications = CommonHelper.TryGetEmoteSpecification(emote);

        if (specifications != null && specifications.SpecificLoopActionTimelineSlot.HasValue) // Some emotes have the loop timeline in slot 4 (i.e. waterfloat)
            loop = (ushort)emote.ActionTimeline[specifications.SpecificLoopActionTimelineSlot.Value].RowId;

        if (blendIntro && intro != 0)
            player.ExperimentalBlend(actor, intro, 1);

        if (loop != 0)
        {
            player.Play(actor, emote, loop, false);
            return;
        }

        /* Commented out because it seems unnecessary to check for other timelines if the loop timeline is not defined.
         * In practice, emotes without a loop timeline are usually one-shot emotes, which are handled with PlayOneShotEmote.
         * I'm keeping it just in case
         */

        //ushort upper = (ushort)emote.ActionTimeline[4].RowId; // Upper-body (SimpleBlend), I don't think this one is needed, it will never happen probably
        //if (upper != 0)
        //{
        //    player.ExperimentalBlend(actor, upper);
        //    return;
        //}

        //for (int i = 0; i < emote.ActionTimeline.Count; i++)
        //{
        //    var id = (ushort)emote.ActionTimeline[i].RowId;
        //    if (id == 0) continue;

        //    if (i == 4)
        //        player.ExperimentalBlend(actor, id);
        //    else
        //        player.Play(actor, emote, id, false);
        //    return;
        //}
    }

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
            var sheet = ExcelSheetHelper.GetSheet<Emote>();
            if (sheet != null)
            {
                foreach (var e in sheet)
                {
                    if ((ushort)e.ActionTimeline[0].RowId == timelineId)
                    {
                        ushort upperBody = (ushort)e.ActionTimeline[4].RowId;
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

        Service.Player.ExperimentalBlend(chara, timelineId);
    }

    public static void Stop(ActionTimelinePlayer player, ICharacter character, bool ShouldNotifyIpc, bool force = false)
    {
        var local = NoireService.ClientState.LocalPlayer;
        if (ShouldNotifyIpc && character is IPlayerCharacter playerCharacter && local != null && local.Address == character.Address)
        {
            // Fire IPC event only if local player is stopping a looped emote
            var trackedCharacter = CommonHelper.TryGetTrackedCharacterFromAddress(character.Address);

            if (trackedCharacter != null)
            {
                // Tell IPC Callers that the emote has stopped immediately and again after the delay
                // Kinda hacky-whacky way to ensure the emote stop is registered properly with sync but it works.
                // This is needed to avoid the server position desync issue.
                // When player A moves and bypasses an emote, this player might still be moving on player B's screen when player A starts the emote, causing a false-positive "stop emote" message
                uint playingEmoteId = trackedCharacter.PlayingEmoteId ?? 0;
                var provider = IpcProvider.Instance;
                provider?.LocalEmotePlayed?.Invoke(playerCharacter, 0);

                delayedTrigger.Start(() =>
                {
                    provider?.LocalEmotePlayed?.Invoke(playerCharacter, 0);
                });
            }
        }

        player.Stop(character, force);
    }

    //public static void StopToIdle(ActionTimelinePlayer player, ICharacter character) => player.StopToIdle(character);

    public static void StopLoop(ICharacter? chara, bool shouldRemoveFromList)
    {
        if (chara == null) return;

        var trackedCharacter = CommonHelper.TryGetTrackedCharacterFromAddress(chara.Address);

        if (trackedCharacter == null) return;

        if (shouldRemoveFromList)
            trackedCharacter.ScheduledForRemoval = true;

        Stop(Service.Player, chara, chara is IPlayerCharacter && shouldRemoveFromList);

        if (shouldRemoveFromList)
        {
            CommonHelper.RemoveCharacterFromTrackedListByUniqueID(trackedCharacter.UniqueId);

            if (TrackedCharacters.Count == 0 && UpdateHooked)
            {
                NoireService.Framework.Update -= OnFrameworkUpdate;
                UpdateHooked = false;
            }
        }
    }

    private static void OnFrameworkUpdate(IFramework framework)
    {
        /*
         * I know Stop() invokes the IPC to send a stop message to mare, so you might think it doesn't make sense to track other characters on framework update.
         * The issue is that if the stop message is shomehow not properly sent or received, the character would be stuck in the looped emote forever.
         * To avoid this, we track every characters, except if it's the local player, any position or rotation change will stop the emote.
         * If this is another player, we allow a small margin of error to avoid stopping the emote on minor movements caused by server position/rotation desync.
         */

        foreach (var trackedCharacter in TrackedCharacters)
        {
            var character = CommonHelper.TryGetCharacterFromTrackedCharacter(trackedCharacter);

            if (character == null)
            {
                CommonHelper.RemoveCharacterFromTrackedListByUniqueID(trackedCharacter.UniqueId);
                return;
            }

            var isLocalPlayer = NoireService.ClientState.LocalPlayer != null && character.Address == NoireService.ClientState.LocalPlayer.Address;

            var charaName = character.Name.TextValue;
            var trackedChara = CommonHelper.TryGetCharacterFromTrackedCharacter(trackedCharacter);

            if (trackedChara == null || !CharacterHelper.IsCharacterInObjectTable(trackedChara))
            {
                StopLoop(character, true);
                return;
            }

            var pos = character.Position;
            var deltaPosDistance = MathHelper.Distance(pos, trackedCharacter.LastPlayerPosition);
            if ((isLocalPlayer && pos != trackedCharacter.LastPlayerPosition) ||
                (!isLocalPlayer && deltaPosDistance > 0.5))
            // 0.5 of margin of error for other players, in case the "stop emote" message is not correctly sent/received, this acts as a "failsafe" for that scenario
            {
                StopLoop(character, true);
                return;
            }

            var rot = character.Rotation;
            var normalizedCurrentRotation = MathHelper.NormalizeAngle(MathHelper.ToDegrees(rot));
            var normalizedLastObservedRotation = MathHelper.NormalizeAngle(MathHelper.ToDegrees(trackedCharacter.LastPlayerRotation));
            var difference = MathHelper.Abs(MathHelper.DeltaAngle(normalizedCurrentRotation, normalizedLastObservedRotation));
            if ((isLocalPlayer && rot != trackedCharacter.LastPlayerRotation) ||
                (!isLocalPlayer && difference > 20))
            // Same reason as for the position, 20 degrees of margin of error for other players
            {
                if (trackedCharacter.PlayingEmoteId.HasValue)
                {
                    // Check if the looped emote should end on rotation with EmoteMode.Camera (true = reset on rotate ?)
                    var emote = EmoteHelper.GetEmoteById(trackedCharacter.PlayingEmoteId.Value);
                    if (emote != null && emote.Value.EmoteMode.RowId != 0 && emote.Value.EmoteMode.Value.Camera)
                    {
                        StopLoop(character, true);
                        return;
                    }
                }
            }

            var isWeaponDrawn = CharacterHelper.IsCharacterWeaponDrawn(character.Address);
            if (isWeaponDrawn != trackedCharacter.IsWeaponDrawn)
            {
                StopLoop(character, true);
                return;
            }
        }
    }

    //From SimpleHeels by Caraxi to sync NPCs
    public static void SyncEmotes(bool shouldSyncAll)
    {
        List<(ICharacter Character, bool IsTracked, TrackedCharacter? TrackedCharacter)> charactersToSync = new List<(ICharacter Chara, bool IsTracked, TrackedCharacter? TrackedCharacter)>();

        if (shouldSyncAll)
        {
            var objectTable = NoireService.ObjectTable;
            foreach (var obj in objectTable)
            {
                if (obj is IPlayerCharacter || obj is INpc)
                {
                    var trackedCharacter = CommonHelper.TryGetTrackedCharacterFromAddress(obj.Address);
                    var isTracked = trackedCharacter != null;

                    if (obj is IPlayerCharacter character)
                        charactersToSync.Add((character, isTracked, trackedCharacter));
                    else if (obj is INpc npc)
                        charactersToSync.Add((npc, isTracked, trackedCharacter));
                }
            }
        }
        else
        {
            foreach (var trackedCharacter in TrackedCharacters)
            {
                var character = CommonHelper.TryGetCharacterFromTrackedCharacter(trackedCharacter);
                if (character == null) continue;
                charactersToSync.Add((character, true, trackedCharacter));
            }
        }

        // If the player is a tracked character, restart their emote directly to trigger the sound again
        // If it's just a player, do it the simple heels way

        foreach (var characterToSync in charactersToSync)
        {
            if (characterToSync.IsTracked && characterToSync.TrackedCharacter != null && characterToSync.TrackedCharacter.PlayingEmoteId != null)
            {
                var emote = EmoteHelper.GetEmoteById(characterToSync.TrackedCharacter.PlayingEmoteId.Value);
                if (emote.HasValue)
                {
                    PlayEmote(Service.Player, characterToSync.Character, emote.Value);
                    continue;
                }
            }

            var charaAddress = CharacterHelper.GetCharacterAddress(characterToSync.Character);
            if (charaAddress->DrawObject == null) continue;
            if (charaAddress->DrawObject->GetObjectType() != ObjectType.CharacterBase) continue;
            if (((CharacterBase*)charaAddress->DrawObject)->GetModelType() != CharacterBase.ModelType.Human) continue;
            var human = (Human*)charaAddress->DrawObject;
            var skeleton = human->Skeleton;
            if (skeleton == null) continue;
            for (var i = 0; i < skeleton->PartialSkeletonCount && i < 1; ++i)
            {
                var partialSkeleton = &skeleton->PartialSkeletons[i];
                var animatedSkeleton = partialSkeleton->GetHavokAnimatedSkeleton(0);
                if (animatedSkeleton == null) continue;
                for (var animControl = 0; animControl < animatedSkeleton->AnimationControls.Length && animControl < 1; ++animControl)
                {
                    var control = animatedSkeleton->AnimationControls[animControl].Value;
                    if (control == null) continue;
                    control->hkaAnimationControl.LocalTime = 0;
                }
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

        var rotToTarget = CommonHelper.GetRotationToTarget(localCharacter, targetCharacter);

        var character = CharacterHelper.GetCharacterAddress(localCharacter);
        character->Rotation = rotToTarget;
    }

    public static void Dispose()
    {
        foreach (var trackedCharacter in TrackedCharacters)
        {
            var character = CommonHelper.TryGetCharacterFromTrackedCharacter(trackedCharacter);

            if (character != null)
            {
                trackedCharacter.ScheduledForRemoval = true;
                Stop(Service.Player, character, character is IPlayerCharacter, true);
            }
        }

        Service.Player.Dispose();
        delayedTrigger.Dispose();
        NoireService.Framework.Update -= OnFrameworkUpdate;
    }
}
