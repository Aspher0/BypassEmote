using BypassEmote.Helpers;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Memory;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using Lumina.Excel.Sheets;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace BypassEmote;

// Borrowed and adapted from Brio (https://github.com/Etheirys/Brio)

/// <summary>
/// Standalone action-timeline based emote player that mirrors Brio's core behavior
/// without depending on any Brio types. Works outside GPose for any ICharacter.
/// </summary>
public sealed class ActionTimelinePlayer : IDisposable
{
    // Per-character state tracking
    private readonly Dictionary<nint, CharacterState> states = new();

    public void Dispose()
    {
        states.Clear();
    }

    //public unsafe float GetOverallSpeed(ICharacter character)
    //{
    //    var native = GetNative(character);
    //    return native->Timeline.OverallSpeed;
    //}

    //public unsafe void SetOverallSpeed(ICharacter character, float speed)
    //{
    //    var native = GetNative(character);
    //    GetOrCreateState(character).OverallSpeedOverride = speed;
    //    native->Timeline.OverallSpeed = speed;
    //}

    //public void ResetOverallSpeed(ICharacter character)
    //{
    //    if (!states.TryGetValue(character.Address, out var state))
    //        return;

    //    state.OverallSpeedOverride = null;
    //}

    //public unsafe void SetLipsOverride(ICharacter character, ushort lipsTimeline)
    //{
    //    var native = GetNative(character);
    //    native->Timeline.SetLipsOverrideTimeline(lipsTimeline);
    //    GetOrCreateState(character).LipsOverride = lipsTimeline;
    //}

    //public void ClearLipsOverride(ICharacter character)
    //{
    //    if (states.TryGetValue(character.Address, out var state))
    //        state.LipsOverride = 0;
    //}

    //public unsafe ushort GetCurrentBaseOverride(ICharacter character)
    //{
    //    var native = GetNative(character);
    //    return native->Timeline.BaseOverride;
    //}

    /// <summary>
    /// Play the given action timeline as a base override. If interrupt == true, the timeline is blended in immediately.
    /// </summary>
    public unsafe void Play(ICharacter character, Emote emote, ushort actionTimeline, bool interrupt = true)
    {
        var native = GetNative(character);
        var st = GetOrCreateState(character);

        var trackedCharacter = CommonHelper.TryGetTrackedCharacterFromAddress(character.Address);

        if (trackedCharacter is null && st.OriginalBase is null)
            st.OriginalBase = new OriginalBase(native->Mode, native->ModeParam, native->Timeline.BaseOverride);

        var emoteMode = emote.EmoteMode;
        var emoteModeId = emoteMode.RowId;

        //// 11 = InPositionLoop = Sit, GroundSit, Sleep
        //var newMode = emoteMode.Value.ConditionMode == 11 ? CharacterModes.InPositionLoop : CharacterModes.EmoteLoop;

        //var emotePlayType = CommonHelper.GetEmotePlayType(emote);

        //// Do not change mode for player characters since it can cause issues on the server if not handled properly.
        //if (character is IPlayerCharacter chara)
        //{
        //    var localChar = NoireService.ObjectTable.LocalPlayer;
        //    if (emotePlayType == Data.EmoteData.EmotePlayType.Looped && localChar != null && character.Address == localChar.Address)
        //        native->SetMode(newMode, (byte)emoteModeId);
        //}
        //else if (character is INpc npc)
        //    native->SetMode(newMode, (byte)emoteModeId);

        /*
         * Maybe I can change the character mode of others and not change it for the local player, but
         * the issue right now is that setting mode on others make their position "desync" and "jitter" with framework update
         * Also, it's kinda pointless to change the mode of others and not the local player, so I will not touch modes for now.
         */

        native->Timeline.BaseOverride = actionTimeline;

        if (interrupt)
            SimpleBlend(character, actionTimeline);
    }

    /// <summary>
    /// Trigger a one-shot blend timeline.
    /// </summary>
    public unsafe void SimpleBlend(ICharacter character, ushort actionTimeline)
    {
        var native = GetNative(character);
        native->Timeline.TimelineSequencer.PlayTimeline(actionTimeline);
    }

