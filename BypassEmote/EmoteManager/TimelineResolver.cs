using Lumina.Excel.Sheets;

namespace BypassEmote;

public static class TimelineResolver
{
    public static ushort ResolveIntroTimeline(Emote emote)
    {
        try
        {
            var timelines = emote.ActionTimeline;
            if (timelines.Count > 1)
            {
                var introRid = timelines[1].RowId;
                return (ushort)introRid;
            }
        }
        catch { }
        return 0;
    }

    public static ushort ResolveLoopingTimeline(Emote emote)
    {
        ushort timelineId = 0;
        try
        {
            var timelines = emote.ActionTimeline;
            var sheet = Service.DataManager.GetExcelSheet<ActionTimeline>();
            if (sheet == null)
                return 0;

            if (timelines.Count > 0)
            {
                var rid0 = timelines[0].RowId;
                if (rid0 != 0)
                {
                    var row0 = sheet.GetRow((uint)rid0);
                    if (row0.IsLoop)
                        return (ushort)rid0;
                }
            }

            for (var i = 0; i < timelines.Count; i++)
            {
                var rid = timelines[i].RowId;
                if (rid == 0) continue;
                var row = sheet.GetRow((uint)rid);
                if (row.IsLoop)
                {
                    timelineId = (ushort)rid;
                    break;
                }
            }
        }
        catch
        {
            timelineId = 0;
        }

        return timelineId;
    }

    public static ushort ResolveNonLoopingTimeline(Emote emote)
    {
        ushort timelineId = 0;
        try
        {
            var timelines = emote.ActionTimeline;
            var sheet = Service.DataManager.GetExcelSheet<ActionTimeline>();

            if (sheet != null)
            {
                for (var i = 0; i < timelines.Count; i++)
                {
                    var rid = timelines[i].RowId;
                    if (rid == 0) continue;
                    var row = sheet.GetRow((uint)rid);

                    if (!row.IsLoop)
                    {
                        timelineId = (ushort)rid;
                        break;
                    }
                }
            }

            if (timelineId == 0)
            {
                if (timelines.Count > 1 && timelines[1].RowId != 0)
                    timelineId = (ushort)timelines[1].RowId;
                else if (timelines.Count > 0 && timelines[0].RowId != 0)
                    timelineId = (ushort)timelines[0].RowId;
                else
                {
                    for (var i = 2; i < timelines.Count; i++)
                    {
                        if (timelines[i].RowId != 0)
                        {
                            timelineId = (ushort)timelines[i].RowId;
                            break;
                        }
                    }
                }
            }
        }
        catch
        {
            timelineId = 0;
        }

        return timelineId;
    }
}
