using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.System.Scheduler.Base;
using NoireLib.Animations.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using NativeCharacter = FFXIVClientStructs.FFXIV.Client.Game.Character.Character;

namespace BypassEmote;

internal static unsafe class TimelineRestarter
{
    private const int FirstTrackOffset = 0x18;
    private const int PreviousTimestampOffset = 0x38;
    private const int TimestampOffset = 0x34;
    private const int LoopStartOffset = 0x3C;
    private const int EndOffset = 0x40;
    private const int PlayModeOffset = 0x58;
    private const int PlayStateOffset = 0x74;
    private const int StateFlagsOffset = 0x7C;
    private const int ActionTimelineKeyOffset = 0xA8;

    private const int NextTrackOffset = 0x20;
    private const int TrackGroupsOffset = 0x28;
    private const int TrackGroupCountOffset = 0x32;
    private const int GroupClipsOffset = 0x18;
    private const int GroupClipCountOffset = 0x22;

    private const int ClipKindOffset = 0x84;
    private const int ClipSoundsOffset = 0xA0;
    private const int ClipSoundCount = 4;
    private const int CharacterSoundClip = 41;
    private const int PathSoundClip = 52;

    private const int ClipChildTimelineOffset = 0x138;
    private const int PathTimelineClip = 0;
    private const int PapTimelineClip = 7;
    private const int MaxTimelineDepth = 4;

    private const int StopSoundVirtualIndex = 36;
    private const int SoundFadeOutMilliseconds = 0;

    private const int LoopEvent = 10;
    private const int PlaysOnce = 1;
    private const byte FinishedFlag = 0x40;
    private const int FirstFinishedState = 3;

    private static readonly uint[] EmoteSlots = [0, 1];

    internal readonly record struct SlotRestart(uint Slot, ushort TimelineId, string? Key, float Timestamp, float End,
        int PlayState, bool Restarted, int SoundsStopped);

    internal static IReadOnlyList<SlotRestart> Restart(ICharacter character, Func<uint, ushort, string?, bool> wanted)
    {
        var restarts = new List<SlotRestart>(EmoteSlots.Length);

        if (character.Address == 0)
            return restarts;

        var sequencer = &((NativeCharacter*)character.Address)->Timeline.TimelineSequencer;

        foreach (var slot in EmoteSlots)
        {
            var timelineId = sequencer->TimelineIds[(int)slot];

            if (timelineId == 0)
                continue;

            var timeline = sequencer->GetSchedulerTimeline(slot);

            if (timeline == null || !wanted(slot, timelineId, KeyOf((byte*)timeline)))
                continue;

            var bytes = (byte*)timeline;
            var playState = *(int*)(bytes + PlayStateOffset);
            var running = playState < FirstFinishedState && (bytes[StateFlagsOffset] & FinishedFlag) == 0;
            var timestamp = *(float*)(bytes + TimestampOffset);
            var stopped = 0;

            if (running)
            {
                stopped = StopSounds(bytes);
                Rewind(timeline);
            }

            restarts.Add(new SlotRestart(slot, timelineId, KeyOf(bytes), timestamp, *(float*)(bytes + EndOffset),
                playState, running, stopped));
        }

        if (restarts.Any(restart => restart.Restarted))
            SkeletonAnimationHelper.ResetAnimationTime(character);

        return restarts;
    }

    private static void Rewind(SchedulerTimeline* timeline)
    {
        var bytes = (byte*)timeline;
        var playsOnce = *(int*)(bytes + PlayModeOffset) == PlaysOnce;
        var start = playsOnce ? 0f : Math.Max(0f, *(float*)(bytes + LoopStartOffset));

        *(float*)(bytes + TimestampOffset) = start;
        *(float*)(bytes + PreviousTimestampOffset) = start;

        var data = stackalloc byte[16];
        data[0] = playsOnce ? (byte)0 : (byte)1;

        timeline->ProcessAll(LoopEvent, data);
    }

    private static int StopSounds(byte* timeline)
    {
        var stopped = new HashSet<nint>();
        StopSounds(timeline, stopped, 0);

        return stopped.Count;
    }

    private static void StopSounds(byte* timeline, HashSet<nint> stopped, int depth)
    {
        for (var track = *(byte**)(timeline + FirstTrackOffset); track != null; track = *(byte**)(track + NextTrackOffset))
        {
            var groups = *(byte***)(track + TrackGroupsOffset);
            var groupCount = *(ushort*)(track + TrackGroupCountOffset);

            for (var groupIndex = 0; groups != null && groupIndex < groupCount; groupIndex++)
            {
                var group = groups[groupIndex];

                if (group == null)
                    continue;

                var clips = *(byte***)(group + GroupClipsOffset);
                var clipCount = *(ushort*)(group + GroupClipCountOffset);

                for (var clipIndex = 0; clips != null && clipIndex < clipCount; clipIndex++)
                    StopClipSounds(clips[clipIndex], stopped, depth);
            }
        }
    }

    private static void StopClipSounds(byte* clip, HashSet<nint> stopped, int depth)
    {
        if (clip == null)
            return;

        var kind = *(int*)(clip + ClipKindOffset);

        if (kind is PathTimelineClip or PapTimelineClip)
        {
            var child = *(byte**)(clip + ClipChildTimelineOffset);

            if (child != null && depth < MaxTimelineDepth)
                StopSounds(child, stopped, depth + 1);

            return;
        }

        if (kind is not (PathSoundClip or CharacterSoundClip))
            return;

        var sounds = (nint*)(clip + ClipSoundsOffset);

        for (var soundIndex = 0; soundIndex < ClipSoundCount; soundIndex++)
        {
            var sound = sounds[soundIndex];

            if (sound == 0)
                continue;

            if (stopped.Add(sound))
            {
                ((delegate* unmanaged<nint, int, void>)(*(nint**)sound)[StopSoundVirtualIndex])(sound,
                    SoundFadeOutMilliseconds);
            }

            sounds[soundIndex] = 0;
        }
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

    internal static string Describe(IReadOnlyList<SlotRestart> restarts)
        => restarts.Count == 0
            ? "no matching timeline in the base or upper body slot"
            : string.Join("; ", restarts.Select(restart =>
                $"slot {restart.Slot} timeline {restart.TimelineId} '{restart.Key ?? "?"}' at "
                + $"{restart.Timestamp:0.00}/{restart.End:0.00}s, state {restart.PlayState}, "
                + (restart.Restarted
                    ? $"restarted, {restart.SoundsStopped} sound(s) stopped"
                    : "left alone, already finished")));

    private static string? KeyOf(byte* timeline)
    {
        var key = *(nint*)(timeline + ActionTimelineKeyOffset);

        return key == 0 ? null : Marshal.PtrToStringUTF8(key);
    }
}
