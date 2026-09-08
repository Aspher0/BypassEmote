using BypassEmote.Enums;
using BypassEmote.Models;
using System.Collections.Generic;

namespace BypassEmote.EmoteSwap;

public static class SwapAdvice
{
    public enum Severity
    {
        Note,
        Warning,
        Error,
    }

    public readonly record struct Line(Severity Severity, string Text);

    public sealed record Facts(bool Unlocked, bool Blocked, string? ChangedByMod);

    public static Severity WorstOf(IReadOnlyList<Line> lines)
    {
        var worst = Severity.Note;

        foreach (var line in lines)
        {
            if (line.Severity > worst)
                worst = line.Severity;
        }

        return worst;
    }

    public static IReadOnlyList<Line> ForOverride(EmoteAttributes source, string sourceName, EmoteAttributes target,
        string targetName, Facts facts)
    {
        if (source.RowId == target.RowId)
            return [new Line(Severity.Error, $"{targetName} is the emote being bypassed.")];

        var lines = new List<Line>();

        if (!target.EligibleTarget)
        {
            lines.Add(new Line(Severity.Error, $"{targetName} can never be a swap target: it is a pose, a facial "
                + "expression, a per-job emote, or it draws your weapon. It is always skipped."));
        }

        if (!facts.Unlocked)
        {
            lines.Add(new Line(Severity.Note, $"You have not unlocked {targetName}. It is skipped."));
        }

        if (facts.Blocked)
        {
            lines.Add(new Line(Severity.Warning, $"{targetName} is on your blocked targets list. This override uses it anyway."));
        }

        if (facts.ChangedByMod is { Length: > 0 } modName)
        {
            lines.Add(new Line(Severity.Warning, $"Your mod \"{modName}\" already changes {targetName}. What your "
                + "\"Emotes your mods change\" setting is set to still applies here."));
        }

        lines.AddRange(Behaviour(source, sourceName, target, targetName));

        return lines;
    }

    public static IReadOnlyList<Line> Behaviour(EmoteAttributes source, string sourceName, EmoteAttributes target,
        string targetName)
    {
        var lines = new List<Line>();

        if ((source.Postures & target.Postures) == PostureFlags.None)
        {
            lines.Add(new Line(Severity.Error, $"{targetName} and {sourceName} share no posture. The swap will not happen."));
        }
        else if ((source.Postures & ~target.Postures) is var missing && missing != PostureFlags.None)
        {
            lines.Add(new Line(Severity.Warning, $"{targetName} has no {PostureText(missing)} animation. The swap "
                + $"will not happen when you play {sourceName} in that posture."));
        }

        AddLoopLine(lines, source, sourceName, target, targetName);
        AddTurnLine(lines, source, sourceName, target, targetName);
        AddSoundLine(lines, source, sourceName, target, targetName);
        AddIntroLine(lines, source, sourceName, target, targetName);

        if (target.CancelsOnRotate)
        {
            lines.Add(new Line(Severity.Warning, $"{targetName} will stop when you turn your character."));
        }

        return lines;
    }

    private static void AddLoopLine(List<Line> lines, EmoteAttributes source, string sourceName,
        EmoteAttributes target, string targetName)
    {
        if (source.LoopKind == target.LoopKind)
            return;

        lines.Add(source.LoopKind == EmotePlayType.Looped
            ? new Line(Severity.Warning, $"{targetName} plays once while {sourceName} loops. The animation will stop "
                + "instead of looping.")
            : new Line(Severity.Warning, $"{targetName} loops while {sourceName} plays once. Weird behavior might happen."));
    }

    private static void AddTurnLine(List<Line> lines, EmoteAttributes source, string sourceName,
        EmoteAttributes target, string targetName)
    {
        if (source.Turn == target.Turn)
            return;

        lines.Add(new Line(Severity.Warning, $"{targetName} does not behave like {sourceName} when you target "
            + $"someone: {targetName} {TurnText(target.Turn)} while {sourceName} {TurnText(source.Turn)}."));
    }

    private static void AddSoundLine(List<Line> lines, EmoteAttributes source, string sourceName,
        EmoteAttributes target, string targetName)
    {
        if (target.Sound == SoundClass.Voiceline && source.Sound != SoundClass.Voiceline)
        {
            lines.Add(new Line(Severity.Warning, $"{targetName} emits a voice line sound. Everyone around you will hear it."));
            return;
        }

        if (target.Sound == SoundClass.Sfx && source.Sound == SoundClass.Silent)
        {
            lines.Add(new Line(Severity.Warning, $"{targetName} emits a sound that {sourceName} does not. Everyone around you will hear it."));
        }
    }

    private static void AddIntroLine(List<Line> lines, EmoteAttributes source, string sourceName,
        EmoteAttributes target, string targetName)
    {
        var sourceHasIntro = source.Intro == IntroKind.Pap;

        if (sourceHasIntro && target.Intro == IntroKind.None || target.Intro == IntroKind.TmbOnly)
        {
            lines.Add(new Line(Severity.Warning, $"{targetName} has no intro meanwhile {sourceName} has one, meaning the intro will not play."));
            return;
        }

        if (!sourceHasIntro && target.Intro == IntroKind.Pap)
        {
            lines.Add(new Line(Severity.Warning, $"{targetName} has an intro and {sourceName} does not."));
        }
    }

    private static string TurnText(TurnClass turn) => turn switch
    {
        TurnClass.None => "does not turn at all",
        TurnClass.Eyes => "only follows with the eyes",
        TurnClass.Head => "turns the head",
        TurnClass.Body => "turns the whole body",
        _ => "turns in a way the plugin could not read",
    };

    private static string PostureText(PostureFlags postures)
    {
        var names = new List<string>(4);

        if (postures.HasFlag(PostureFlags.Standing))
            names.Add("standing");

        if (postures.HasFlag(PostureFlags.ChairSit))
            names.Add("chair sitting");

        if (postures.HasFlag(PostureFlags.GroundSit))
            names.Add("ground sitting");

        if (postures.HasFlag(PostureFlags.Mounted))
            names.Add("mounted");

        return names.Count == 0 ? "matching" : string.Join(" or ", names);
    }
}
