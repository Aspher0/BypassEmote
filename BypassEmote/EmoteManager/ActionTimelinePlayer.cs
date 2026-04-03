using BypassEmote.Helpers;
using BypassEmote.Models;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Memory;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using Lumina.Excel.Sheets;
using NoireLib;
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
    private readonly Dictionary<nint, CharacterState> characterStates = new();

    public void Dispose()
    {
        characterStates.Clear();
    }

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

        native->Timeline.BaseOverride = actionTimeline;

        if (interrupt)
            ExperimentalBlend(character, emote, actionTimeline);
    }

    public unsafe void ExperimentalBlend(ICharacter character, Emote? emote, ushort actionTimeline, int prio = -1, Models.CharacterState? characterState = null)
    {
        var native = GetNative(character);

        ulong charTargetId = 0xE0000000;

        if (characterState != null)
            charTargetId = characterState.TargetObject?.GameObjectId ?? 0xE0000000;
        else
            charTargetId = CommonHelper.GetPlayerTarget(character);

        var animParams = (ActionTimelineAnimParams*)MemoryHelper.Allocate(0x60);
        Unsafe.InitBlockUnaligned(animParams, 0, 0x60);
        animParams->Unk0 = 0.0f;
        animParams->Unk4 = 0.0f;
        animParams->Unk8 = 0.0f;
        animParams->UnkC = 0.0f;
        animParams->Unk10 = 0.0f;
        animParams->Intensity = 1.0f;
        animParams->StartTS = 0.0f;
        animParams->Unk1C = -1.0f;
        animParams->Unk20 = 0;
        animParams->TargetObjId = charTargetId;
        animParams->Unk30 = 0;
        animParams->Priority = (uint)prio;
        animParams->Unk38 = -1;
        animParams->Unk3C = (actionTimeline == 3123) ? (byte)0 : (byte)0xFF;
        animParams->Unk42 = 0;
        native->Timeline.TimelineSequencer.PlayTimeline(actionTimeline, animParams);
        MemoryHelper.Free((nint)animParams);
    }

    /// <summary>
    /// Reset the character's base override and original mode state and do a small blend to clear state.
    /// </summary>
    public unsafe void ResetBase(ICharacter character)
    {
        if (!characterStates.TryGetValue(character.Address, out var state) || state.OriginalBase is null)
            return;

        var native = GetNative(character);
        var ob = state.OriginalBase.Value;

        native->Timeline.BaseOverride = ob.OriginalTimeline;

        state.OriginalBase = null;

        ExperimentalBlend(character, null, 3);
    }

    public void Stop(ICharacter character, bool force)
    {
        var trackedCharacter = CommonHelper.TryGetTrackedCharacterFromAddress(character.Address);

        if (HasBaseOverride(character) && (force || (trackedCharacter != null && trackedCharacter.ScheduledForRemoval)))
            ResetBase(character);
    }

    public bool HasBaseOverride(ICharacter character)
        => characterStates.TryGetValue(character.Address, out var st) && st.OriginalBase is not null;

    // --- Helpers ---

    private CharacterState GetOrCreateState(ICharacter character)
    {
        if (!characterStates.TryGetValue(character.Address, out var s))
        {
            s = new CharacterState();
            characterStates[character.Address] = s;
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

[StructLayout(LayoutKind.Explicit, Size = 0x60)]
public struct ActionTimelineAnimParams
{
    [FieldOffset(0x00)] public nint vtblAddr;
    [FieldOffset(0x10)] public float Unk0;
    [FieldOffset(0x14)] public float Unk4;
    [FieldOffset(0x18)] public float Unk8;
    [FieldOffset(0x1C)] public float UnkC;
    [FieldOffset(0x20)] public float Unk10;
    [FieldOffset(0x24)] public float Intensity;
    [FieldOffset(0x28)] public float StartTS;
    [FieldOffset(0x2C)] public float Unk1C;
    [FieldOffset(0x30)] public ulong Unk20;
    [FieldOffset(0x38)] public ulong TargetObjId;
    [FieldOffset(0x40)] public uint Unk30;
    [FieldOffset(0x44)] public uint Priority; // 0-7, or -1 for anim default
    [FieldOffset(0x48)] public int Unk38;
    [FieldOffset(0x4C)] public byte Unk3C;
    [FieldOffset(0x4D)] public byte Unk3D;
    [FieldOffset(0x4E)] public byte Unk3E;
    [FieldOffset(0x4F)] public byte Unk3F;
    [FieldOffset(0x50)] public byte Unk40;
    [FieldOffset(0x52)] public byte Unk42;
}
