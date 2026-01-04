using BypassEmote.Helpers;
using BypassEmote.IPC;
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
using System.Collections.Generic;
using System.Linq;

namespace BypassEmote;

internal static unsafe class EmotePlayer
{
    private static bool UpdateHooked;

    // List of characters using a looped emote
    public static List<TrackedCharacter> TrackedCharacters = new List<TrackedCharacter>();

    public static void PlayEmote(ICharacter? chara, Emote emote, IpcData? receivedIpcData = null)
    {
        if (chara == null)
            return;

        if (NoireService.ClientState.IsGPosing)
            return;

        if (NoireService.ObjectTable.LocalPlayer != null && chara.Address == NoireService.ObjectTable.LocalPlayer.Address)
        {
            if (NoireService.ObjectTable.LocalPlayer.IsCasting ||
                NoireService.ObjectTable.LocalPlayer.IsDead)
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

        if (chara is not INpc && chara is not IBattleNpc && !CommonHelper.IsCharacterInBypassedLoop(chara) && (native->Mode != CharacterModes.Normal || CharacterHelper.IsCharacterSleeping(chara)))
        {
            if (CharacterHelper.IsCharacterSleeping(chara) || (native->Mode != CharacterModes.EmoteLoop && native->Mode != CharacterModes.InPositionLoop && native->Mode != CharacterModes.Mounted && native->Mode != CharacterModes.RidingPillion))
            {
                // Block: Not in allowed modes
                if (NoireService.ObjectTable.LocalPlayer != null && chara.Address == NoireService.ObjectTable.LocalPlayer.Address)
                    NoireLogger.PrintToChat("You cannot bypass this emote right now.");
                return;
            }

            if (emotePlayType != EmotePlayType.OneShot)
            {
                // Block: In EmoteLoop/InPositionLoop but not OneShot
                if (NoireService.ObjectTable.LocalPlayer != null && chara.Address == NoireService.ObjectTable.LocalPlayer.Address)
                    NoireLogger.PrintToChat("You cannot bypass this emote right now.");
                return;
            }
        }

        var emoteCategory = EmoteHelper.GetEmoteCategory(emote);
        if (emoteCategory != NoireLib.Enums.EmoteCategory.Expressions)
            StopLoop(chara, false);

        if (NoireService.ObjectTable.LocalPlayer != null &&
            chara.Address == NoireService.ObjectTable.LocalPlayer.Address &&
            emoteCategory != NoireLib.Enums.EmoteCategory.Expressions)
            CommonHelper.FaceTarget();

        switch (emotePlayType)
        {
            case EmotePlayType.Looped:
                {
                    PlayEmote(Service.Player, chara, emote);
                    var trackedCharacter = CommonHelper.AddOrUpdateCharacterInTrackedList(chara.Address, emote, receivedIpcData);

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

                    if (emoteCategory != NoireLib.Enums.EmoteCategory.Expressions)
                        StopLoop(chara, true); // I know this is redundant but this will ensure any looping emote will be completely stopped and removed from tracked list

                    PlayOneShotEmote(chara, timelineId);
                    break;
                }
        }

        IpcHelper.NotifyEmoteStart(chara, emote);
    }

    public static void OnReceiveIPCData(ICharacter chara, IpcData ipcData)
    {
        if (chara == null)
            return;

#if DEBUG
        if (ipcData.ActionType == ActionType.Unknown)
            NoireLogger.LogWarning("Received IPC Data with Unknown ActionType.");
#endif

        if (ipcData.ActionType == ActionType.ConfigUpdate)
        {
            // Update every object owned by the player
            var trackedCharacters = TrackedCharacters.Where(tc =>
            {
                return tc.ReceivedIpcData?.IsCharacterOrBelongsToIt(ipcData.CharacterAddress) ?? false;
            });

            foreach (var trackedCharacter in trackedCharacters)
            {
                trackedCharacter.ReceivedIpcData?.UpdateConfig(ipcData);
            }
            return;
        }

        if (ipcData.ActionType == ActionType.StopEmote)
        {
            StopLoop(chara, true);
            return;
        }

        var emoteId = ipcData.EmoteId;
        var emote = EmoteHelper.GetEmoteById(emoteId);

        if (emote == null)
            return;

        PlayEmote(chara, emote.Value, ipcData);
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
            player.ExperimentalBlend(actor, emote, intro, 1);

        if (loop != 0)
        {
            player.Play(actor, emote, loop, false);
            return;
        }
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

        Service.Player.ExperimentalBlend(chara, null, timelineId);
    }

    public static void Stop(ActionTimelinePlayer player, ICharacter character, bool ShouldNotifyIpc, bool force = false)
    {
        if (ShouldNotifyIpc)
            IpcHelper.NotifyEmoteStop(character);

        player.Stop(character, force);
    }

    public static void StopLoop(ICharacter? chara, bool shouldRemoveFromList)
    {
        if (chara == null) return;

        var trackedCharacter = CommonHelper.TryGetTrackedCharacterFromAddress(chara.Address);

        if (trackedCharacter == null) return;

        if (shouldRemoveFromList)
            trackedCharacter.ScheduledForRemoval = true;

        Stop(Service.Player, chara, CommonHelper.IsLocalObject(chara) && shouldRemoveFromList);

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
         * To avoid this, we track every characters and stop emotes accordingly.
         * If this is another player, we allow a small margin of error to avoid stopping the emote on minor movements caused by server position/rotation desync.
         */

        foreach (var trackedCharacter in TrackedCharacters)
        {
            var character = CommonHelper.TryGetCharacterFromTrackedCharacter(trackedCharacter);

            if (character == null || !CharacterHelper.IsCharacterInObjectTable(character))
            {
                CommonHelper.RemoveCharacterFromTrackedListByUniqueID(trackedCharacter.UniqueId);
                return;
            }

            var isLocalPlayerOwned = CommonHelper.IsLocalObject(character);
            var isOtherPlayerOwned = trackedCharacter.ReceivedIpcData?.IsOwnedObject ?? false;
            var isNpc = character is INpc || character is IBattleNpc;

            // Determine the character type and ownership
            bool isTrueNpc = isNpc && !isLocalPlayerOwned && !isOtherPlayerOwned;
            bool isOtherPlayer = character is IPlayerCharacter && !isLocalPlayerOwned;
            bool isLocallyOwnedObject = isNpc && isLocalPlayerOwned;
            bool isRemotelyOwnedObject = isNpc && isOtherPlayerOwned;

            // Determine if we should use margin of error
            bool useMarginOfError = isOtherPlayer;

            // Determine the stop emote behavior based on ownership
            bool shouldUseConfigForStop = isLocallyOwnedObject;
            bool shouldUseIpcConfigForStop = isRemotelyOwnedObject;

            // Position tracking
            var pos = character.Position;
            var deltaPosDistance = MathHelper.Distance(pos, trackedCharacter.LastPlayerPosition);

            bool positionChanged = false;
            if (useMarginOfError)
            {
                // Other players: use margin of error
                positionChanged = deltaPosDistance > 0.5;
            }
            else
            {
                // Local player, true NPCs, and owned objects: no margin of error
                positionChanged = pos != trackedCharacter.LastPlayerPosition;
            }

            if (positionChanged)
            {
                bool shouldStop = true;

                if (shouldUseConfigForStop)
                    shouldStop = Configuration.Instance.StopOwnedObjectEmoteOnMove;
                else if (shouldUseIpcConfigForStop)
                    shouldStop = trackedCharacter.ReceivedIpcData?.StopOwnedObjectEmoteOnMove ?? false;

                if (shouldStop)
                {
                    StopLoop(character, true);
                    return;
                }
            }

            // Rotation tracking
            var rot = character.Rotation;
            var normalizedCurrentRotation = MathHelper.NormalizeAngle(MathHelper.ToDegrees(rot));
            var normalizedLastObservedRotation = MathHelper.NormalizeAngle(MathHelper.ToDegrees(trackedCharacter.LastPlayerRotation));
            var difference = MathHelper.Abs(MathHelper.DeltaAngle(normalizedCurrentRotation, normalizedLastObservedRotation));

            bool rotationChanged = false;
            if (useMarginOfError)
            {
                // Other players: use margin of error
                rotationChanged = difference > 20;
            }
            else
            {
                // Local player, true NPCs, and owned objects: no margin of error
                rotationChanged = rot != trackedCharacter.LastPlayerRotation;
            }

            if (rotationChanged)
            {
                // Check if the looped emote should end on rotation with EmoteMode.Camera
                if (trackedCharacter.PlayingEmoteId.HasValue)
                {
                    var emote = EmoteHelper.GetEmoteById(trackedCharacter.PlayingEmoteId.Value);
                    if (emote != null && emote.Value.EmoteMode.RowId != 0 && emote.Value.EmoteMode.Value.Camera)
                    {
                        bool shouldStop = true;

                        if (shouldUseConfigForStop)
                            shouldStop = Configuration.Instance.StopOwnedObjectEmoteOnMove;
                        else if (shouldUseIpcConfigForStop)
                            shouldStop = trackedCharacter.ReceivedIpcData?.StopOwnedObjectEmoteOnMove ?? false;

                        if (shouldStop)
                        {
                            StopLoop(character, true);
                            return;
                        }
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
                if (obj is IPlayerCharacter || obj is INpc || obj is IBattleNpc)
                {
                    var trackedCharacter = CommonHelper.TryGetTrackedCharacterFromAddress(obj.Address);
                    var isTracked = trackedCharacter != null;

                    if (obj is IPlayerCharacter character)
                        charactersToSync.Add((character, isTracked, trackedCharacter));
                    else if (obj is INpc || obj is IBattleNpc)
                        charactersToSync.Add(((ICharacter)obj, isTracked, trackedCharacter));
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
                    //PlayEmote(Service.Player, characterToSync.Character, emote.Value); // Causes slight desync on looped emotes with intro
                    ushort loop = (ushort)emote.Value.ActionTimeline[0].RowId;
                    Service.Player.ExperimentalBlend(characterToSync.Character, emote, loop); // Seems to work better for loop anims with an intro, otherwise there will be a slight desync
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

    public static void Dispose()
    {
        foreach (var trackedCharacter in TrackedCharacters)
        {
            var character = CommonHelper.TryGetCharacterFromTrackedCharacter(trackedCharacter);

            if (character != null)
            {
                trackedCharacter.ScheduledForRemoval = true;
                Stop(Service.Player, character, CommonHelper.IsLocalObject(character), true);
            }
        }

        Service.Player.Dispose();
        NoireService.Framework.Update -= OnFrameworkUpdate;
        UpdateHooked = false;
    }
}
