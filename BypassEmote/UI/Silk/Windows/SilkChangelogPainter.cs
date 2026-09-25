using BypassEmote.Localization;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using NoireLib;
using NoireLib.Changelog;
using NoireLib.UI;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace BypassEmote.UI.Silk.Windows;

internal sealed class SilkChangelogPainter
{
    private static string SwitchText => L.ChangelogOnUpdate.Text;
    private static string CloseText => L.Close.Text;
    private static string NewText => L.NewBadge.Text;

    private static readonly Vector4 DotFill = SilkPalette.Hex(0x0b1120);
    private static readonly Vector4 FootBg = SilkPalette.Rgb(0, 0, 0, 0.18f);
    private static readonly Vector4 ItemHover = SilkPalette.Rgb(191, 211, 236, 0.04f);
    private static readonly Vector4 ItemOn = SilkPalette.Rgb(191, 211, 236, 0.08f);
    private static readonly Vector4 NewBg = SilkPalette.Rgb(114, 224, 180, 0.14f);
    private static readonly Vector4 ChipBg = SilkPalette.Rgb(191, 211, 236, 0.10f);
    private static readonly Vector4 LooseGroup = SilkPalette.Hex(0xbfd3ec);
    private static readonly Vector4 DefaultHeader = SilkPalette.Hex(0xe9eef6);

    private static readonly (Vector4 Source, Vector4 Shown)[] ColorMap =
    [
        (SilkPalette.Hex(0xE64040), SilkPalette.Hex(0xff8a8a)),
        (SilkPalette.Hex(0xE81313), SilkPalette.Hex(0xff8a8a)),
        (SilkPalette.Hex(0xFCC203), SilkPalette.Hex(0xffb86b)),
        (SilkPalette.Hex(0x4d8eff), SilkPalette.Hex(0x8fb8ff)),
        (SilkPalette.Hex(0x1BCC18), SilkPalette.Hex(0x7ee0a8)),
        (SilkPalette.Hex(0xFFFFFF), SilkPalette.Hex(0xe9eef6)),
        (SilkPalette.Hex(0xB3B3B3), SilkPalette.Hex(0xe9eef6)),
    ];

    private static readonly SilkIconShape BugIcon = SilkIcons.Define(16f,
        SilkIcons.Rect(5f, 5f, 6f, 8f, 3f, 1.5f),
        SilkIcons.Path("M8 5V3.5M5 8H2.5M11 8h2.5M5.5 11.5l-2 1.5M10.5 11.5l2 1.5M5.5 5.5l-1.5-1.5M10.5 5.5l1.5-1.5", 1.5f));

    private static readonly SilkIconShape WrenchIcon = SilkIcons.Stroked(16f, 1.5f, true,
        "M10.5 2.5a3 3 0 0 0-2.8 4.1L2.8 11.5a1.2 1.2 0 0 0 1.7 1.7l4.9-4.9a3 3 0 0 0 4.1-2.8l-1.8 1.8-1.7-.4-.4-1.7z");

    private static readonly SilkIconShape SparkIcon = SilkIcons.Define(16f,
        SilkIcons.Path("M8 1.5l1.4 4.1L13.5 7l-4.1 1.4L8 12.5 6.6 8.4 2.5 7l4.1-1.4z", 0f, true));

    private enum DocKind
    {
        Ver,
        Title,
        Desc,
        Head,
        Li,
        Button,
        Raw,
    }

    private sealed class DocItem
    {
        public DocKind Kind;
        public float Y;
        public float Height;
        public string Text = string.Empty;
        public string Aside = string.Empty;
        public Vector4 Color;
        public Vector4 TextColor;
        public SilkIconShape? Icon;
        public bool Level2;
        public bool FirstLine;
        public int Id;
        public ChangelogEntry? Entry;
    }

    private sealed class VersionLabel
    {
        public string Short = string.Empty;
        public string Chip = string.Empty;
        public string Date = string.Empty;
        public string Title = string.Empty;
    }

