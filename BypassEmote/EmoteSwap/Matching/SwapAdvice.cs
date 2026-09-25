using BypassEmote.Enums;
using BypassEmote.Localization;
using BypassEmote.Models;
using System.Collections.Generic;
using System.Linq;

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
            return [new Line(Severity.Error, L.AdviceBeingBypassed.With("target", targetName))];

        var lines = new List<Line>();

        if (!target.EligibleTarget)
        {
            lines.Add(new Line(Severity.Error, L.AdviceNeverTarget.With("target", targetName)));
        }

        if (!facts.Unlocked)
        {
            lines.Add(new Line(Severity.Note, L.AdviceNotUnlocked.With("target", targetName)));
        }

        if (facts.Blocked)
        {
            lines.Add(new Line(Severity.Warning, L.AdviceBlocked.With("target", targetName)));
        }

        if (facts.ChangedByMod is { Length: > 0 } modName)
        {
            lines.Add(new Line(Severity.Warning, L.AdviceModChanges.With("mod", modName, "target", targetName)));
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
            lines.Add(new Line(Severity.Error, L.AdviceNoPosture.With("target", targetName, "source", sourceName)));
        }
        else if ((source.Postures & ~target.Postures) is var missing && missing != PostureFlags.None)
        {
            lines.Add(new Line(Severity.Warning, L.Fill(L.AdviceMissingPosture, "target", targetName, "source", sourceName,
                "posture", PostureText(missing))));
        }

        AddLoopLine(lines, source, sourceName, target, targetName);
        AddTurnLine(lines, source, sourceName, target, targetName);
        AddSoundLine(lines, source, sourceName, target, targetName);
        AddIntroLine(lines, source, sourceName, target, targetName);
        AddAdjustLine(lines, source, sourceName, target, targetName);

        if (target.CancelsOnRotate)
        {
            lines.Add(new Line(Severity.Warning, L.AdviceStopsOnTurn.With("target", targetName)));
        }

        return lines;
    }

    private static void AddAdjustLine(List<Line> lines, EmoteAttributes source, string sourceName,
        EmoteAttributes target, string targetName)
    {
        if (target.AdjustRelativePapPath == null)
            return;

        if (source.AdjustRelativePapPath != null
            || source.Variants.Any(variant => variant.Posture == PostureFlags.Mounted))
        {
            return;
        }

        lines.Add(new Line(Severity.Warning, L.AdviceAdjust.With("target", targetName, "source", sourceName)));
    }

    private static void AddLoopLine(List<Line> lines, EmoteAttributes source, string sourceName,
        EmoteAttributes target, string targetName)
    {
        if (source.LoopKind == target.LoopKind)
            return;

        lines.Add(source.LoopKind == EmotePlayType.Looped
            ? new Line(Severity.Warning, L.AdvicePlaysOnce.With("target", targetName, "source", sourceName))
            : new Line(Severity.Warning, L.AdviceLoops.With("target", targetName, "source", sourceName)));
    }

    private static void AddTurnLine(List<Line> lines, EmoteAttributes source, string sourceName,
        EmoteAttributes target, string targetName)
    {
        if (source.Turn == target.Turn)
            return;

        lines.Add(new Line(Severity.Warning, L.Fill(L.AdviceTurn, "target", targetName, "source", sourceName,
            "target_turn", TurnText(target.Turn), "source_turn", TurnText(source.Turn))));
    }

    private static void AddSoundLine(List<Line> lines, EmoteAttributes source, string sourceName,
        EmoteAttributes target, string targetName)
    {
        if (target.Sound == SoundClass.Voiceline && source.Sound != SoundClass.Voiceline)
        {
            lines.Add(new Line(Severity.Warning, L.AdviceVoiceLine.With("target", targetName)));
            return;
        }

        if (target.Sound == SoundClass.Sfx && source.Sound == SoundClass.Silent)
        {
            lines.Add(new Line(Severity.Warning, L.AdviceSound.With("target", targetName, "source", sourceName)));
        }
    }

    private static void AddIntroLine(List<Line> lines, EmoteAttributes source, string sourceName,
        EmoteAttributes target, string targetName)
    {
        var sourceHasIntro = source.Intro == IntroKind.Pap;

        if (sourceHasIntro && target.Intro == IntroKind.None || target.Intro == IntroKind.TmbOnly)
        {
            lines.Add(new Line(Severity.Warning, L.AdviceIntroDropped.With("target", targetName, "source", sourceName)));
            return;
        }

        if (!sourceHasIntro && target.Intro == IntroKind.Pap)
        {
            lines.Add(new Line(Severity.Warning, L.AdviceIntroAdded.With("target", targetName, "source", sourceName)));
        }
    }

    private static string TurnText(TurnClass turn) => turn switch
    {
        TurnClass.None => L.TurnNone.Text,
        TurnClass.Eyes => L.TurnEyes.Text,
        TurnClass.Head => L.TurnHead.Text,
        TurnClass.Body => L.TurnBody.Text,
        _ => L.TurnUnknown.Text,
    };

    private static string PostureText(PostureFlags postures)
    {
        var names = new List<string>(4);

        if (postures.HasFlag(PostureFlags.Standing))
            names.Add(L.PostureStanding.Text);

        if (postures.HasFlag(PostureFlags.ChairSit))
            names.Add(L.PostureChairSitting.Text);

        if (postures.HasFlag(PostureFlags.GroundSit))
            names.Add(L.PostureGroundSitting.Text);

        if (postures.HasFlag(PostureFlags.Mounted))
            names.Add(L.PostureMounted.Text);

        if (names.Count == 0)
            return L.PostureMatching.Text;

        var text = names[0];

        for (var index = 1; index < names.Count; index++)
            text = L.PostureOr.With("first", text, "second", names[index]);

        return text;
    }
}
