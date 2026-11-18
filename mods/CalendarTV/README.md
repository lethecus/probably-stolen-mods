# Calendar TV

Heavily inspired by [Frequency TV](https://steamcommunity.com/sharedfiles/filedetails/?id=3836903387).
The difference is simple: this is not a full access system. It is a playlist video player that only sits
on the calendar screen when a hotkey is pressed, and it only plays public, unlisted and mix playlists.

Plays a YouTube playlist on the shop's wall calendar sign instead of the date, so you can keep serving
customers while it runs. A hotkey switches the sign between the calendar and the video; the video fills
the whole yellow screen and the page chrome (masthead, comments, playlist panel) is hidden.

Press the next track key (default `O`) to skip to the next track; clicking the sign does not open the calendar window while the video is on. While the player is on, the game's hotkeys are suppressed, so
typing in the player does not trigger them. The cassette player pauses while the video runs and resumes
when it is switched off.

Want to help make the mod better? Report issues at https://tally.so/r/0V85KB so we can keep it working for you.

https://github.com/lethecus/probably-stolen-mods
The source for all my mods if you wish to confirm nothing shady going on.

- Mod ID: `calendar_tv`
- Version: 1.0.7
- Author: Lethecus
- Game version: 1.0.0.14

## Settings

| Setting | Default | Notes |
| --- | --- | --- |
| Playlist -> Playlist | empty | Playlist URL (`https://www.youtube.com/playlist?list=...`), just the list id, or a watch/mix URL (`https://www.youtube.com/watch?v=...&list=RD...&start_radio=1`) |
| Playlist -> Loop playlist | on | Reserved for the queue handling |
| Playlist -> Randomize | off | Start at one of the first 100 rows of the playlist (the part YouTube renders before lazy loading) each time the video is turned on. The skip key always advances in order |
| Controls -> Toggle key | `I` | Fallback list; the native binder in Options -> Calendar TV captures any key |
| Controls -> Next track key | `O` | Skips to the next playlist item while the video is on |

The toggle key is stored in `ModConfigs/calendar_tv.json`, so it survives a reload that is not saved.
The native Options entry (Options -> Calendar TV) captures whatever key you press.

## How playback works

The sign draws a Steam HTML Surface (Chromium), the same technology as Frequency TV. YouTube's embed
player refuses to start when it is opened as a top level page - it shows *Video player configuration
error* (Error 153), because the request has no referrer. So the mod opens the normal playlist page and
lets an injected script click the wanted row, which YouTube turns into a watch page carrying the whole
queue. All of that browsing happens inside Steam's browser; the mod itself makes no .NET network calls.
A small stylesheet and script then

- hide every page element except the player and pin the player to the full viewport,
- start playback (muted first if the browser blocks audible autoplay, then unmute),
- report state through `document.title` (`CT: playing`, `CT: videoerr ...`), which the mod logs,
- skip to the next playlist item when the current one ends, and also skip a track that will not play
  (age restricted, removed, or the player shows an error) after a short stall.

The next track key (default `O`) runs `#movie_player.nextVideo()`, so it advances the queue; the sign's own click is blocked while the video is on so the calendar window does not pop up.

## Capabilities

`steam` (HTML surface) and `network` (the pages shown come from youtube.com). The mod uses no .NET
networking APIs; the internet access is declared because that is what the sign ends up displaying.

Volume is controlled by the video site's own player: if the sign is silent, check the Windows Volume
Mixer - 'Steam Client WebHelper' may be muted or low. The game's volume slider does not affect it.

## Install

Copy this folder into `<game>\Mods\CalendarTV`, activate the mod in the game's Mods panel and restart.
The folder needs `manifest.xml` and `CalendarTV.dll`.

## Build

```powershell
dotnet build CalendarTV.csproj -c Release
```

The project reads `$(GameDir)` from `ProbablyStolenMods\Directory.Build.props`.

## Mixes and single videos

A `watch?v=...&list=...` URL (including a YouTube mix, `list=RD...`, and a plain single video) is loaded
as it is. Mixes have no usable playlist page - YouTube answers "This playlist type is unviewable" when
the page is opened directly - but the watch page carries the radio queue and keeps handing out the next
track, so `next` and the end-of-track advance both work. Account-specific ids such as `RDMM` (My Mix)
cannot be rebuilt without a Google session.
## Note on input focus (1.0.1)

The mod never calls `SteamHTMLSurface.SetKeyFocus(browser, true)`. A Steam HTML surface that holds
focus makes Steam route mouse input to the browser instead of the game, which in practice blocks clicking
and dragging anything in the shop until the video is switched off. Playback, next-track and the restart
are all driven through `ExecuteJavascript`, so the surface does not need focus at all.
## Note on input (1.0.7)

The mod must not set `KeyBindHelper.SuppressHotkeys`. That flag is read by `InputActionManager` together
with `StoreUIManager.IsAnyTyping()` and it gates **mouse buttons** as well as hotkeys, so leaving it on
while the video plays makes every click and drag in the shop silently fail (hovering, the phone and other
UI events keep working, which makes it look like an invisible wall).