    private readonly SilkScrollPane timelineScroll = new();
    private readonly SilkScrollPane docScroll = new();
    private readonly List<DocItem> doc = [];
    private readonly List<string> wrapBuffer = [];
    private readonly Dictionary<ChangelogVersion, VersionLabel> labels = new(ReferenceEqualityComparer.Instance);

    private ChangelogVersion? docVersion;
    private float docWidth = -1f;
    private float docScale = -1f;
    private float docTextScale = -1f;
    private float docHeight;
    private ChangelogVersion? shownVersion;

    private readonly BeChangelogWindow window;
    private readonly string animKey;

    internal SilkChangelogPainter(BeChangelogWindow window)
    {
        this.window = window;
        animKey = "SilkWindow." + window.Id;
    }

    private static NoireChangelogManager? Manager => NoireLibMain.GetModule<NoireChangelogManager>();

    private const float Radius = SilkChrome.Radius;

    private string AnimationKey => animKey;

    private bool IsOpen
    {
        get => window.IsOpen;
        set => window.IsOpen = value;
    }

    internal void DrawBody(Vector2 min, Vector2 max)
    {
        var manager = Manager;
        var s = SilkUi.Scale;
        var footTop = max.Y - (63f * s);

        if (manager != null)
        {
            var paneBottom = MathF.Max(min.Y, footTop);
            var split = min.X + MathF.Round(210f * s);

            DrawTimeline(manager, min, new Vector2(split, paneBottom));
            DrawDoc(manager.SelectedVersion, new Vector2(split, min.Y), new Vector2(max.X, paneBottom));
        }

        DrawFooter(manager, new Vector2(min.X, footTop), max);
    }

