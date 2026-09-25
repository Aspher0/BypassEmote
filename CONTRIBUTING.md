# Contributing to BypassEmote

- **Translations only** can be sent as a pull request right away.
- **Code changes** must start with an issue. Please do not open a pull request for a code change before it has been discussed and approved in an issue, it will most likely be closed and it might save you useless work. If you do want to contribute and add something yourself, explain what you want, how you would make it, and make it clear you want to create a pull request and do it yourself.

# Translations

Translations, and improvements to existing ones, are very much welcome. The plugin currently ships in English, French, German, Japanese, Korean and Simplified Chinese, but most of those are just made with a translator.

Every language is a single `.lang` file in [`BypassEmote/Localization/LangFiles`](BypassEmote/Localization/LangFiles).

## Translating in game

This is the easiest way, you see your changes live.

1. Open the settings, and click **Translate...** next to the language.
2. Pick a language, or type a language code (such as `it` or `pt-BR`) to start a new one.
3. Write your translations. **Show the plugin in this language** lets you see them in the real windows as you go.
4. Click **Save**. The file is written to `%AppData%\XIVLauncher\pluginConfigs\BypassEmote\Localization\<code>.lang`. **Save to a file...** lets you pick another place. I recommend you click save when editing, and then save to file when you want to export it for contribution.
5. Send that file as a pull request (see below).

## Translating without the game

1. Copy [`template.lang`](BypassEmote/Localization/LangFiles/template.lang) to `<code>.lang` in the same folder, for example `it.lang` or `pt-BR.lang`. Do not edit `template.lang` itself!
2. Write each translation after the `=` of its key. Empty translations fall back to english.

## Rules for a `.lang` file

- Keep the `# Source:` line above each key as it is. It is the English text your translation was made from, and it is how the plugin knows when that text changes later.
- Keep every `{placeholders}` exactly as written in the source, such as `{command}` or `{mod}`. You can move them anywhere in the sentence, but do not translate or rename them.
- `\n` is a line break. Keep the same number of them as the source when you can.
- A plural key ends in `.one` or `.other`. Your language can use the forms it needs: `.zero`, `.one`, `.two`, `.few`, `.many`, `.other`. A language without plurals, such as Japanese, only needs `.other`.
- Credit yourself with a line such as `@credits = Your name`. Several names are separated by commas. Credits are shown in the language settings.
- Your own comments (lines starting with `#`) are kept.

## Updating an existing translation

When an English text changes, the translation made from the old one is marked as outdated in the translation editor. It still shows in the plugin until you update it. If it is still correct, click **Still correct**.

To improve a translation, edit its file directly, or load it in the translation editor and save it again.

## Sending a translation

Open a pull request that adds or changes the `.lang` file only. No issue is needed for translations.

# Code changes

1. **Open an issue first.** Describe the bug or the change you have in mind, and why. Wait for my answer before writing any code: I might already be working on it, or it might not fit the plugin.
2. Once the issue is approved, fork the repository, make the change on its own branch, and open a pull request that links the issue.
3. Keep the pull request focused on that one change. No unrelated reformatting or renaming.
4. Try to match the existing code style.
5. Every user-facing text goes through the localization system (the `L` classes in `BypassEmote/Localization/Texts`), never as a hard-coded string. Write it in English, and leave the other languages to their translators.
6. Make sure the plugin builds, and test your change(s) in game.

# Bug reports

Bugs can be reported in an issue, or on the Discord server (the button in the main window). Please include:

- what you did, what you expected, and what happened instead;
- your game client (Global, Chinese, Korean) and your bypass mode (Emote Swap or Direct Play);
- the zip exported by `/belogs`. It contains personal information: send it to me PRIVATELY.
