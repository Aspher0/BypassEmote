using BypassEmote.Helpers;
using Dalamud.Game.ClientState.Objects.Types;
using Lumina.Excel.Sheets;
using NoireLib.Animations.Timelines;
using System;

namespace BypassEmote;

/// <summary> Plays emotes through action timelines. For Direct Play only. </summary>
public sealed class ActionTimelinePlayer : IDisposable
{
    private readonly ActionTimelineDriver _driver = new();

    public void Dispose() => _driver.Dispose();

    public void Play(ICharacter character, Emote emote, ushort actionTimeline, bool interrupt = true)
    {
        var alreadyTracked = CommonHelper.TryGetTrackedCharacterFromAddress(character.Address) != null;

        _driver.Play(character, actionTimeline, captureBase: !alreadyTracked, interrupt: interrupt,
            targetId: CommonHelper.GetPlayerTarget(character));
    }

    public void Blend(ICharacter character, ushort actionTimeline, int prio = ActionTimelineDriver.DefaultPriority,
        Models.CharacterState? characterState = null, bool collapseFade = false)
        => _driver.Blend(character, actionTimeline, prio,
            CommonHelper.TargetIdFor(character, characterState), collapseFade);

    public void ResetBase(ICharacter character) => _driver.ResetBase(character);

    public void Stop(ICharacter character, bool force)
    {
        var trackedCharacter = CommonHelper.TryGetTrackedCharacterFromAddress(character.Address);

        if (HasBaseOverride(character) && (force || trackedCharacter is { ScheduledForRemoval: true }))
            _driver.ResetBase(character);
    }

    public bool HasBaseOverride(ICharacter character) => _driver.HasBaseOverride(character);
}
