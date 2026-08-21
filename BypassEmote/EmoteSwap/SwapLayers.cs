namespace BypassEmote.EmoteSwap;

public static class SwapLayers
{
    public static bool UniquePackNames { get; set; } = true;
    public static bool DoorLoader { get; set; } = false;
    public static bool DoorBind { get; set; } = false;
    public static bool DoorRequest { get; set; } = true;
    public static bool MatchEnforcement { get; set; } = true;
    public static bool PrewarmPacks { get; set; } = false;
    public static bool NoCacheFlip { get; set; } = false;
    public static bool BindingPackCorrection { get; set; } = true;
    public static bool MappingPackCorrection { get; set; } = true;
    public static bool PublishVanillaPath { get; set; } = true;
    public static bool AlwaysComposePaths { get; set; } = false;
    public static bool SwapOwnedEmotes { get; set; } = false;
}
