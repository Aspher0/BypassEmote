using BypassEmote.Localization;
using NoireLib;
using NoireLib.Helpers;
using System.Numerics;

namespace BypassEmote.Helpers;

internal static class ModActionChatPayloads
{
    private static readonly Vector3 LinkColor = ColorHelper.HexToVector3("#4FA3FF");

    private static ChatText PenumbraReason()
        => Service.Penumbra?.Unavailable is { IsEmpty: false } reason ? reason : L.PenumbraNotRunning;

    internal static void Append(NoireLogger.ChatMessageBuilder chat, string modDirectory, string modName)
    {
        if (string.IsNullOrEmpty(modDirectory))
            return;

        chat.AddText(" ");
        chat.AddLink(L.OpenLink.Text, $"BypassEmote.OpenMod.{modDirectory}", () => Open(modDirectory, modName), LinkColor);
        chat.AddText(" ");
        chat.AddLink(L.DisableLink.Text, $"BypassEmote.DisableMod.{modDirectory}", () => Disable(modDirectory, modName), LinkColor);
    }

    private static void Open(string modDirectory, string modName)
    {
        if (Service.Penumbra is not { Available: true } penumbra)
        {
            LogHelper.Error(ChatText.Of(L.ModNotOpened, "reason", PenumbraReason()));
            return;
        }

        if (!penumbra.OpenMod(modDirectory, modName))
            LogHelper.Error(ChatText.Of(L.PenumbraWouldNotOpen, "mod", modName), "modaction.open-failed");
    }

    private static void Disable(string modDirectory, string modName)
    {
        if (Service.Penumbra is not { Available: true } penumbra)
        {
            LogHelper.Error(ChatText.Of(L.ModNotDisabled, "reason", PenumbraReason()));
            return;
        }

        if (penumbra.GetPlayerCollection() is not { } collection)
        {
            LogHelper.Error(L.NoCollectionModNotDisabled);
            return;
        }

        if (!penumbra.TrySetModEnabled(collection.Id, modDirectory, false))
        {
            LogHelper.Error(ChatText.Of(L.PenumbraWouldNotSwitchOff, "mod", modName, "collection", collection.Name), "modaction.disable-failed");
            return;
        }

        LogHelper.Info(ChatText.Of(L.ModNowOff, "mod", modName, "collection", collection.Name));
    }
}