    private void DrawTimeline(NoireChangelogManager manager, Vector2 min, Vector2 max)
    {
        var s = SilkUi.Scale;
        SilkPaint.Fill(new Vector2(max.X - s, min.Y), max, SilkPalette.Line, 0f);

        var versions = manager.Versions;
        var bLine = SilkText.NaturalLine(14f, SilkWeight.ExtraBold);
        var smallLine = SilkText.NaturalLine(11f);
        var spanLine = SilkText.NaturalLine(11.5f);
        var itemHeight = (9f * s) + bLine + s + smallLine + (3f * s) + spanLine + (9f * s);
        var content = (8f * s) + (versions.Count * itemHeight) + (16f * s);
        var paneMax = new Vector2(max.X - s, max.Y);

        timelineScroll.Update("tlbar", min, paneMax, content);

        if (!ReferenceEquals(manager.SelectedVersion, shownVersion))
        {
            var index = 0;

            for (var i = 0; i < versions.Count; i++)
            {
                if (ReferenceEquals(versions[i], manager.SelectedVersion))
                {
                    index = i;
                    break;
                }
            }

            timelineScroll.EnsureVisible((8f * s) + (index * itemHeight), (8f * s) + ((index + 1) * itemHeight));
        }

        SilkPaneKit.PushClip(min, paneMax);

        var itemLeft = min.X + (16f * s);
        var itemRight = paneMax.X - (10f * s);
        var top = min.Y + (8f * s) - timelineScroll.Offset;
        var selected = manager.SelectedVersion;

        for (var i = 0; i < versions.Count; i++)
        {
            var itemMin = new Vector2(itemLeft, top + (i * itemHeight));
            var itemMax = new Vector2(itemRight, itemMin.Y + itemHeight);

            if (itemMax.Y < min.Y || itemMin.Y > max.Y)
                continue;

            var version = versions[i];
            var on = ReferenceEquals(version, selected);

            ImGui.PushID(i);
            var clicked = SilkPaneKit.Hit("v", itemMin, itemMax, out var hovered, out _);
            ImGui.PopID();

            if (clicked && !on)
            {
                manager.SelectVersion(version.Version);
                docScroll.Reset();
            }

            if (on)
                SilkPaint.Fill(itemMin, itemMax, ItemOn, 10f * s);
            else if (hovered)
                SilkPaint.Fill(itemMin, itemMax, ItemHover, 10f * s);

            var lineTop = i == 0 ? itemMin.Y + (16f * s) : itemMin.Y;
            var lineBottom = i == versions.Count - 1 ? itemMin.Y + (16f * s) : itemMax.Y;

            if (lineBottom > lineTop)
                SilkPaint.Fill(new Vector2(itemMin.X + (10f * s), lineTop), new Vector2(itemMin.X + (12f * s), lineBottom), SilkPalette.Line2, 0f);

            var dotCentre = itemMin + new Vector2(11f * s, 18f * s);

            if (on)
            {
                SilkPaint.Glow(dotCentre - new Vector2(5f * s, 5f * s), dotCentre + new Vector2(5f * s, 5f * s), 10f * s, 0f, SilkPalette.Ice, 5f * s);
                SilkPaint.Circle(dotCentre, 7f * s, SilkPalette.IceRing30);
                SilkPaint.Circle(dotCentre, 5f * s, SilkPalette.Ice);
            }
            else
            {
                SilkPaint.Circle(dotCentre, 7f * s, SilkPalette.Line2);
                SilkPaint.Circle(dotCentre, 5f * s, DotFill);
            }

            var label = Label(version);
            var textLeft = itemMin.X + (26f * s);
            var textWidth = itemMax.X - (10f * s) - textLeft;
            var bTop = itemMin.Y + (9f * s);

            var drawn = SilkText.Draw(new Vector2(textLeft, bTop), label.Short, 14f, SilkWeight.ExtraBold, SilkPalette.Ink);

            if (i == 0)
            {
                var badgeHeight = SilkText.NaturalLine(9f, SilkWeight.Bold) + (4f * s);
                var badgeWidth = SilkText.Width(NewText, 9f, SilkWeight.Bold, 0.6f) + (12f * s);
                var badgeMin = new Vector2(textLeft + drawn.X + (6f * s), MathF.Round(bTop + ((bLine - badgeHeight) * 0.5f)));
                var badgeMax = badgeMin + new Vector2(badgeWidth, badgeHeight);

                SilkPaint.Fill(badgeMin, badgeMax, NewBg, badgeHeight * 0.5f);
                SilkText.Draw(badgeMin + new Vector2(6f * s, 2f * s), NewText, 9f, SilkWeight.Bold, SilkPalette.Ok, 0.6f);
            }

            var smallTop = bTop + bLine + s;
            SilkText.Draw(new Vector2(textLeft, smallTop), label.Date, 11f, SilkWeight.Medium, SilkPalette.Ink3, 0f, false, textWidth);

            var spanTop = smallTop + smallLine + (3f * s);
            SilkText.Draw(new Vector2(textLeft, spanTop), label.Title, 11.5f, SilkWeight.Medium, SilkPalette.Ink3, 0f, false, textWidth);
        }

        SilkPaneKit.PopClip();
        timelineScroll.DrawBar(min, paneMax);
    }

    private void DrawDoc(ChangelogVersion? version, Vector2 min, Vector2 max)
    {
        var s = SilkUi.Scale;

        if (!ReferenceEquals(version, shownVersion))
        {
            shownVersion = version;
            docScroll.Reset();
        }

        if (version == null)
            return;

        var inner = MathF.Max(40f * s, (max.X - min.X) - (52f * s));
        EnsureDoc(version, inner);

        docScroll.Update("docbar", min, max, docHeight);

        SilkPaneKit.PushClip(min, max);

        var left = min.X + (26f * s);
        var top = min.Y - docScroll.Offset;

        var effects = default(EffectScope);
        ChangelogEntry? effectsFor = null;

        foreach (var item in doc)
        {
            if (effectsFor != null && !ReferenceEquals(item.Entry, effectsFor))
            {
                effects.Dispose();
                effectsFor = null;
            }

            var y = top + item.Y;

            if (y + item.Height < min.Y || y > max.Y)
                continue;

            if (effectsFor == null && item.Kind == DocKind.Li && item.Entry is { } entry && (entry.Gradient != null || entry.Motion != null))
            {
                effects = NoireEffects.Begin(entry.Gradient, entry.Motion);
                effectsFor = entry;
            }

            DrawDocItem(item, left, y);
        }

        effects.Dispose();

        SilkPaneKit.PopClip();
        docScroll.DrawBar(min, max);
    }

