# Repository link
`https://raw.githubusercontent.com/Aspher0/BypassEmote/refs/heads/main/repo.json`

# Definition and usage

This is a simple plugin allowing you to play any game emote, regardless of whether it has been unlocked or not.<br/>
It is as simple as typing the usual emote command in chat, such as `/tea` or `\beesknees`.<br/>
This is NOT an unlock cheat.

Emotes are played according to your character's condition: sitting on the ground, sitting in a chair, mounted, riding pillion, swimming, diving, holding an umbrella or a torch, wearing a fashion accessory. This was an issue in the first versions, it's now completely fixed!<br/>

Since 2.0.0.0, there are two ways of bypassing an emote, and a popup will appear on your first launch (if you were using this plugin before) to let you pick the one you want. You can change your mind at any time in the configuration window.

## Emote Swap

This is the new recommended method, and it requires you to have Penumbra and to be paired with other players for them to see your bypassed emotes.<br/>
Instead of applying the animation on your character, the plugin looks for an emote you have unlocked that behaves like the one you asked for, swaps its animation with the locked one through a Penumbra mod it manages for you and then plays the unlocked emote.

## Direct Play

This is the original method and it does not need Penumbra.<br/>
The animation is applied on your character from your own client only, therefore, other people won't see your emotes\*.<br/>
\* Other players won't be able to see your emotes, unless they are using a syncing plugin that integrates BypassEmote's IPC.

By default, Direct Play will only play emotes from the base pose (pose 0) of your current stance.<br/>
This is because the game client sends duplicate change pose packets when your character is in any other pose. This is not caused by the plugin itself, but rather by how the game handles pose changes. The "Idle Animation Delay" setting in the game's Character Configuration > Control Settings > Character tab is what causes it.<br/>
You can switch to unsafe mode in the BypassEmote configuration window if you know what you are doing.<br/>
Please note that safe mode is not a promise either, it only keeps Direct Play working in states where I'm pretty sure it's safe.

Direct Play is 100% safe to apply on other characters such as minions, NPCs or other players, hence applying emotes to them will use Direct Play.

## Commands

- After enabling the plugin, you can type `/bypassemote` or `/be` to open the locked emotes UI.<br/>
- By adding the "c" or "config" argument (`/be c`, `/be config`), you will open the configuration window.<br/>
- By adding an emote argument (`/be /tea`, `/be tdance`, etc.), the emote will play. This will force the game into using BypassEmote.<br/>
- Alternatively, you can simply type the emote command in chat (`/tea`, `/tdance`, etc) and it will let the game handle it if you have unlocked that emote, otherwise it will use BypassEmote.<br/>
A good use case of using `/be <emote>` while having unlocked the emote is if you want to play an animation with enabled sound with Direct Play mode. By using `/be sync` or `/be syncall` (provided that players you want to sync with are using BypassEmote Direct Play to play emotes), the sound associated to the emote will also reset.<br/>
- Using `/be sync` or `/be syncall` will allow to reset every players animations to 0, hence "syncing" duo emotes, for example. Using `/be sync` will only sync players bypassing emotes in Direct Play, meanwhile `/be syncall` will sync every player on the map.<br/>
Moreover, sync commands will reset any sound associated to the emote, but please note that those two commands will only reset sounds if the owning player is using BypassEmote Direct Play to play the emote. It will not reset sounds if the player is emoting normally or through Emote Swap.<br/>
- By targetting an NPC and typing `/bet <emote_command>` or `/bet stop`, it will apply the provided animation to the targetted NPC, or completely stop it.
- By typing `/bem <emote_command>` or `/bem stop`, it will apply the provided animation to your minion if summoned, or completely stop it. You do not need to target your minion.<br/>
The same goes for `/bep` with your pet, and `/bec` with your chocobo.

You can also create a permanent Penumbra mod from any emote, by clicking the "Create a mod" button in the main UI, or by right clicking an emote and picking "Create a mod from this emote...". This is useful if you want to keep an animation over one of your emotes without the plugin having to do anything.

The settings are configured the best way possible in my opinion, so you shouldn't need to touch any of it.

# FAQ

**Q**: Is this safe to use?<br/>
**A**: With Emote Swap, yes. BypassEmote will create an animation mod like any other one you may find on modding websites.<br/>
With Direct Play, the safe mode tries to prevent this by forcing you to be in idle pose 0. I try to do everything in my power to minimize any possible risk, but 100% safety just can not be achieved in Direct Play mode. See [issue #7](https://github.com/Aspher0/BypassEmote/issues/7) for more informations.

**Q**: Do I need Penumbra or anything else?<br/>
**A**: Only for Emote Swap, you will need Penumbra and to be synced/paired with other people. Emote swap creates an animation mod and requires other people to be able to see them.

**Q**: I installed BypassEmote and (some sync plugin), but I cannot see my friend's emote, is this a bug?<br/>
**A**: If your friend is using Emote Swap, you should see it.<br/>
If they are using Direct Play, then no. BypassEmote does not work on its own in that mode, developpers need to integrate this plugin's IPC methods in their codebase to allow relaying emote messages.<br/>
Try asking the developers of the sync plugin you use to integrate BypassEmote, but please respect them if they refuse.

**Q**: The plugin tells me the game build has not been approved, what does that mean?<br/>
**A**: A game patch can break some of the functions BypassEmote needs. Each patch, some of the plugin's functionnalities get turned off automatically until I have tested and approved them. The plugin checks for updates every 10 minutes and you can also check for updates manually in the configuration window. The plugin will keep working without it and it does NOT mean it is less safe. It only means you may need to stop your current bypass to bypass another emote.

**Q**: I found a bug, how and where can I report it?<br/>
**A**: Just open an issue on this Github repository explaining the bug, how to reproduce it and try providing any relevant errors that may appear in the `/xllog` window by filtering the regex global filter with "BypassEmote".
