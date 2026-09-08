using BypassEmote.Enums;
using BypassEmote.Models;
using Newtonsoft.Json.Linq;
using NoireLib.Configuration;
using NoireLib.Configuration.Migrations;
using NoireLib.Helpers.ObjectExtensions;
using System;
using System.Collections.Generic;

namespace BypassEmote;

[NoireConfig("Configuration")]
[AutoSave]
public class ConfigurationInstance : NoireConfigBase
{
    public override string GetConfigFileName() => "Configuration";
    public override int Version { get; set; } = 2;

    public bool PluginEnabled { get; set; } = true;

    public bool ShowWindowsInGpose { get; set; } = false;

    public bool ShowWindowsWhenUiHidden { get; set; } = false;

    public List<uint> FavoriteEmotes { get; set; } = new List<uint>();

    public List<uint> BlockedTargetEmotesEmoteSwap { get; set; } = new List<uint>();

    public List<EmoteOverride> EmoteOverrides { get; set; } = new List<EmoteOverride>();

    public bool ShowUpdateNotification { get; set; } = true;

    public bool ShowChangelogOnUpdate { get; set; } = true;

    public bool ShowAllEmotes { get; set; } = false;

    public bool ShowEmoteIds { get; set; } = false;

    public bool ShowInvalidEmotes { get; set; } = false;

    /// <summary> Last hotbar picked in the assign window: 0-9 are bars 1-10, 10-17 are XHB 1-8. </summary>
    public int AssignModalHotbar { get; set; } = 0;

    public bool BypassOnHotbarSlotTriggered { get; set; } = true;

    public bool AutoFaceTargetDirectPlay { get; set; } = true;

    public bool DirectPlayUnsafe { get; set; } = false;

    public bool StopOwnedObjectEmoteOnMove { get; set; } = true;

    public SelfBypassMode SelfBypassMode { get; set; } = SelfBypassMode.EmoteSwap;

    public SwapLifetime SwapLifetime { get; set; } = SwapLifetime.WhenTargetPlayed;

    public SwapBehavior SwapBehavior { get; set; } = SwapBehavior.KeepAll;

    // 0 means no cap. (fr ong)
    public int MaxKeptSwapsPerTarget { get; set; } = 5;

    public bool AnonymizeModName { get; set; } = false;

    public LoopMatchRule LoopMatching { get; set; } = LoopMatchRule.Strict;

    public TurnMatchRule TurnMatching { get; set; } = TurnMatchRule.Lenient;

    public SoundMatchRule SoundMatching { get; set; } = SoundMatchRule.Lenient;

    public CachedDispatchMode CachedDispatch { get; set; } = CachedDispatchMode.On;

    public int MaxTargetsPerRank { get; set; } = 3;

    public DispatchFidelity DispatchFidelity { get; set; } = DispatchFidelity.OneRankBelow;

    public IdlePoseFallback IdlePoseLoops { get; set; } = IdlePoseFallback.NothingElseFits;

    public ModdedTargetRule ModdedTargets { get; set; } = ModdedTargetRule.LastResort;

    public bool ShowSwapMessages { get; set; } = false;

    public bool ShowWarningMessages { get; set; } = true;

    public TimeSpan ThrottleTimeWarnings { get; set; } = 5.Minutes();

    public bool ShowErrorMessages { get; set; } = true;

    public TimeSpan ThrottleTimeErrors { get; set; } = TimeSpan.Zero;

    public bool SwapPromptPending { get; set; } = false;

    public string ApprovedGameVersion { get; set; } = string.Empty;

    public string ApprovedPluginVersion { get; set; } = string.Empty;

    public string AnnouncedApprovalGameVersion { get; set; } = string.Empty;

    public bool AlwaysCacheBreak { get; set; } = false;

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
