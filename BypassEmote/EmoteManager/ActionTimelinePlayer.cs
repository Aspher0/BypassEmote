using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;

namespace BypassEmote;

// Borrowed and adapted from Brio (https://github.com/Etheirys/Brio)

/// <summary>
/// Standalone action-timeline based emote player that mirrors Brio's core behavior
/// without depending on any Brio types. Works outside GPose for any ICharacter.
/// </summary>
public sealed class ActionTimelinePlayer : IDisposable
{
    // Per-character state tracking
    private readonly Dictionary<nint, CharacterState> _states = new();

    public void Dispose()
    {
        // Best-effort cleanup: just drop our state tracking.
        _states.Clear();
    }

    // --- Public API ---

    public unsafe float GetOverallSpeed(ICharacter character)
    {
        var native = GetNative(character);
        return native->Timeline.OverallSpeed;
    }

    public unsafe void SetOverallSpeed(ICharacter character, float speed)
    {
        var native = GetNative(character);
        GetOrCreateState(character).OverallSpeedOverride = speed;
        native->Timeline.OverallSpeed = speed;
    }

    public void ResetOverallSpeed(ICharacter character)
    {
        if (!_states.TryGetValue(character.Address, out var state))
            return;

        state.OverallSpeedOverride = null;
    }

    public unsafe void SetLipsOverride(ICharacter character, ushort lipsTimeline)
    {
        var native = GetNative(character);
        native->Timeline.SetLipsOverrideTimeline(lipsTimeline);
        GetOrCreateState(character).LipsOverride = lipsTimeline;
    }

    public void ClearLipsOverride(ICharacter character)
    {
        if (_states.TryGetValue(character.Address, out var state))
            state.LipsOverride = 0;
    }

    public unsafe ushort GetCurrentBaseOverride(ICharacter character)
    {
        var native = GetNative(character);
        return native->Timeline.BaseOverride;
    }

    /// <summary>
    /// Play the given action timeline as a base override. If interrupt == true, the timeline is blended in immediately.
    /// </summary>
    public unsafe void Play(ICharacter character, ushort actionTimeline, bool interrupt = true)
    {
        var native = GetNative(character);
        var st = GetOrCreateState(character);

        if (st.OriginalBase is null)
            st.OriginalBase = new OriginalBase(native->Mode, native->ModeParam, native->Timeline.BaseOverride);

        if (character is not IPlayerCharacter chara)
        {
            native->SetMode(CharacterModes.AnimLock, 0);
        }

        native->Timeline.BaseOverride = actionTimeline;

        if (interrupt)
            Blend(character, actionTimeline);
    }

    /// <summary>
    /// Trigger a one-shot blend timeline (same as Brio's TimelineSequencer.PlayTimeline).
    /// </summary>
    public unsafe void Blend(ICharacter character, ushort actionTimeline)
    {
        var native = GetNative(character);
        native->Timeline.TimelineSequencer.PlayTimeline(actionTimeline);
    }

    /// <summary>
    /// Stop the animation by setting speed to 0, wait a few ticks to reset local time on controls, then optionally run a post action
    /// and restore previous speed.
    /// </summary>
    public async Task StopAndResetAsync(ICharacter character, Action? postStopAction = null, bool restoreSpeedAfterAction = false)
    {
        float oldSpeed;
        unsafe { oldSpeed = GetNative(character)->Timeline.OverallSpeed; }

        SetOverallSpeed(character, 0);

        await RunOnTick(() =>
        {
            unsafe
            {
                var c = GetNative(character);
                var drawObj = c->GameObject.DrawObject;
                if (drawObj == null)
                    return;
                if (drawObj->Object.GetObjectType() != ObjectType.CharacterBase)
                    return;

                var charaBase = (CharacterBase*)drawObj;
                if (charaBase->Skeleton == null)
                    return;

                var skeleton = charaBase->Skeleton;
                for (int p = 0; p < skeleton->PartialSkeletonCount; ++p)
                {
                    var partial = &skeleton->PartialSkeletons[p];
                    var animatedSkele = partial->GetHavokAnimatedSkeleton(0);
                    if (animatedSkele == null)
                        continue;

                    for (int cidx = 0; cidx < animatedSkele->AnimationControls.Length; ++cidx)
                    {
                        var control = animatedSkele->AnimationControls[cidx].Value;
                        if (control == null)
                            continue;

                        var binding = control->hkaAnimationControl.Binding;
                        if (binding.ptr == null)
                            continue;

                        var anim = binding.ptr->Animation.ptr;
                        if (anim == null)
                            continue;

                        if (control->PlaybackSpeed == 0)
                        {
                            control->hkaAnimationControl.LocalTime = 0;
                        }
                    }
                }
            }
        }, delayTicks: 4);

        postStopAction?.Invoke();

        if (restoreSpeedAfterAction)
        {
            await RunOnTick(() =>
            {
                SetOverallSpeed(character, oldSpeed);
            }, delayTicks: 2);
        }
    }