    private static void DrawDocItem(DocItem item, float left, float y)
    {
        var s = SilkUi.Scale;

        switch (item.Kind)
        {
            case DocKind.Ver:
                {
                    var chipMin = new Vector2(left, y);
                    var chipMax = chipMin + new Vector2(SilkText.Width(item.Text, 12f, SilkWeight.ExtraBold, 0f, true) + (16f * s), item.Height);
                    SilkPaint.Fill(chipMin, chipMax, ChipBg, 6f * s);

                    var baseline = y + (2f * s) + SilkText.Ascent(12f, SilkWeight.ExtraBold, true);
                    SilkText.DrawOnBaseline(chipMin.X + (8f * s), baseline, item.Text, 12f, SilkWeight.ExtraBold, SilkPalette.Ice, 0f, true);
                    SilkText.DrawOnBaseline(chipMax.X + (10f * s), baseline, item.Aside, 12f, SilkWeight.Medium, SilkPalette.Ink3);
                    break;
                }
            case DocKind.Title:
                SilkText.Draw(new Vector2(left, SilkText.GlyphTop(y, item.Height, 26f, SilkWeight.ExtraBold, 1.15f)), item.Text, 26f, SilkWeight.ExtraBold, SilkPalette.Ink, -0.6f);
                break;
            case DocKind.Desc:
                SilkText.Draw(new Vector2(left, SilkText.GlyphTop(y, item.Height, 13f, SilkWeight.Medium, 1.6f)), item.Text, 13f, SilkWeight.Medium, SilkPalette.Ink2);
                break;
            case DocKind.Head:
                {
                    var boxMin = new Vector2(left, y);
                    var boxMax = boxMin + new Vector2(26f * s, 26f * s);
                    SilkPaint.Fill(boxMin, boxMax, SilkPalette.Alpha(item.Color, item.Color.W * 0.16f), 8f * s);

                    if (item.Icon != null)
                        SilkIcons.Draw(item.Icon, boxMin + new Vector2(6.5f * s, 6.5f * s), 13f * s, item.Color);

                    SilkText.Draw(new Vector2(left + (35f * s), SilkText.GlyphTop(y, 26f * s, 13.5f, SilkWeight.ExtraBold)), item.Text, 13.5f, SilkWeight.ExtraBold, item.Color);
                    break;
                }
            case DocKind.Li:
                {
                    if (item.FirstLine)
                    {
                        var dot = (item.Level2 ? 4f : 5f) * s;
                        var dotLeft = left + ((item.Level2 ? 30f : 13f) * s);
                        var dotTop = y + (SilkUi.FontPx(13f) * 0.62f);
                        SilkPaint.Fill(new Vector2(dotLeft, dotTop), new Vector2(dotLeft + dot, dotTop + dot), SilkPalette.Fade(item.TextColor, item.Level2 ? 0.5f : 0.8f), dot * 0.5f);
                    }

                    var indent = (item.Level2 ? 52f : 35f) * s;
                    SilkText.Draw(new Vector2(left + indent, SilkText.GlyphTop(y, item.Height, 13f, SilkWeight.Medium, 1.55f)), item.Text, 13f, SilkWeight.Medium, item.TextColor);
                    break;
                }
            case DocKind.Button:
                {
                    var entry = item.Entry!;
                    var indent = (item.Level2 ? 52f : 35f) * s;
                    var text = item.Text;
                    var width = SilkPaneKit.ButtonWidth(text, null, 12f, 12f, 0f, 0f);
                    var bMin = new Vector2(left + indent, y);
                    var bMax = bMin + new Vector2(width, item.Height);

                    ImGui.PushID(item.Id);
                    var clicked = SilkPaneKit.Button("cb", bMin, bMax, text, null, true, 12f, 12f, 0f, 0f, 8f, entry.ButtonTextColor);
                    var right = ImGui.IsItemClicked(ImGuiMouseButton.Right);
                    var middle = ImGui.IsItemClicked(ImGuiMouseButton.Middle);
                    ImGui.PopID();

                    if (clicked)
                        entry.ButtonAction?.Invoke(ImGuiMouseButton.Left);
                    else if (right)
                        entry.ButtonAction?.Invoke(ImGuiMouseButton.Right);
                    else if (middle)
                        entry.ButtonAction?.Invoke(ImGuiMouseButton.Middle);
                    break;
                }
            case DocKind.Raw:
                ImGui.SetCursorScreenPos(new Vector2(left + ((item.Level2 ? 52f : 35f) * s), y));
                item.Entry?.RawAction?.Invoke();
                break;
        }
    }

