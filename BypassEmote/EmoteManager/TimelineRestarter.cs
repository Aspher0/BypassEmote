using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using FFXIVClientStructs.FFXIV.Client.System.Scheduler.Base;
using NoireLib.Animations.Timelines;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using NativeCharacter = FFXIVClientStructs.FFXIV.Client.Game.Character.Character;
using ObjectType = FFXIVClientStructs.FFXIV.Client.Graphics.Scene.ObjectType;

namespace BypassEmote;

internal static unsafe class TimelineRestarter
{
    private const int TimestampOffset = 0x34;

    private static readonly uint[] EmoteSlots = [0, 1];

    internal readonly record struct SlotRestart(uint Slot, ushort TimelineId, string? Key, float Timestamp, float LoopStart,
        float End, int PlayState, bool Restarted, bool Replayed, float RestartedAt, int SoundsStopped, int VfxRestarted);

    private sealed class ClipReset
    {
        public readonly HashSet<nint> Sounds = [];
        public int Vfx;
    }

    internal static IReadOnlyList<SlotRestart> Restart(ICharacter character, Func<uint, ushort, string?, bool> wanted,
        int replayPriority = ActionTimelineDriver.DefaultPriority)
    {
        var restarts = new List<SlotRestart>(EmoteSlots.Length);

        if (character.Address == 0)
            return restarts;

        var sequencer = &((NativeCharacter*)character.Address)->Timeline.TimelineSequencer;
        var replays = new List<ushort>(EmoteSlots.Length);
        float? animationStart = null;

        foreach (var slot in EmoteSlots)
        {
            var timelineId = sequencer->TimelineIds[(int)slot];

            if (timelineId == 0)
                continue;

            var timeline = sequencer->GetSchedulerTimeline(slot);

            if (timeline == null || !wanted(slot, timelineId, KeyOf((byte*)timeline)))
                continue;

            var bytes = (byte*)timeline;
            var playState = *(int*)(bytes + 0x74);
            var running = playState < 3 && (bytes[0x7C] & 0x40) == 0;
            var timestamp = *(float*)(bytes + TimestampOffset);
            var loopStart = *(float*)(bytes + 0x3C);
            var fromTop = *(int*)(bytes + 0x58) == 1 || timestamp < loopStart;
            var restartedAt = fromTop ? 0f : Math.Max(0f, loopStart);
            var reset = new ClipReset();

            if (running)
            {
                ResetClips(bytes, !fromTop, reset, 0);

                if (fromTop)
                    replays.Add(timelineId);
                else
                    Rewind(timeline, restartedAt);

                animationStart ??= restartedAt / 30f;
            }

            restarts.Add(new SlotRestart(slot, timelineId, KeyOf(bytes), timestamp, loopStart,
                *(float*)(bytes + 0x40), playState, running, running && fromTop, restartedAt, reset.Sounds.Count,
                reset.Vfx));
        }

        if (animationStart is { } start)
            RewindSkeleton(character, start);

        foreach (var timelineId in replays)
            Service.ActionTimelinePlayer.Blend(character, timelineId, replayPriority, collapseFade: true);

        return restarts;
    }

    internal static int RewindSkeleton(ICharacter character, float localTime = 0f)
    {
        if (character.Address == 0)
            return 0;

        var native = (GameObject*)character.Address;

        if (native->DrawObject == null || native->DrawObject->GetObjectType() != ObjectType.CharacterBase)
            return 0;

        var skeleton = ((CharacterBase*)native->DrawObject)->Skeleton;

        if (skeleton == null || skeleton->PartialSkeletons == null)
            return 0;

        var rewound = 0;

        for (var partial = 0; partial < skeleton->PartialSkeletonCount; partial++)
        {
            var animated = skeleton->PartialSkeletons[partial].GetHavokAnimatedSkeleton(0);

            if (animated == null)
                continue;

            for (var index = 0; index < animated->AnimationControls.Length; index++)
            {
                var control = animated->AnimationControls[index].Value;

                if (control == null)
                    continue;

                control->hkaAnimationControl.LocalTime = localTime;
                rewound++;
            }
        }

        return rewound;
    }