    // Thank you to Senko for the help and for providing this function
    public unsafe void ExperimentalBlend(ICharacter character, ushort actionTimeline, int prio = -1)
    {
        var native = GetNative(character);
        var animParams = (ActionTimelineAnimParams*)MemoryHelper.Allocate(0x60);
        Unsafe.InitBlockUnaligned(animParams, 0, 0x60);
        animParams->Unk0 = 0.0f; // unknown
        animParams->Unk4 = 0.0f; // unknown
        animParams->Unk8 = 0.0f; // unknown
        animParams->UnkC = 0.0f; // gets reset to 0.0f when played
        animParams->Unk10 = 0.0f; // unknown
        animParams->Intensity = 1.0f; // range from 0.0f to 1.0f
        animParams->StartTS = 0.0f; // time offset to start animation at. can pass 0.001f to skip some emote audios
        animParams->Unk1C = -1.0f; // unknown
        animParams->Unk20 = 0; // unknown
        animParams->TargetObjId = character.TargetObjectId; // target for dote/allsaintscharm etc.
        animParams->Unk30 = 0; // unknown
        animParams->Priority = (uint)prio; // -1 for anim default. using priority 0 gets the anim immediately cancelled by the idle pose
        animParams->Unk38 = -1; // unknown
        animParams->Unk3C = (actionTimeline == 3123) ? (byte)0 : (byte)0xFF; // original creator has no idea what they did here or why, and neither do I. 3123 corresponds to emote/pose01_start
        animParams->Unk42 = 0; // unknown
        native->Timeline.TimelineSequencer.PlayTimeline(actionTimeline, animParams);
        MemoryHelper.Free((nint)animParams);
    }

    [StructLayout(LayoutKind.Explicit, Size = 0x60)]
    public unsafe struct ActionTimelineAnimParams
    {
        [FieldOffset(0x00)] public nint vtblAddr;
        [FieldOffset(0x10)] public float Unk0;
        [FieldOffset(0x14)] public float Unk4;
        [FieldOffset(0x18)] public float Unk8;
        [FieldOffset(0x1C)] public float UnkC;
        [FieldOffset(0x20)] public float Unk10;
        [FieldOffset(0x24)] public float Intensity;
        [FieldOffset(0x28)] public float StartTS;
        [FieldOffset(0x2C)] public float Unk1C; // Default -1.0
        [FieldOffset(0x30)] public ulong Unk20;
        [FieldOffset(0x38)] public ulong TargetObjId;
        [FieldOffset(0x40)] public uint Unk30;
        [FieldOffset(0x44)] public uint Priority; // 0-7, or -1 for anim default
        [FieldOffset(0x48)] public int Unk38; // default -1 (0xFFFFFFFF)
        [FieldOffset(0x4C)] public byte Unk3C; // typically 0xFF or 0
        [FieldOffset(0x4D)] public byte Unk3D; // always 0?
        [FieldOffset(0x4E)] public byte Unk3E; // always 0?
        [FieldOffset(0x4F)] public byte Unk3F; // always 0?
        [FieldOffset(0x50)] public byte Unk40; // always 0?
        [FieldOffset(0x52)] public byte Unk42;
    }

    /// <summary>
    /// Reset the character's base override and original mode state and do a small blend to clear state.
    /// </summary>
    public unsafe void ResetBase(ICharacter character)
    {
        if (!states.TryGetValue(character.Address, out var state) || state.OriginalBase is null)
            return;

        var native = GetNative(character);
        var ob = state.OriginalBase.Value;

        // Do not change mode for player characters since it can cause issues on the server if not handled properly.
        //if (character is IPlayerCharacter)
        //{
        //    var localChar = NoireService.ObjectTable.LocalPlayer;

        //    if (localChar != null && character.Address == localChar.Address)
        //    {
        //        NoireLogger.LogDebug($"Restoring original mode {ob.OriginalMode} and param {ob.OriginalInput} for player character.");
        //        native->SetMode(ob.OriginalMode, ob.OriginalInput);
        //    }
        //}
        //else if (character is INpc)
        //    native->SetMode(ob.OriginalMode, ob.OriginalInput);

        native->Timeline.BaseOverride = ob.OriginalTimeline;

        state.OriginalBase = null;

        SimpleBlend(character, 3);
    }