    /// <summary>
    /// Reset the character's base override and original mode state and do a small blend to clear state.
    /// </summary>
    public unsafe void ResetBase(ICharacter character)
    {
        if (!_states.TryGetValue(character.Address, out var state) || state.OriginalBase is null)
            return;

        var native = GetNative(character);
        var ob = state.OriginalBase.Value;

        native->Timeline.BaseOverride = ob.OriginalTimeline;
        native->Mode = ob.OriginalMode;
        native->ModeParam = ob.OriginalInput;

        state.OriginalBase = null;

        // Small blend to clear state (Brio uses timeline 3 here)
        Blend(character, 3);
    }

    public unsafe void ResetToIdle(ICharacter character)
    {
        var native = GetNative(character);

        native->Timeline.BaseOverride = 0;
        Blend(character, 3);

        if (!_states.TryGetValue(character.Address, out var state) || state.OriginalBase is null)
            return;

        state.OriginalBase = null;
    }

    public void Stop(ICharacter character)
    {
        if (HasBaseOverride(character))
        {
            ResetBase(character);
            ResetOverallSpeed(character);
        }
    }

    public void StopToIdle(ICharacter character)
    {
        ResetToIdle(character);
        ResetOverallSpeed(character);
    }

    public void Reset(ICharacter character)
    {
        ClearLipsOverride(character);
        ResetPerSlotSpeeds(character);
        ResetBase(character);
        ResetOverallSpeed(character);
    }

    public bool HasBaseOverride(ICharacter character)
    {
        return _states.TryGetValue(character.Address, out var st) && st.OriginalBase is not null;
    }

    // --- Per-slot speed controls ---

    public unsafe float GetSlotSpeed(ICharacter character, int slotIndex)
    {
        var st = GetOrCreateState(character);
        if (st.SlotSpeedOverrides.TryGetValue(slotIndex, out var sp))
            return sp;

        var native = GetNative(character);
        return native->Timeline.TimelineSequencer.TimelineSpeeds[slotIndex];
    }

    public unsafe void SetSlotSpeed(ICharacter character, int slotIndex, float speed)
    {
        var st = GetOrCreateState(character);
        st.SlotSpeedOverrides[slotIndex] = speed;
        var native = GetNative(character);
        native->Timeline.TimelineSequencer.SetSlotSpeed((uint)slotIndex, speed);
        st.SlotsDirty = true;
    }

    public void ResetSlotSpeed(ICharacter character, int slotIndex)
    {
        var st = GetOrCreateState(character);
        st.SlotSpeedOverrides.Remove(slotIndex);
        st.SlotsDirty = true;
    }

    public void ResetPerSlotSpeeds(ICharacter character)
    {
        if (_states.TryGetValue(character.Address, out var st))
        {
            st.SlotSpeedOverrides.Clear();
            st.SlotsDirty = false;
        }
    }

    public bool CheckAndResetDirtySlots(ICharacter character)
    {
        if (_states.TryGetValue(character.Address, out var st) && st.SlotsDirty)
        {
            st.SlotsDirty = false;
            return true;
        }
        return false;
    }

    // --- Helpers ---

    private CharacterState GetOrCreateState(ICharacter character)
    {
        if (!_states.TryGetValue(character.Address, out var s))
        {
            s = new CharacterState();
            _states[character.Address] = s;
        }
        return s;
    }

    private static unsafe Character* GetNative(ICharacter character)
        => (Character*)character.Address;

    private Task RunOnTick(Action action, int delayTicks)
    {
        var tcs = new TaskCompletionSource();
        int ticks = 0;
        void Handler(IFramework f)
        {
            ticks++;
            if (ticks >= delayTicks)
            {
                Service.Framework.Update -= Handler;
                try { action(); } catch { /* ignore */ }
                tcs.SetResult();
            }
        }
        Service.Framework.Update += Handler;
        return tcs.Task;
    }

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
