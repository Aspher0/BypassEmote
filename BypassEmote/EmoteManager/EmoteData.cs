using BypassEmote.Helpers;
using System;
using System.Collections.Generic;

namespace BypassEmote.Data;

public static class EmoteData
{
    public enum EmotePlayType
    {
        Looped = 1,
        OneShot = 2,
        DoNotPlay = 0,
    }

    public enum EmoteCategory
    {
        General = 1,
        Special = 2,
        Expressions = 3,
        Unknown = 0,
    }

    // Emote ID, (PlayType, Optional Name Override, Optional Icon Override)
    public static readonly Dictionary<object, (EmotePlayType PlayType, string? Name, ushort? Icon)> EmoteSpecifications = new Dictionary<object, (EmotePlayType PlayType, string? Name, ushort? Icon)>
    {
        { Tuple.Create(308u, 320u), (EmotePlayType.DoNotPlay, null, null ) }, // Unknown emotes, does nothing
        { Tuple.Create(305u, 306u), (EmotePlayType.DoNotPlay, null, null ) }, // Unknown emotes, does nothing
        { 297u, (EmotePlayType.DoNotPlay, null, null ) }, // Unknown emote, does nothing
        { 290u, (EmotePlayType.OneShot, $"{CommonHelper.GetRealEmoteNameById(288u)} <Looking down>", null ) }, // Photograph
        { 289u, (EmotePlayType.OneShot, $"{CommonHelper.GetRealEmoteNameById(288u)} <Looking up>", null ) }, // Photograph
        { 287u, (EmotePlayType.Looped, $"{CommonHelper.GetRealEmoteNameById(283u)} <Lower bouquet>", null ) }, // Bouquet while targetting another player using Bouquet
        { 268u, (EmotePlayType.OneShot, $"{CommonHelper.GetRealEmoteNameById(267)} <No target>", null) }, // All saint's charm without a target
        { 255u, (EmotePlayType.Looped, $"{CommonHelper.GetRealEmoteNameById(50u)} <Pose 4>", CommonHelper.GetRealEmoteIconById(50u) ) }, // Chair sit
        { 254u, (EmotePlayType.Looped, $"{CommonHelper.GetRealEmoteNameById(50u)} <Pose 3>", CommonHelper.GetRealEmoteIconById(50u) ) }, // Chair sit
        { 253u, (EmotePlayType.Looped, "Umbrella <Pose 3>", null ) },
        { 244u, (EmotePlayType.Looped, "Umbrella <Pose 2>", null ) },
        { 243u, (EmotePlayType.Looped, "Umbrella <Pose 1>", null ) },
        { 219u, (EmotePlayType.Looped, $"{CommonHelper.GetRealEmoteNameById(90u)} <Pose 6>", CommonHelper.GetRealEmoteIconById(90u) ) }, // Change pose
        { 218u, (EmotePlayType.Looped, $"{CommonHelper.GetRealEmoteNameById(90u)} <Pose 5>", CommonHelper.GetRealEmoteIconById(90u) ) }, // Change pose
        { 179u, (EmotePlayType.OneShot, $"{CommonHelper.GetRealEmoteNameById(178u)} <No target>", null ) }, // Splash without a target
        { 177u, (EmotePlayType.DoNotPlay, null, null ) }, // Unknown emote, does nothing
        { 175u, (EmotePlayType.OneShot, null, null ) }, // Ultima
        { 168u, (EmotePlayType.DoNotPlay, null, null ) }, // Unknown emote, does nothing
        { 147u, (EmotePlayType.OneShot, $"{CommonHelper.GetRealEmoteNameById(146u)} <No target>", CommonHelper.GetRealEmoteIconById(146u) ) }, // Dote
        { 144u, (EmotePlayType.OneShot, null, null ) }, // Diamond Dust
        { 138u, (EmotePlayType.OneShot, null, null ) }, // Zantetsuken
        { 117u, (EmotePlayType.Looped, $"{CommonHelper.GetRealEmoteNameById(52u)} <Pose 3>", CommonHelper.GetRealEmoteIconById(52u) ) }, // Groundsit
        { 116u, (EmotePlayType.DoNotPlay, null, null ) }, // Unknown emote, does nothing
        { 108u, (EmotePlayType.Looped, $"{CommonHelper.GetRealEmoteNameById(90u)} <Pose 4>", CommonHelper.GetRealEmoteIconById(90u) ) }, // Change pose
        { 107u, (EmotePlayType.Looped, $"{CommonHelper.GetRealEmoteNameById(90u)} <Pose 3>", CommonHelper.GetRealEmoteIconById(90u) ) }, // Change pose
        { 100u, (EmotePlayType.DoNotPlay, $"{CommonHelper.GetRealEmoteNameById(88u)} <Pose 2>", CommonHelper.GetRealEmoteIconById(13u) ) }, // Bed pose, DO NOT PLAY since it forces lying down and prevents movement
        { 99u, (EmotePlayType.DoNotPlay, $"{CommonHelper.GetRealEmoteNameById(88u)} <Pose 1>", CommonHelper.GetRealEmoteIconById(13u) ) }, // Bed pose, DO NOT PLAY since it forces lying down and prevents movement
        { 98u, (EmotePlayType.Looped, $"{CommonHelper.GetRealEmoteNameById(52u)} <Pose 2>", CommonHelper.GetRealEmoteIconById(52u) ) }, // Groundsit
        { 97u, (EmotePlayType.Looped, $"{CommonHelper.GetRealEmoteNameById(52u)} <Pose 1>", CommonHelper.GetRealEmoteIconById(52u) ) }, // Groundsit
        { 96u, (EmotePlayType.Looped, $"{CommonHelper.GetRealEmoteNameById(50u)} <Pose 2>", CommonHelper.GetRealEmoteIconById(50u) ) }, // Chair sit
        { 95u, (EmotePlayType.Looped, $"{CommonHelper.GetRealEmoteNameById(50u)} <Pose 1>", CommonHelper.GetRealEmoteIconById(50u) ) }, // Chair sit
        { 94u, (EmotePlayType.DoNotPlay, null, null ) }, // Unknown emote, does nothing
        { 93u, (EmotePlayType.Looped, $"Drawn weapon <Pose 1>", CommonHelper.GetRealEmoteIconById(238u) ) }, // Pose 1 when weapon is drawn
        { 92u, (EmotePlayType.Looped, $"{CommonHelper.GetRealEmoteNameById(90u)} <Pose 2>", CommonHelper.GetRealEmoteIconById(90u) ) }, // Change pose
        { 91u, (EmotePlayType.Looped, $"{CommonHelper.GetRealEmoteNameById(90u)} <Pose 1>", CommonHelper.GetRealEmoteIconById(90u) ) }, // Change pose
        { 89u, (EmotePlayType.DoNotPlay, $"{CommonHelper.GetRealEmoteNameById(51u)} <From bed pose>", CommonHelper.GetRealEmoteIconById(51u) ) }, // Stand up from bed pose, DO NOY PLAY since it prevents movement for a moment
        { 88u, (EmotePlayType.DoNotPlay, null, CommonHelper.GetRealEmoteIconById(13u) ) }, // Bed pose, DO NOT PLAY since it forces lying down and prevents movement
        { 87u, (EmotePlayType.OneShot, $"{CommonHelper.GetRealEmoteNameById(87u)} <No target>", CommonHelper.GetRealEmoteIconById(87u) ) }, // Throw snowball without a target
        { 86u, (EmotePlayType.OneShot, $"{CommonHelper.GetRealEmoteNameById(86u)} <With target>", CommonHelper.GetRealEmoteIconById(86u) ) }, // Throw snowball with a target
        { 62u, (EmotePlayType.OneShot, null, null ) }, // Megaflare
        { Tuple.Create(60u, 61u), (EmotePlayType.DoNotPlay, null, null ) }, // Visor, does nothing
        { 53u, (EmotePlayType.OneShot, $"{CommonHelper.GetRealEmoteNameById(53u)} <From groundsit>", CommonHelper.GetRealEmoteIconById(53u) ) }, // Stand up from groundsit
        { 51u, (EmotePlayType.OneShot, $"{CommonHelper.GetRealEmoteNameById(51u)} <From chairsit>", CommonHelper.GetRealEmoteIconById(51u) ) }, // Stand up from groundsit
        { 0u, (EmotePlayType.DoNotPlay, null, null ) }, // Base idle pose
    };
}
