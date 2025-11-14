using BypassEmote.Helpers;
using BypassEmote.Models;
using System.Collections.Generic;

namespace BypassEmote.Data;

public static class EmoteData
{
    public static readonly List<EmoteSpecification> EmoteSpecifications = new()
    {
        new EmoteSpecification(290u, EmotePlayType.OneShot, $"{CommonHelper.GetRealEmoteNameById(288u)} <Looking down>" ), // Photograph
        new EmoteSpecification(289u, EmotePlayType.OneShot, $"{CommonHelper.GetRealEmoteNameById(288u)} <Looking up>" ), // Photograph
        new EmoteSpecification(287u, EmotePlayType.Looped, $"{CommonHelper.GetRealEmoteNameById(283u)} <Lower bouquet>" ), // Bouquet while targetting another player using Bouquet
        new EmoteSpecification(268u, EmotePlayType.OneShot, $"{CommonHelper.GetRealEmoteNameById(267)} <No target>"), // All saint's charm without a target
        new EmoteSpecification(255u, EmotePlayType.Looped, $"{CommonHelper.GetRealEmoteNameById(50u)} <Pose4>", CommonHelper.GetRealEmoteIconById(50u) ), // Chair sit
        new EmoteSpecification(254u, EmotePlayType.Looped, $"{CommonHelper.GetRealEmoteNameById(50u)} <Pose3>", CommonHelper.GetRealEmoteIconById(50u) ), // Chair sit
        new EmoteSpecification(253u, EmotePlayType.Looped, "Umbrella <Pose3>" ),
        new EmoteSpecification(244u, EmotePlayType.Looped, "Umbrella <Pose2>" ),
        new EmoteSpecification(243u, EmotePlayType.Looped, "Umbrella <Pose1>" ),
        new EmoteSpecification(219u, EmotePlayType.Looped, $"{CommonHelper.GetRealEmoteNameById(90u)} <Pose6>", CommonHelper.GetRealEmoteIconById(90u) ), // Change pose
        new EmoteSpecification(218u, EmotePlayType.Looped, $"{CommonHelper.GetRealEmoteNameById(90u)} <Pose5>", CommonHelper.GetRealEmoteIconById(90u) ), // Change pose
        new EmoteSpecification(179u, EmotePlayType.OneShot, $"{CommonHelper.GetRealEmoteNameById(178u)} <No target>" ), // Splash without a target
        new EmoteSpecification(175u, EmotePlayType.OneShot ), // Ultima
        new EmoteSpecification(151u, EmotePlayType.OneShot, specificOneShotActionTimelineSlot: 4 ), // Water flip
        new EmoteSpecification(150u, EmotePlayType.Looped, specificLoopActionTimelineSlot: 4 ), // Water float
        new EmoteSpecification(147u, EmotePlayType.OneShot, $"{CommonHelper.GetRealEmoteNameById(146u)} <No target>", CommonHelper.GetRealEmoteIconById(146u) ), // Dote
        new EmoteSpecification(144u, EmotePlayType.OneShot ), // Diamond Dust
        new EmoteSpecification(138u, EmotePlayType.OneShot ), // Zantetsuken
        new EmoteSpecification(117u, EmotePlayType.Looped, $"{CommonHelper.GetRealEmoteNameById(52u)} <Pose3>", CommonHelper.GetRealEmoteIconById(52u) ), // Groundsit
        new EmoteSpecification(108u, EmotePlayType.Looped, $"{CommonHelper.GetRealEmoteNameById(90u)} <Pose4>", CommonHelper.GetRealEmoteIconById(90u) ), // Change pose
        new EmoteSpecification(107u, EmotePlayType.Looped, $"{CommonHelper.GetRealEmoteNameById(90u)} <Pose3>", CommonHelper.GetRealEmoteIconById(90u) ), // Change pose
        new EmoteSpecification(100u, EmotePlayType.DoNotPlay, $"{CommonHelper.GetRealEmoteNameById(88u)} <Pose2>", CommonHelper.GetRealEmoteIconById(13u) ), // Bed pose, DO NOT PLAY since it messes with the character and the server
        new EmoteSpecification(99u, EmotePlayType.DoNotPlay, $"{CommonHelper.GetRealEmoteNameById(88u)} <Pose1>", CommonHelper.GetRealEmoteIconById(13u) ), // Bed pose, DO NOT PLAY since it messes with the character and the server
        new EmoteSpecification(98u, EmotePlayType.Looped, $"{CommonHelper.GetRealEmoteNameById(52u)} <Pose2>", CommonHelper.GetRealEmoteIconById(52u) ), // Groundsit
        new EmoteSpecification(97u, EmotePlayType.Looped, $"{CommonHelper.GetRealEmoteNameById(52u)} <Pose1>", CommonHelper.GetRealEmoteIconById(52u) ), // Groundsit
        new EmoteSpecification(96u, EmotePlayType.Looped, $"{CommonHelper.GetRealEmoteNameById(50u)} <Pose2>", CommonHelper.GetRealEmoteIconById(50u) ), // Chair sit
        new EmoteSpecification(95u, EmotePlayType.Looped, $"{CommonHelper.GetRealEmoteNameById(50u)} <Pose1>", CommonHelper.GetRealEmoteIconById(50u) ), // Chair sit
        new EmoteSpecification(93u, EmotePlayType.Looped, $"Drawn weapon <Pose1>", CommonHelper.GetRealEmoteIconById(238u) ), // Pose1 when weapon is drawn
        new EmoteSpecification(92u, EmotePlayType.Looped, $"{CommonHelper.GetRealEmoteNameById(90u)} <Pose2>", CommonHelper.GetRealEmoteIconById(90u) ), // Change pose
        new EmoteSpecification(91u, EmotePlayType.Looped, $"{CommonHelper.GetRealEmoteNameById(90u)} <Pose1>", CommonHelper.GetRealEmoteIconById(90u) ), // Change pose
        new EmoteSpecification(89u, EmotePlayType.DoNotPlay, $"{CommonHelper.GetRealEmoteNameById(51u)} <From bed pose>", CommonHelper.GetRealEmoteIconById(51u) ), // Stand up from bed pose, DO NOT PLAY since it messes with the character and the server
        new EmoteSpecification(88u, EmotePlayType.DoNotPlay, null, CommonHelper.GetRealEmoteIconById(13u) ), // Bed pose, DO NOT PLAY since it messes with the character and the server
        new EmoteSpecification(87u, EmotePlayType.OneShot, $"{CommonHelper.GetRealEmoteNameById(87u)} <No target>", CommonHelper.GetRealEmoteIconById(87u) ), // Throw snowball without a target
        new EmoteSpecification(86u, EmotePlayType.OneShot, $"{CommonHelper.GetRealEmoteNameById(86u)} <With target>", CommonHelper.GetRealEmoteIconById(86u) ), // Throw snowball with a target
        new EmoteSpecification(62u, EmotePlayType.OneShot ), // Megaflare
        new EmoteSpecification(60u,61u, EmotePlayType.DoNotPlay ), // Visor, does nothing
        new EmoteSpecification(53u, EmotePlayType.OneShot, $"{CommonHelper.GetRealEmoteNameById(53u)} <From groundsit>", CommonHelper.GetRealEmoteIconById(53u) ), // Stand up from groundsit
        new EmoteSpecification(51u, EmotePlayType.OneShot, $"{CommonHelper.GetRealEmoteNameById(51u)} <From chairsit>", CommonHelper.GetRealEmoteIconById(51u) ), // Stand up from groundsit
        new EmoteSpecification(0u, EmotePlayType.DoNotPlay ), // Base idle pose
    };
}
