using System.Collections.Generic;

namespace BypassEmote.Models;

public sealed class EmoteOverride
{
    public uint SourceEmote { get; set; }

    // Order in the list equals priority.
    public List<uint> Targets { get; set; } = new List<uint>();

    public bool LimitedToTargets { get; set; }
}
