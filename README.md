# Repository link
`https://raw.githubusercontent.com/Aspher0/BypassEmote/refs/heads/main/repo.json`

# Definition and usage

This is a simple plugin allowing you to play any game emote, regardless of whether it has been unlocked or not.<br/>
It is as simple as typing the usual emote command in chat.<br/>
This is not an unlock cheat and is client side only, therefore, other people won't see your emotes* and nothing will be sent to the server.<br/>
\* Other players won't be able to see your emotes, unless you are using a syncing plugin that integrates BypassEmote.

- After enabling the plugin, you can type `/bypassemote` or `/be` to open the locked emotes UI.<br/>
- By adding the "c" or "config" argument (`/be c`, `/be config`), you will open the configuration window.<br/>
- By adding an emote argument (`/be /tea`, `/be tdance`, etc.), the emote will play. This will force the game into using BypassEmote.<br/>
- Alternatively, you can simply type the emote command in chat (`/tea`, `/tdance`, etc) and it will let the game handle it if you have unlocked that emote, otherwise it will use BypassEmote.<br/>
A good use case of using `/be <emote>` while having unlocked the emote is if you want to play an animation with enabled sound. By using `/be sync` or `/be syncall` (provided that players you want to sync are using BypassEmote to play emotes), the sound associated to the emote will also reset.<br/>
- Using `/be sync` or `/be syncall` will allow to reset every players animations to 0, hence "syncing" duo emotes, for example. Using `/be sync` will only sync players bypassing emotes, meanwhile `/be syncall` will sync every player on the map.<br/>
Moreover, sync commands will reset any sound associated to the emote, but please note that those two commands will only reset sounds if the owning player is using BypassEmote to play the emote. It will not reset sounds if the player is emoting normally.<br/>
- By targetting an NPC and typing `/bet <emote_command>` or `/bet stop`, it will apply the provided animation to the targetted NPC, or completely stop it.
- By typing `/bem <emote_command>` or `/bem stop`, it will apply the provided animation to your minion if summoned, or completely stop it. You do not need to target your minion, and of course you would need to turn it into a human first with glamourer.

In the configuration window, you will be able to disable the plugin for yourself, which means the plugin will not play any emote for you anymore when you type the commands in chat, and locked emotes will not be applied unless you use the plugin's UI.

# FAQ

**Q**: Is this safe to use?<br/>
**A**: While I cannot guarantee 100% safety, I do everything in my power to minimize any possible risk and take safety with high consideration. I have added multiple securities and checks to make BypassEmote as safe as possible.<br/>
Currently, BypassEmote is 99.99% safe with only one extremely minor theorical issue that might not even be a realistic one anyway, and I am still trying to fix it. If you want to read more, head to [issue #7](https://github.com/Aspher0/BypassEmote/issues/7).<br/>
TL;DR: It is relatively safe, has been tested extensively, reviewed by some competent developer (and deemed safe), and does not directly send anything to the servers.

**Q**: I installed BypassEmote and (some sync plugin), but I cannot see my friend's emote, is this a bug?<br/>
**A**: BypassEmote does not work on its own, developpers need to integrate this plugin's IPC methods in their codebase to allow relaying emote messages.<br/>
Try asking the developers of the sync plugin you use to integrate BypassEmote, but please respect them if they refuse.

**Q**: I found a bug, how and where can I report it?<br/>
**A**: Just open an issue on this Github repository explaining the bug, how to reproduce it and try providing any relevant errors that may appear in the `/xllog` window by filtering the regex global filter with "BypassEmote".