    public unsafe void ResetToIdle(ICharacter character)
    {
        var native = GetNative(character);

        native->Timeline.BaseOverride = 0;
        SimpleBlend(character, 3);

        if (!states.TryGetValue(character.Address, out var state) || state.OriginalBase is null)
            return;

        var ob = state.OriginalBase.Value;

        state.OriginalBase = null;
    }

    public void Stop(ICharacter character, bool force)
    {
        var trackedCharacter = CommonHelper.TryGetTrackedCharacterFromAddress(character.Address);

        if (HasBaseOverride(character) && (force || (trackedCharacter != null && trackedCharacter.ScheduledForRemoval)))
        {
            ResetBase(character);
            //ResetOverallSpeed(character);
        }
    }

    //public void StopToIdle(ICharacter character)
    //{
    //    ResetToIdle(character);
    //    //ResetOverallSpeed(character);
    //}

    //public void Reset(ICharacter character)
    //{
    //    ClearLipsOverride(character);
    //    ResetPerSlotSpeeds(character);
    //    ResetBase(character);
    //    ResetOverallSpeed(character);
    //}

    public bool HasBaseOverride(ICharacter character)
    {
        return states.TryGetValue(character.Address, out var st) && st.OriginalBase is not null;
    }

    // --- Per-slot speed controls ---

    //public unsafe float GetSlotSpeed(ICharacter character, int slotIndex)
    //{
    //    var st = GetOrCreateState(character);
    //    if (st.SlotSpeedOverrides.TryGetValue(slotIndex, out var sp))
    //        return sp;

    //    var native = GetNative(character);
    //    return native->Timeline.TimelineSequencer.TimelineSpeeds[slotIndex];
    //}

    //public unsafe void SetSlotSpeed(ICharacter character, int slotIndex, float speed)
    //{
    //    var st = GetOrCreateState(character);
    //    st.SlotSpeedOverrides[slotIndex] = speed;
    //    var native = GetNative(character);
    //    native->Timeline.TimelineSequencer.SetSlotSpeed((uint)slotIndex, speed);
    //    st.SlotsDirty = true;
    //}

    //public void ResetSlotSpeed(ICharacter character, int slotIndex)
    //{
    //    var st = GetOrCreateState(character);
    //    st.SlotSpeedOverrides.Remove(slotIndex);
    //    st.SlotsDirty = true;
    //}

    //public void ResetPerSlotSpeeds(ICharacter character)
    //{
    //    if (states.TryGetValue(character.Address, out var st))
    //    {
    //        st.SlotSpeedOverrides.Clear();
    //        st.SlotsDirty = false;
    //    }
    //}

    //public bool CheckAndResetDirtySlots(ICharacter character)
    //{
    //    if (states.TryGetValue(character.Address, out var st) && st.SlotsDirty)
    //    {
    //        st.SlotsDirty = false;
    //        return true;
    //    }
    //    return false;
    //}

    // --- Helpers ---

    private CharacterState GetOrCreateState(ICharacter character)
    {
        if (!states.TryGetValue(character.Address, out var s))
        {
            s = new CharacterState();
            states[character.Address] = s;
        }
        return s;
    }

    private static unsafe Character* GetNative(ICharacter character)
        => (Character*)character.Address;

    private sealed class CharacterState
    {
        public OriginalBase? OriginalBase;
        public float? OverallSpeedOverride;
        public ushort LipsOverride;
        public bool SlotsDirty;
        public readonly Dictionary<int, float> SlotSpeedOverrides = new();
    }

    private readonly record struct OriginalBase(CharacterModes OriginalMode, byte OriginalInput, ushort OriginalTimeline);
}
