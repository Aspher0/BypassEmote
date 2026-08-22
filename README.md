# Repository link
`https://raw.githubusercontent.com/Aspher0/BypassEmote/refs/heads/main/repo.json`

# Definition and usage

This is a simple plugin allowing you to play any game emote, regardless of whether it has been unlocked or not.<br/>
It is as simple as typing the usual emote command in chat.<br/>
This is not an unlock cheat, nothing is unlocked on your account and the emotes stay locked as far as the game is concerned.

Emotes are played according to your character's condition, whichever way you bypass them. Sitting on the ground, sitting in a chair, mounted, riding pillion, swimming, diving, holding an umbrella or a torch, wearing a fashion accessory: the plugin plays the variant that matches the state you are in, instead of forcing the standing animation on you.<br/>
If an emote cannot be played in the state you are in, the plugin will tell you so instead of playing something wrong, and the main UI shows the same condition icons as the game's own emote window so you can see at a glance where an emote can be played.

Since 2.0.0.0, there are two ways of bypassing an emote, and a popup will appear on your first launch to let you pick the one you want. You can change your mind at any time in the configuration window.

## Emote Swap

This is the new method, it is the recommended one, and it requires Penumbra.<br/>
Instead of applying the animation on your character, the plugin looks for an emote you have unlocked that behaves like the one you asked for, plays that one for real, and swaps its animation with the locked one through a Penumbra mod it manages for you.<br/>
Since the game itself is playing the emote, there is nothing for anyone to notice, and other players see it over any sync service without their plugin having to integrate BypassEmote.

## Direct Play

This is the original method, and it does not need Penumbra.<br/>
The animation is applied on your character from your own client only, therefore, other people won't see your emotes\* and nothing is sent to the server.<br/>
\* Other players won't be able to see your emotes, unless they are using a syncing plugin that integrates BypassEmote.

By default, Direct Play will only play emotes from the base pose (pose 0) of your current stance.<br/>
This is because the game client sends duplicate change pose packets when your character is in any other pose. This is not caused by the plugin itself, but rather by how the game handles pose changes, and the "Idle Animation Delay" setting in the game's Character Configuration > Control Settings > Character tab is what causes it.<br/>
You can lift that limit in the configuration window if you know what you are doing, but please note that safe mode is not a promise either, it only keeps Direct Play to the states where nothing has ever been seen to go wrong.

## Commands

- After enabling the plugin, you can type `/bypassemote` or `/be` to open the locked emotes UI.<br/>
- By adding the "c" or "config" argument (`/be c`, `/be config`), you will open the configuration window.<br/>
- By adding an emote argument (`/be /tea`, `/be tdance`, etc.), the emote will play. This will force the game into using BypassEmote.<br/>
- Alternatively, you can simply type the emote command in chat (`/tea`, `/tdance`, etc) and it will let the game handle it if you have unlocked that emote, otherwise it will use BypassEmote.<br/>
A good use case of using `/be <emote>` while having unlocked the emote is if you want to play an animation with enabled sound. By using `/be sync` or `/be syncall` (provided that players you want to sync are using BypassEmote to play emotes), the sound associated to the emote will also reset.<br/>
- Using `/be sync` or `/be syncall` will allow to reset every players animations to 0, hence "syncing" duo emotes, for example. Using `/be sync` will only sync players bypassing emotes, meanwhile `/be syncall` will sync every player on the map.<br/>
Moreover, sync commands will reset any sound associated to the emote, but please note that those two commands will only reset sounds if the owning player is using BypassEmote to play the emote. It will not reset sounds if the player is emoting normally.<br/>
- By targetting an NPC and typing `/bet <emote_command>` or `/bet stop`, it will apply the provided animation to the targetted NPC, or completely stop it.
- By typing `/bem <emote_command>` or `/bem stop`, it will apply the provided animation to your minion if summoned, or completely stop it. You do not need to target your minion.<br/>
The same goes for `/bep` with your pet, and `/bec` with your chocobo.

You can also create a permanent Penumbra mod from any emote, by clicking the "Create a mod" button in the main UI, or by right clicking an emote and picking "Create a mod from this emote...". This is useful if you want to keep an animation over one of your emotes without the plugin having to do anything.

Everything in the configuration window comes set up the way it works best, so you do not need to touch any of it unless you want to.<br/>
In the configuration window, you will be able to disable the plugin for yourself, which means the plugin will not play any emote for you anymore when you type the commands in chat, and locked emotes will not be applied unless you use the plugin's UI.

# FAQ

**Q**: Is this safe to use?<br/>
**A**: With Emote Swap, yes. The emote your character plays is one you own and the game plays it itself, so there is nothing the server may notice, which is exactly what [issue #7](https://github.com/Aspher0/BypassEmote/issues/7) was about.<br/>
With Direct Play, safe mode keeps you to the states where nothing has ever been seen to go wrong, and I do everything in my power to minimize any possible risk, but if you want to be certain, use Emote Swap.

**Q**: Do I need Penumbra?<br/>
**A**: Only for Emote Swap. Direct Play works on its own, and the plugin will tell you if Penumbra is missing when you try to use Emote Swap.

**Q**: I installed BypassEmote and (some sync plugin), but I cannot see my friend's emote, is this a bug?<br/>
**A**: If your friend is using Emote Swap, you should see it, since what gets synced is a regular emote and a Penumbra mod, and nothing else is needed on either side.<br/>
If they are using Direct Play, then no. BypassEmote does not work on its own in that mode, developpers need to integrate this plugin's IPC methods in their codebase to allow relaying emote messages.<br/>
Try asking the developers of the sync plugin you use to integrate BypassEmote, but please respect them if they refuse.

**Q**: The plugin tells me the game build has not been approved, what does that mean?<br/>
**A**: A game patch can move the functions BypassEmote needs. So after a patch, the parts of the plugin that rely on them stay off until I have checked the new build and approved it. The plugin checks every 10 minutes on its own, and you can also check for updates manually in the configuration window. The plugin will keep working but you may need to stop your current bypass to bypass another emote.

**Q**: I found a bug, how and where can I report it?<br/>
**A**: Just open an issue on this Github repository explaining the bug, how to reproduce it and try providing any relevant errors that may appear in the `/xllog` window by filtering the regex global filter with "BypassEmote".