    private void EnsureDoc(ChangelogVersion version, float inner)
    {
        var s = SilkUi.Scale;
        var textScale = SilkUi.TextScale;
        var width = MathF.Round(inner);

        if (ReferenceEquals(docVersion, version) && docWidth == width && docScale == s && docTextScale == textScale)
            return;

        docVersion = version;
        docWidth = width;
        docScale = s;
        docTextScale = textScale;
        doc.Clear();

        var label = Label(version);
        var y = 18f * s;

        var chipHeight = SilkText.NaturalLine(12f, SilkWeight.ExtraBold, true) + (4f * s);
        doc.Add(new DocItem { Kind = DocKind.Ver, Y = y, Height = chipHeight, Text = label.Chip, Aside = label.Date });
        y += chipHeight;

        y += 10f * s;
        var titleLine = SilkText.LineBox(26f, 1.15f);
        SilkText.Wrap(label.Title, 26f, SilkWeight.ExtraBold, inner, wrapBuffer, false, -0.6f);

        foreach (var line in wrapBuffer)
        {
            doc.Add(new DocItem { Kind = DocKind.Title, Y = y, Height = titleLine, Text = line });
            y += titleLine;
        }

        if (!string.IsNullOrWhiteSpace(version.Description))
        {
            y += 8f * s;
            var descLine = SilkText.LineBox(13f, 1.6f);
            SilkText.Wrap(Clean(version.Description), 13f, SilkWeight.Medium, MathF.Min(inner, 560f * s), wrapBuffer);

            foreach (var line in wrapBuffer)
            {
                doc.Add(new DocItem { Kind = DocKind.Desc, Y = y, Height = descLine, Text = line });
                y += descLine;
            }
        }

        var liLine = SilkText.LineBox(13f, 1.55f);
        var open = false;
        var firstInGroup = true;
        var groupColor = LooseGroup;
        var id = 0;

        foreach (var entry in version.Entries)
        {
            if (entry.IsSeparator)
            {
                open = false;
                continue;
            }

            if (entry.IsHeader)
            {
                groupColor = Shown(entry.TextColor) ?? DefaultHeader;
                y += 22f * s;
                doc.Add(new DocItem { Kind = DocKind.Head, Y = y, Height = 26f * s, Text = Clean(entry.Text ?? string.Empty), Color = groupColor, Icon = IconFor(entry.Icon) });
                y += 26f * s;
                open = true;
                firstInGroup = true;
                continue;
            }

            if (!open)
            {
                groupColor = LooseGroup;
                y += 22f * s;
                open = true;
                firstInGroup = true;
            }

            y += (firstInGroup ? 10f : 7f) * s;
            firstInGroup = false;

            var level2 = entry.IndentLevel > 1;

            if (entry.IsRaw)
            {
                doc.Add(new DocItem { Kind = DocKind.Raw, Y = y, Height = liLine, Entry = entry, Level2 = level2 });
                y += liLine;
                continue;
            }

            var textWidth = inner - ((level2 ? 52f : 35f) * s);
            var liColor = Shown(entry.TextColor) ?? SilkPalette.Ink;
            SilkText.Wrap(Clean(entry.Text ?? string.Empty), 13f, SilkWeight.Medium, textWidth, wrapBuffer);
            var withEffects = entry.Gradient != null || entry.Motion != null;

            for (var i = 0; i < wrapBuffer.Count; i++)
            {
                doc.Add(new DocItem { Kind = DocKind.Li, Y = y, Height = liLine, Text = wrapBuffer[i], Color = groupColor, TextColor = liColor, Level2 = level2, FirstLine = i == 0, Entry = withEffects ? entry : null });
                y += liLine;
            }

            if (!string.IsNullOrWhiteSpace(entry.ButtonText) && entry.ButtonAction != null)
            {
                y += 6f * s;
                doc.Add(new DocItem { Kind = DocKind.Button, Y = y, Height = 26f * s, Text = entry.ButtonText, Entry = entry, Level2 = level2, Id = ++id });
                y += 26f * s;
            }
        }

        docHeight = y + (28f * s);
    }

