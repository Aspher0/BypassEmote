using NoireLib;
using NoireLib.Helpers;
using System.Numerics;

namespace BypassEmote.Helpers;

// Chat payloads (links) to look at a mod that got in a swap's way, or switch it off.
internal static class ModActionChatPayloads
{
    private static readonly Vector3 LinkColor = ColorHelper.HexToVector3("#4FA3FF");

    private const string OpenFailedKind = "modaction.open-failed";
    private const string DisableFailedKind = "modaction.disable-failed";

    internal static void Append(NoireLogger.ChatMessageBuilder chat, string modDirectory, string modName)
    {
        if (string.IsNullOrEmpty(modDirectory))
            return;

        chat.AddText(" ");
        chat.AddLink("[Open]", $"BypassEmote.OpenMod.{modDirectory}", () => Open(modDirectory, modName), LinkColor);
        chat.AddText(" ");
        chat.AddLink("[Disable]", $"BypassEmote.DisableMod.{modDirectory}", () => Disable(modDirectory, modName), LinkColor);
    }

    private static void Open(string modDirectory, string modName)
    {
        if (Service.Penumbra is not { Available: true } penumbra)
        {
            FeedbackHelper.Error("Penumbra is not available. Mod not opened.");
            return;
        }

        if (!penumbra.OpenMod(modDirectory, modName))
            FeedbackHelper.Error($"Penumbra would not open '{modName}'.", OpenFailedKind);
    }

    private static void Disable(string modDirectory, string modName)
    {
        if (Service.Penumbra is not { Available: true } penumbra)
        {
            FeedbackHelper.Error("Penumbra is not available. Mod not disabled.");
            return;
        }

        if (penumbra.GetPlayerCollection() is not { } collection)
        {
            FeedbackHelper.Error("No Penumbra collection is assigned to your character. Mod not disabled.");
            return;
        }

        if (!penumbra.TrySetModEnabled(collection.Id, modDirectory, false))
        {
            FeedbackHelper.Error($"Penumbra would not switch '{modName}' off in {collection.Name}.", DisableFailedKind);
            return;
        }

        FeedbackHelper.Info($"'{modName}' is now off in {collection.Name}.");
    }
}