    private static void Rewind(SchedulerTimeline* timeline, float start)
    {
        var bytes = (byte*)timeline;

        *(float*)(bytes + TimestampOffset) = start;
        *(float*)(bytes + 0x38) = start;

        var data = stackalloc byte[16];
        data[0] = 1;

        timeline->ProcessAll(10, data);
    }

    private static void ResetClips(byte* timeline, bool loop, ClipReset reset, int depth)
    {
        for (var track = *(byte**)(timeline + 0x18); track != null; track = *(byte**)(track + 0x20))
        {
            var groups = *(byte***)(track + 0x28);
            var groupCount = *(ushort*)(track + 0x32);

            for (var groupIndex = 0; groups != null && groupIndex < groupCount; groupIndex++)
            {
                var group = groups[groupIndex];

                if (group == null)
                    continue;

                var clips = *(byte***)(group + 0x18);
                var clipCount = *(ushort*)(group + 0x22);

                for (var clipIndex = 0; clips != null && clipIndex < clipCount; clipIndex++)
                    ResetClip(clips[clipIndex], loop, reset, depth);
            }
        }
    }

    private static void ResetClip(byte* clip, bool loop, ClipReset reset, int depth)
    {
        if (clip == null)
            return;

        var kind = *(int*)(clip + 0x84);
        var skipsRearm = (clip[0x88] & 0x01) != 0;

        if (kind is 0 or 7)
        {
            var child = *(byte**)(clip + 0x138);

            if (child != null && depth < 4)
                ResetClips(child, loop && skipsRearm, reset, depth + 1);

            return;
        }

        if (loop && skipsRearm)
            return;

        if (kind is 52 or 41)
            StopClipSounds(clip, reset.Sounds);
        else if (kind is 9 or 39)
            RestartClipVfx(clip, reset);
    }

    private static void StopClipSounds(byte* clip, HashSet<nint> stopped)
    {
        var sounds = (nint*)(clip + 0xA0);

        for (var soundIndex = 0; soundIndex < 4; soundIndex++)
        {
            var sound = sounds[soundIndex];

            if (sound == 0)
                continue;

            if (stopped.Add(sound))
            {
                ((delegate* unmanaged<nint, int, void>)(*(nint**)sound)[36])(sound,
                    0);
            }

            sounds[soundIndex] = 0;
        }
    }

    private static void RestartClipVfx(byte* clip, ClipReset reset)
    {
        var vfx = *(byte**)(clip + 0x98);

        if (vfx == null || vfx[0xD0] == 0)
            return;

        ((delegate* unmanaged<byte*, void>)(*(nint**)clip)[24])(clip);
        reset.Vfx++;
    }

    internal static bool IsDrawnAndVisible(ICharacter character)
    {
        if (character.Address == 0)
            return false;

        var native = (GameObject*)character.Address;

        return native->DrawObject != null && ((ulong)native->RenderFlags & ~0x8UL) == 0;
    }

    internal static string DescribeSlots(ICharacter character)
    {
        if (character.Address == 0)
            return "no character";

        var sequencer = &((NativeCharacter*)character.Address)->Timeline.TimelineSequencer;
        var slots = new List<string>(EmoteSlots.Length);

        foreach (var slot in EmoteSlots)
        {
            var timelineId = sequencer->TimelineIds[(int)slot];
            var timeline = sequencer->GetSchedulerTimeline(slot);

            slots.Add(timeline == null
                ? $"slot {slot} timeline {timelineId}, nothing loaded"
                : $"slot {slot} timeline {timelineId} '{KeyOf((byte*)timeline) ?? "?"}'");
        }

        return string.Join("; ", slots);
    }

    private static string? KeyOf(byte* timeline)
    {
        var key = *(nint*)(timeline + 0xA8);

        return key == 0 ? null : Marshal.PtrToStringUTF8(key);
    }
}
