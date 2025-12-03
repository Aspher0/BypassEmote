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

In the configuration window, you will be able to disable the plugin for yourself, which means the plugin will not play any emote for you anymore when you type the commands in chat, and locked emotes will not be applied unless you use the plugin's UI.