    private void DrawFooter(NoireChangelogManager? manager, Vector2 min, Vector2 max)
    {
        var s = SilkUi.Scale;

        SilkPaint.Fill(min, max, SilkPalette.Fade(FootBg, SilkUi.Opacity), Radius * s, RectCorners.Bottom);
        SilkPaint.Fill(min, new Vector2(max.X, min.Y + s), SilkPalette.Line, 0f);

        var rowTop = min.Y + (13f * s);
        var rowHeight = 36f * s;
        var switchMin = new Vector2(min.X + (16f * s), rowTop + (7f * s));
        var on = Configuration.ShowChangelogOnUpdate;

        if (SilkPaneKit.Switch(AnimationKey, "sw", switchMin, on))
        {
            on = !on;
            Configuration.ShowChangelogOnUpdate = on;
            manager?.SetAutomaticallyShowChangelog(on);
        }

        var textLeft = switchMin.X + (50f * s);
        SilkText.Draw(new Vector2(textLeft, SilkText.GlyphTop(rowTop, rowHeight, 12f)), SwitchText, 12f, SilkWeight.Medium, SilkPalette.Ink3);

        var closeWidth = SilkPaneKit.ButtonWidth(CloseText, null, 16f, 13f, 0f, 0f);
        var closeMax = new Vector2(max.X - (16f * s), rowTop + rowHeight);
        var closeMin = new Vector2(closeMax.X - closeWidth, rowTop);

        if (SilkPaneKit.Button("cl-close", closeMin, closeMax, CloseText, null))
        {
            IsOpen = false;

            if (manager != null && manager.CustomWindow == window)
                manager.CloseWindow();
        }
    }

    private VersionLabel Label(ChangelogVersion version)
    {
        if (labels.TryGetValue(version, out var label))
            return label;

        var full = version.Version.ToString();
        label = new VersionLabel
        {
            Chip = "v" + full,
            Short = full.EndsWith(".0", StringComparison.Ordinal) ? full[..^2] : full,
            Date = version.Date ?? string.Empty,
            Title = Clean(version.Title ?? string.Empty),
        };

        labels[version] = label;
        return label;
    }

    private static string Clean(string text)
        => text.Replace("\r", string.Empty).Replace("*", string.Empty).TrimEnd('\n', ' ');

    private static Vector4? Shown(Vector4? color)
    {
        if (color is not { } value)
            return null;

        foreach (var (source, shown) in ColorMap)
        {
            if (MathF.Abs(source.X - value.X) < 0.02f && MathF.Abs(source.Y - value.Y) < 0.02f && MathF.Abs(source.Z - value.Z) < 0.02f)
                return shown;
        }

        return value;
    }

    private static SilkIconShape IconFor(FontAwesomeIcon? icon) => icon switch
    {
        FontAwesomeIcon.Bug => BugIcon,
        FontAwesomeIcon.Wrench => WrenchIcon,
        FontAwesomeIcon.Plus => SilkIcons.Get(SilkIcon.Plus),
        _ => SparkIcon,
    };
}
