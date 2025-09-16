using System.Collections.Generic;

namespace BypassEmote.Models;

public class FfxivCollectEmote
{
    public int? id { get; set; }
    public string? name { get; set; }
    public string? command { get; set; }
    public string? patch { get; set; }
    public List<FfxivCollectSource>? sources { get; set; }
}
