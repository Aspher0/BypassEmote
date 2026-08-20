using BypassEmote.Models;
using Newtonsoft.Json.Linq;
using NoireLib.Configuration;
using NoireLib.Configuration.Migrations;
using NoireLib.Helpers.ObjectExtensions;
using System;
using System.Collections.Generic;

namespace BypassEmote;

[NoireConfig("Configuration")]
public class ConfigurationInstance : NoireConfigBase
{
    public override string GetConfigFileName() => "Configuration";
    public override int Version { get; set; } = 2;

    [AutoSave]
    public bool PluginEnabled { get; set; } = true;

    [AutoSave]
    public bool ShowWindowsInGpose { get; set; } = false;

    [AutoSave]
    public bool ShowWindowsWhenUiHidden { get; set; } = false;

    [AutoSave]
    public List<uint> FavoriteEmotes { get; set; } = new List<uint>();

    [AutoSave]
    public List<uint> BlockedTargetEmotesEmoteSwap { get; set; } = new List<uint>();

    [AutoSave]
    public bool ShowUpdateNotification { get; set; } = true;

    [AutoSave]
    public bool ShowChangelogOnUpdate { get; set; } = true;

    [AutoSave]
    public bool ShowAllEmotes { get; set; } = false;

    [AutoSave]
    public bool ShowEmoteIds { get; set; } = false;

    [AutoSave]
    public bool ShowInvalidEmotes { get; set; } = false;

    /// <summary> Last hotbar picked in the assign window: 0-9 are bars 1-10, 10-17 are XHB 1-8. </summary>
    [AutoSave]
    public int AssignModalHotbar { get; set; } = 0;

    [AutoSave]
    public bool BypassOnHotbarSlotTriggered { get; set; } = true;

    [AutoSave]
    public bool AutoFaceTargetDirectPlay { get; set; } = true;

    [AutoSave]
    public bool StopOwnedObjectEmoteOnMove { get; set; } = true;

    [AutoSave]
    public SelfBypassMode SelfBypassMode { get; set; } = SelfBypassMode.EmoteSwap;

    [AutoSave]
    public SwapModFlavor SwapModFlavor { get; set; } = SwapModFlavor.RealMod;

    [AutoSave]
    public SwapLifetime SwapLifetime { get; set; } = SwapLifetime.Ephemeral;

    [AutoSave]
    public LoopMatchRule LoopMatching { get; set; } = LoopMatchRule.Strict;

    [AutoSave]
    public TurnMatchRule TurnMatching { get; set; } = TurnMatchRule.Lenient;

    [AutoSave]
    public SoundMatchRule SoundMatching { get; set; } = SoundMatchRule.Strict;

    [AutoSave]
    public AlternateTargetsMode AlternateTargets { get; set; } = AlternateTargetsMode.TwoEmotesWhenCacheBreakDown;

    [AutoSave]
    public IdlePoseFallback IdlePoseLoops { get; set; } = IdlePoseFallback.NothingElseFits;

    [AutoSave]
    public ModdedTargetRule ModdedTargets { get; set; } = ModdedTargetRule.LastResort;

    [AutoSave]
    public bool ShowSwapMessages { get; set; } = false;

    [AutoSave]
    public bool ShowWarningMessages { get; set; } = true;

    [AutoSave]
    public TimeSpan ThrottleTimeWarnings { get; set; } = 5.Minutes();

    [AutoSave]
    public bool ShowErrorMessages { get; set; } = true;

    [AutoSave]
    public TimeSpan ThrottleTimeErrors { get; set; } = TimeSpan.Zero;

    [AutoSave]
    public bool SwapPromptPending { get; set; } = false;

    public class MigrationV1ToV2 : ConfigMigrationBase
    {
        public override int FromVersion => 1;
        public override int ToVersion => 2;
        public override string Migrate(JObject jsonObject) =>
            MigrationBuilder.Create()
                .AddProperty("SelfBypassMode", (int)SelfBypassMode.DirectPlay)
                .AddProperty("SwapPromptPending", true)
                .Migrate(jsonObject, 2);
    }
}
