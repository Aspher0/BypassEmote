using System.Collections.Generic;

namespace BypassEmote.Models;

public class FfxivCollectResponse
{
    public int count { get; set; }
    public List<FfxivCollectEmote> results { get; set; } = [];
}
