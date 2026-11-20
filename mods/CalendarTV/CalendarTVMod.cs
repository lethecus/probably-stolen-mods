using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using HarmonyLib;
using Steamworks;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CalendarTV
{
	/// <summary>
	/// Personal mod. Shows a YouTube playlist on the shop's wall calendar sign instead of the date,
	/// toggled by a hotkey. The video fills the sign; a click skips to the next track; the game's own
	/// hotkeys are suppressed while the player is on screen; the cassette pauses while the video runs
	/// and resumes afterwards.
	///
	/// Playback uses Steam's HTML Surface (Chromium), so YouTube, cookies and audio behave like a
	/// browser tab. The toggle key is stored in this mod's config (not the save file) and can also be
	/// changed from Options -> Calendar TV, which captures any key.
	/// </summary>
	public sealed class CalendarTVMod : IMod
	{
		internal const string ModId = "calendar_tv";

		internal static ModLog Log;

		private const KeyCode DefaultToggleKey = KeyCode.I;

		private const string BindingId = "calendar_tv_toggle";

		private const string NextBindingId = "calendar_tv_next";

		private const KeyCode DefaultNextKey = KeyCode.O;

		// Keybinds are stored in this mod's own data folder as a small JSON file. They used to live in ModConfig
		// entries, but the ModConfig window keeps its own text buffer and applying it could write stale keys over
		// a rebind made in Options. Options -> Calendar TV is now the only place to set them.
		private const string KeyFile = "keys.json";

		private static KeyCode nextKey = DefaultNextKey;

		private static int lastNextFrame = -1;

		private static ConfigEntry<string> playlistSetting;

		private static bool shuffle;

		private static ModConfig config;

		private static KeyCode toggleKey = DefaultToggleKey;

		private static Harmony harmony;

		private static bool browserReady;

		private static bool browserCreating;

		private static HHTMLBrowser browser;

		private static CallResult<HTML_BrowserReady_t> createdResult;

		private static Callback<HTML_NeedsPaint_t> paintCallback;

		private static Callback<HTML_StartRequest_t> startCallback;

		private static Callback<HTML_FinishedRequest_t> finishedCallback;

		private static Callback<HTML_ChangedTitle_t> titleCallback;

		private static bool driverRunning;

		private static Texture2D browserTexture;

		private static RawImage view;

		private static GameObject maskObject;

		private static Sprite maskSprite;

		private static StoreCalendar sign;

		private static TextMeshProUGUI dayText;

		private static bool active;

		private static int lastToggleFrame = -1;

		private static bool musicPaused;

		private static GameItem cassetteMachine;

		private static bool cassetteWasPlaying;

		internal static bool Active
		{
			get
			{
				return active;
			}
		}

		public void Init(ModManifest manifest)
		{
			Log = new ModLog(manifest);
		}

		public void OnEnable()
		{
			try
			{
				config = ModConfig.For(ModId);
				playlistSetting = config.Define("playlist", "", Loc.Get("cfg.section.playlist"), () => Loc.Get("cfg.playlist.label"), null, () => Loc.Get("cfg.playlist.tip"), false);
				// toggle_key / next_key / shuffle are deliberately NOT defined here: the game's Options registry has
				// no text control, so the playlist URL is the only setting that still needs the mod settings page.
				// Keys and shuffle live in Options -> Calendar TV and are persisted in this mod's own data folder.
				// The old "loop" entry was removed: it was never read anywhere, so it did nothing.
			}
			catch (Exception ex)
			{
				Log?.Warning("Could not register the settings: " + ex.Message);
			}

			LoadKeys();
			EnsureBinding();
			RegisterOptions();

			harmony = new Harmony("mod.calendar_tv");
			harmony.PatchAll(typeof(CalendarTVMod).Assembly);
			ModHook.OnGameLoadedLate += OnGameLoaded;
			SceneManager.sceneLoaded += OnSceneLoaded;
			SceneManager.activeSceneChanged += OnActiveSceneChanged;

			Log?.Log("Calendar TV enabled; toggle key " + toggleKey + ".");
		}

		public void OnDisable()
		{
			try
			{
				if (active)
				{
					SetActive(false);
				}
			}
			catch
			{
			}

			ModHook.OnGameLoadedLate -= OnGameLoaded;
			try
			{
				SceneManager.sceneLoaded -= OnSceneLoaded;
				SceneManager.activeSceneChanged -= OnActiveSceneChanged;
			}
			catch
			{
			}
			try
			{
				GameOptionRegistry.RemoveBySource("calendar_tv");
			}
			catch
			{
			}

			if (harmony != null)
			{
				harmony.UnpatchAll("mod.calendar_tv");
				harmony = null;
			}
		}

		// ------------------------------------------------------------- keybind

		/// <summary>
		/// Settings are stored as plain "key=value" lines in this mod's own data folder. ModConfig is deliberately
		/// not used for keys or shuffle any more: its window keeps a text buffer, and applying stale text could
		/// overwrite a rebind made in Options. Options -> Calendar TV is the only place to change these.
		/// </summary>
		private static void LoadKeys()
		{
			try
			{
				if (!ModHelper.ModDataFileExists(ModId, KeyFile))
				{
					// First run after the settings moved out of ModConfig: adopt what the game's binder already
					// shows (that is what the Options menu displays), then persist it.
					KeyCode boundToggle = KeyBindHelper.Get(BindingId);
					KeyCode boundNext = KeyBindHelper.Get(NextBindingId);
					if (boundToggle != KeyCode.None)
					{
						toggleKey = boundToggle;
					}
					if (boundNext != KeyCode.None)
					{
						nextKey = boundNext;
					}
					SaveKeys();
					Log?.Log("Created the settings file: toggle " + toggleKey + ", next " + nextKey + ".");
					return;
				}
				string raw = ModHelper.ReadModDataFileText(ModId, KeyFile);
				if (string.IsNullOrEmpty(raw))
				{
					return;
				}
				string[] lines = raw.Split('\n');
				for (int i = 0; i < lines.Length; i++)
				{
					string line = lines[i].Trim();
					int split = line.IndexOf('=');
					if (split <= 0)
					{
						continue;
					}
					string key = line.Substring(0, split).Trim();
					string value = line.Substring(split + 1).Trim();
					KeyCode parsed;
					if (key == "toggle" && Enum.TryParse(value, true, out parsed))
					{
						toggleKey = parsed;
					}
					else if (key == "next" && Enum.TryParse(value, true, out parsed))
					{
						nextKey = parsed;
					}
					else if (key == "shuffle")
					{
						bool flag;
						if (bool.TryParse(value, out flag))
						{
							shuffle = flag;
						}
					}
				}
			}
			catch (Exception ex)
			{
				Log?.Warning("Could not read the key file: " + ex.Message);
			}
		}

		private static void SaveKeys()
		{
			try
			{
				StringBuilder text = new StringBuilder();
				text.AppendLine("# Calendar TV settings");
				text.AppendLine("toggle=" + toggleKey);
				text.AppendLine("next=" + nextKey);
				text.AppendLine("shuffle=" + (shuffle ? "true" : "false"));
				ModHelper.WriteModDataFileText(ModId, KeyFile, text.ToString());
			}
			catch (Exception ex)
			{
				Log?.Warning("Could not write the key file: " + ex.Message);
			}
		}

		private static void EnsureBinding()
		{
			try
			{
				if (KeyBindHelper.Find(BindingId) == null)
				{
					KeyBindHelper.Bindings.Add(new KeyBindHelper.Binding
					{
						id = BindingId,
						defaultKey = DefaultToggleKey,
						label = () => "Calendar TV: wall video"
					});
				}
				if (KeyBindHelper.Find(NextBindingId) == null)
				{
					KeyBindHelper.Bindings.Add(new KeyBindHelper.Binding
					{
						id = NextBindingId,
						defaultKey = DefaultNextKey,
						label = () => "Calendar TV: next track"
					});
				}
				// Push the persisted keys into the binder so the Options menu shows what the mod will actually
				// use. The binder's own ES3 storage does not survive a restart for mod additions, the config
				// file does, so the config is the source of truth and the binder is the display.
				KeyBindHelper.Set(BindingId, toggleKey);
				KeyBindHelper.Set(NextBindingId, nextKey);
			}
			catch (Exception ex)
			{
				Log?.Warning("Could not add the toggle binding: " + ex.Message);
			}
		}

		/// <summary>Native Options entry: click the key button, then press any key.</summary>
		private static void RegisterOptions()
		{
			try
			{
				GameOptionRegistry.GetOrAddSection("calendar_tv", () => "Calendar TV", "calendar_tv");
				GameOptionRegistry.Entry entry = GameOptionRegistry.KeyBind(
					"calendar_tv_toggle_key",
					() => "Toggle wall video",
					() => toggleKey,
					SetToggleKey);
				entry.source = "calendar_tv";
				GameOptionRegistry.Add("calendar_tv", entry);

				GameOptionRegistry.Entry next = GameOptionRegistry.KeyBind(
					"calendar_tv_next_key",
					() => "Next track",
					() => nextKey,
					SetNextKey);
				next.source = "calendar_tv";
				GameOptionRegistry.Add("calendar_tv", next);

				GameOptionRegistry.Entry randomize = GameOptionRegistry.Toggle(
					"calendar_tv_shuffle",
					() => "Randomize playlist",
					() => shuffle,
					SetShuffle,
					() => "Start at a random row and keep shuffling: every next track is picked at random instead of following the list.");
				randomize.source = "calendar_tv";
				GameOptionRegistry.Add("calendar_tv", randomize);
			}
			catch (Exception ex)
			{
				Log?.Warning("Could not register the Options keybind: " + ex.Message);
			}
		}

		private static void SetNextKey(KeyCode key)
		{
			try
			{
				nextKey = key;
				KeyBindHelper.Set(NextBindingId, key);
				KeyBindHelper.Save();
				SaveKeys();
				Log?.Log("Next track key bound to " + key + ".");
			}
			catch (Exception ex)
			{
				Log?.Warning("Could not store the next track key: " + ex.Message);
			}
		}

		private static void SetToggleKey(KeyCode key)
		{
			try
			{
				toggleKey = key;
				KeyBindHelper.Set(BindingId, key);
				KeyBindHelper.Save();
				SaveKeys();
				Log?.Log("Toggle key bound to " + key + ".");
			}
			catch (Exception ex)
			{
				Log?.Warning("Could not store the toggle key: " + ex.Message);
			}
		}

		private static void SetShuffle(bool value)
		{
			try
			{
				shuffle = value;
				SaveKeys();
				Log?.Log("Randomize " + (value ? "on" : "off") + ".");
			}
			catch (Exception ex)
			{
				Log?.Warning("Could not store Randomize: " + ex.Message);
			}
		}

		/// <summary>
		/// Leaving to the main menu destroys the shop, so nothing ticks and the hotkey stops working -
		/// without this the browser would keep playing after the shop is gone.
		/// </summary>
		private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
		{
			try
			{
				if (active)
				{
					SetActive(false);
				}
				else if (browserReady)
				{
					SteamHTMLSurface.ExecuteJavascript(browser, StopScript);
					SteamHTMLSurface.LoadURL(browser, "about:blank", null);
				}
				sign = null;
				dayText = null;
				view = null;
				Log?.Log("Scene '" + scene.name + "': wall video stopped.");
			}
			catch (Exception ex)
			{
				Log?.Warning("Could not stop the wall video on the scene change: " + ex.Message);
			}
		}

		private static void OnActiveSceneChanged(Scene previous, Scene next)
		{
			OnSceneLoaded(next, LoadSceneMode.Single);
		}

		private static void OnGameLoaded()
		{
			sign = UnityEngine.Object.FindObjectOfType<StoreCalendar>();
			dayText = sign != null ? sign.dayTMP : null;
			if (sign != null)
			{
				Log?.Log("Found the wall calendar sign.");
				LogHierarchy();
				LogSignGeometry();
			}
		}

		private static void LogHierarchy()
		{
			try
			{
				Transform node = dayText != null ? dayText.transform : null;
				for (int depth = 0; depth < 4 && node != null; depth++)
				{
					StringBuilder siblings = new StringBuilder();
					Transform parent = node.parent;
					if (parent != null)
					{
						for (int i = 0; i < parent.childCount && i < 12; i++)
						{
							Transform sibling = parent.GetChild(i);
							siblings.Append(i).Append(':').Append(sibling.name).Append("  ");
						}
					}
					Log?.Log("sign node [" + depth + "] '" + node.name + "' idx=" + node.GetSiblingIndex() + " children=" + node.childCount + " | siblings: " + siblings);
					node = parent;
				}
			}
			catch (Exception ex)
			{
				Log?.Warning("Could not dump the sign hierarchy: " + ex.Message);
			}
		}

		/// <summary>
		/// One-off geometry dump: where the date text sits in world space and which renderers make up
		/// the calendar around it, so the video overlay can be sized to the screen instead of the text.
		/// </summary>
		private static void LogSignGeometry()
		{
			try
			{
				if (dayText == null)
				{
					return;
				}
				RectTransform rect = dayText.rectTransform;
				Vector3[] corners = new Vector3[4];
				rect.GetWorldCorners(corners);
				Log?.Log("day text rect=" + rect.rect.size + " world=" + corners[0] + " .. " + corners[2]);

				Canvas canvas = dayText.GetComponentInParent<Canvas>();
				if (canvas != null)
				{
					Log?.Log("canvas '" + canvas.name + "' mode=" + canvas.renderMode + " order=" + canvas.sortingOrder + " layer=" + canvas.sortingLayerName + " scale=" + canvas.transform.lossyScale);
					Transform root = canvas.rootCanvas != null ? canvas.rootCanvas.transform : canvas.transform;
					Log?.Log("canvas root '" + root.name + "' pos=" + root.position + " scale=" + root.lossyScale + " parent='" + (root.parent != null ? root.parent.name : "-") + "'");
				}

				Vector3 centre = rect.TransformPoint(rect.rect.center);
				Renderer[] renderers = UnityEngine.Object.FindObjectsOfType<Renderer>();
				List<Renderer> near = new List<Renderer>();
				List<float> distances = new List<float>();
				foreach (Renderer renderer in renderers)
				{
					if (renderer == null || !renderer.enabled)
					{
						continue;
					}
					float distance = Vector3.Distance(renderer.bounds.center, centre);
					if (distance > 12f)
					{
						continue;
					}
					int at = near.Count;
					for (int i = 0; i < distances.Count; i++)
					{
						if (distance < distances[i])
						{
							at = i;
							break;
						}
					}
					near.Insert(at, renderer);
					distances.Insert(at, distance);
				}
				Log?.Log("renderers near the date text: " + near.Count);
				for (int i = 0; i < near.Count && i < 15; i++)
				{
					Renderer renderer = near[i];
					SpriteRenderer sprite = renderer as SpriteRenderer;
					string extra = sprite != null ? (" order=" + sprite.sortingOrder + "/" + sprite.sortingLayerName) : "";
					Log?.Log("  [" + i + "] '" + renderer.name + "' d=" + distances[i].ToString("0.00") + " centre=" + renderer.bounds.center + " size=" + renderer.bounds.size + extra + " contains=" + renderer.bounds.Contains(centre));
				}
			}
			catch (Exception ex)
			{
				Log?.Warning("Could not dump the sign geometry: " + ex.Message);
			}
		}

		// ---------------------------------------------------------------- toggle

		internal static void Tick()
		{
			try
			{
				if (sign == null)
				{
					OnGameLoaded();
				}
				if (Input.GetKeyDown(toggleKey) && lastToggleFrame != UnityEngine.Time.frameCount)
				{
					lastToggleFrame = UnityEngine.Time.frameCount;
					SetActive(!active);
				}
				if (active && nextKey != toggleKey && Input.GetKeyDown(nextKey) && lastNextFrame != UnityEngine.Time.frameCount)
				{
					lastNextFrame = UnityEngine.Time.frameCount;
					NextTrack();
				}
			}
			catch (Exception ex)
			{
				Log?.Warning("Calendar TV tick failed: " + ex.Message);
			}
		}

		internal static void SetActive(bool on)
		{
			if (on == active)
			{
				return;
			}
			active = on;

			if (on)
			{
				EnsureView();
				EnsureBrowser();
				if (view != null)
				{
					view.gameObject.SetActive(true);
				}
				if (dayText != null)
				{
					dayText.enabled = false;
				}
				PauseMusic();

				if (browserReady)
				{
					SteamHTMLSurface.SetKeyFocus(browser, false); // never take keyboard/mouse focus: Steam routes input away from the game while a surface has it
					LoadCurrentUrl();
				}
			}
			else
			{
				if (view != null)
				{
					view.gameObject.SetActive(false);
				}
				if (dayText != null)
				{
					dayText.enabled = true;
				}
				ResumeMusic();
				if (browserReady)
				{
					// Stop the audio for real: pausing is not enough, the page keeps a live media element.
					try
					{
						SteamHTMLSurface.ExecuteJavascript(browser, StopScript);
						SteamHTMLSurface.LoadURL(browser, "about:blank", null);
					}
					catch (Exception ex)
					{
						Log?.Warning("Could not stop the video: " + ex.Message);
					}
					SteamHTMLSurface.SetKeyFocus(browser, false);
				}
			}

			Log?.Log(on ? "Wall screen: video on." : "Wall screen: video off.");

		}

		private static void EnsureView()
		{
			if (view != null || dayText == null)
			{
				return;
			}
			try
			{
				// The calendar face is a rounded rectangle; the video is square, so a rounded stencil mask
				// keeps its corners from poking over the bezel.
				GameObject maskGo = new GameObject("CalendarTVMask", typeof(RectTransform), typeof(Image), typeof(Mask));
				maskObject = maskGo;
				maskGo.transform.SetParent(dayText.transform, false);
				maskGo.transform.SetSiblingIndex(dayText.transform.GetSiblingIndex() + 1);
				RectTransform maskRect = maskGo.GetComponent<RectTransform>();
				maskRect.anchorMin = Vector2.zero;
				maskRect.anchorMax = Vector2.one;
				maskRect.offsetMin = new Vector2(-18f, -12.5f);
				maskRect.offsetMax = new Vector2(12.8f, 13.2f);
				maskRect.localScale = Vector3.one;
				Image maskImage = maskGo.GetComponent<Image>();
				maskImage.sprite = MaskSprite();
				maskImage.type = Image.Type.Sliced;
				maskImage.raycastTarget = false;
				Mask mask = maskGo.GetComponent<Mask>();
				mask.showMaskGraphic = false;

				GameObject go = new GameObject("CalendarTVView", typeof(RectTransform), typeof(RawImage));
				go.transform.SetParent(maskGo.transform, false);
				RectTransform rect = go.GetComponent<RectTransform>();
				rect.anchorMin = Vector2.zero;
				rect.anchorMax = Vector2.one;
				rect.offsetMin = Vector2.zero;
				rect.offsetMax = Vector2.zero;
				rect.localScale = Vector3.one;
				view = go.GetComponent<RawImage>();
				view.color = Color.white;
				view.raycastTarget = false;
				view.enabled = false;
			}
			catch (Exception ex)
			{
				Log?.Warning("Could not build the wall screen overlay: " + ex.Message);
			}
		}

		/// <summary>Rounded rectangle sprite used as the stencil for the video overlay.</summary>
		private static Sprite MaskSprite()
		{
			if (maskSprite != null)
			{
				return maskSprite;
			}
			const int size = 32;
			const int radius = 10;
			Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
			texture.name = "CalendarTV.Mask";
			Color32[] pixels = new Color32[size * size];
			for (int y = 0; y < size; y++)
			{
				for (int x = 0; x < size; x++)
				{
					float dx = 0f;
					float dy = 0f;
					if (x < radius)
					{
						dx = radius - (x + 0.5f);
					}
					else if (x >= size - radius)
					{
						dx = (x + 0.5f) - (size - radius);
					}
					if (y < radius)
					{
						dy = radius - (y + 0.5f);
					}
					else if (y >= size - radius)
					{
						dy = (y + 0.5f) - (size - radius);
					}
					bool inside = dx * dx + dy * dy <= radius * radius;
					pixels[y * size + x] = new Color32(255, 255, 255, inside ? (byte)255 : (byte)0);
				}
			}
			texture.SetPixels32(pixels);
			texture.filterMode = FilterMode.Bilinear;
			texture.wrapMode = TextureWrapMode.Clamp;
			texture.Apply(false, false);
			maskSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f, 0u, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
			maskSprite.name = "CalendarTV.MaskSprite";
			return maskSprite;
		}

		private static void FitTexture()
		{
			if (view == null || browserTexture == null)
			{
				return;
			}
			Rect area = view.rectTransform.rect;
			if (area.width <= 0f || area.height <= 0f)
			{
				return;
			}
			float textureAspect = (float)browserTexture.width / browserTexture.height;
			float areaAspect = area.width / area.height;
			float x = 0f;
			float y = 0f;
			float w = 1f;
			float h = 1f;
			if (areaAspect > textureAspect)
			{
				h = textureAspect / areaAspect;
				y = (1f - h) * 0.5f;
			}
			else
			{
				w = areaAspect / textureAspect;
				x = (1f - w) * 0.5f;
			}
			// The HTML surface gives BGRA rows top-down, so the image must be flipped vertically.
			view.uvRect = new Rect(x, 1f - (y + h), w, -h);
		}

		// -------------------------------------------------------------- browser

		/// <summary>
		/// All browsing happens inside Steam's HTML surface, so the mod does not need the network
		/// capability: the playlist is opened as a normal web page and the injected script clicks the
		/// wanted row. YouTube's embed player is refused when it is opened as a top level page (no
		/// referrer), showing "Video player configuration error", so the watch page is used instead.
		/// </summary>
		private const string PlaybackScript = @"(function () {
	var say = window.__ctSay;
	if (!say) {
		say = function (t) {
			try {
				if (window.__ctSayTimer) { clearTimeout(window.__ctSayTimer); if (window.__ctTitle !== null) { document.title = window.__ctTitle; } }
				window.__ctTitle = document.title;
				document.title = 'CT:' + t;
				window.__ctSayTimer = setTimeout(function () {
					try { if (window.__ctTitle !== null) { document.title = window.__ctTitle; } } catch (e) {}
					window.__ctTitle = null;
					window.__ctSayTimer = 0;
				}, 120);
			} catch (e) {}
		};
		window.__ctSay = say;
	}
	if (!window.__ctStyle) {
		window.__ctStyle = function () {
			if (document.getElementById('__ctCss')) { return; }
			var s = document.createElement('style');
			s.id = '__ctCss';
			s.textContent = 'html,body{margin:0!important;padding:0!important;overflow:hidden!important;background:#000!important}' +
				'#masthead-container,#secondary,ytd-watch-metadata,#below,#comments,ytd-playlist-panel-renderer,#related,ytd-merch-shelf-renderer,ytd-consent-bump-v2-lightbox,ytd-popup-container,tp-yt-app-drawer,#chat,#survey,#ticker,tp-yt-iron-overlay-backdrop{display:none!important;visibility:hidden!important}' +
				'#movie_player{position:fixed!important;top:0!important;left:0!important;width:100vw!important;height:100vh!important;max-width:none!important;max-height:none!important;margin:0!important;padding:0!important;border:0!important;z-index:2147483000!important}' +
				'#player-container-outer,#player-container,#player,ytd-player{max-width:none!important;max-height:none!important;margin:0!important;padding:0!important}' +
				'.ytp-chrome-bottom,.ytp-gradient-bottom,.ytp-pause-overlay,.ytp-ce-element,.ytp-endscreen-content{opacity:0!important;pointer-events:none!important}';
			(document.head || document.documentElement).appendChild(s);
		};
	}
	if (!window.__ctHook) {
		window.__ctHook = 1;
		try {
			var ms = !!window.MediaSource;
			say('env mse=' + ms + ' h264=' + (ms ? MediaSource.isTypeSupported('video/mp4; codecs=\u0022avc1.64001f,mp4a.40.2\u0022') : 'n/a') + ' ua=' + navigator.userAgent.slice(0, 24));
		} catch (e) { say('env unknown'); }
	}
	if (!window.__ctTry) {
		window.__ctTry = function () {
			var v = document.querySelector('video');
			// Age-restricted or otherwise blocked tracks never fire ended, so skip them after a stall.
			var now = v ? v.currentTime : -1;
			if (v && !v.paused && now > (window.__ctLastTime || 0)) { window.__ctStall = 0; } else { window.__ctStall = (window.__ctStall || 0) + 1; }
			window.__ctLastTime = now;
			var errored = false;
			try { errored = !!document.querySelector('.ytp-error'); } catch (e) {}
			if ((errored && window.__ctStall > 6) || window.__ctStall > 30) {
				window.__ctStall = 0;
				say('skipping a track that will not play' + (errored ? ' (player error)' : ''));
				try { var skip = document.querySelector('.ytp-next-button'); if (skip) { skip.click(); return true; } } catch (e) {}
				try { var pl2 = document.getElementById('movie_player'); if (pl2 && pl2.nextVideo) { pl2.nextVideo(); return true; } } catch (e) {}
			}
			if (!v) { return false; }
			if (!v.__ctHooked) {
				v.__ctHooked = 1;
				v.addEventListener('error', function () { say('videoerr code=' + (v.error ? v.error.code : '?') + ' msg=' + String((v.error && v.error.message) || '').slice(0, 40)); });
				v.addEventListener('playing', function () {
					if (window.__ctMuted) {
						window.__ctMuted = 0;
						try { v.muted = false; } catch (e) {}
						try { var pl = document.getElementById('movie_player'); if (pl && pl.unMute) { pl.unMute(); } } catch (e) {}
						say('unmuted');
					}
					say('playing');
				});
				v.addEventListener('ended', function () { say('ended'); try { var n = document.querySelector('.ytp-next-button'); if (n) { n.click(); } } catch (e) {} });
			}
			if (!v.paused) { return true; }
			try { var p = document.getElementById('movie_player'); if (p && p.playVideo) { p.playVideo(); } } catch (e) {}
			if (v.paused) {
				var attempt = null;
				try { attempt = v.play(); } catch (e) {}
				if (attempt && attempt.catch) {
					attempt.catch(function () {
						try { v.muted = true; window.__ctMuted = 1; } catch (e) {}
						try { var again = v.play(); if (again && again.catch) { again.catch(function () { say('muted autoplay refused'); }); } } catch (e) {}
					});
				}
			}
			return !v.paused;
		};
	}
	window.__ctStyle();
	try {
		if (!window.__ctNav && location.pathname.indexOf('/playlist') === 0) {
			var anchors = document.querySelectorAll('a[href*=\u0022/watch?v=\u0022]');
			var seen = {};
			var urls = [];
			for (var i = 0; i < anchors.length && urls.length < 300; i++) {
				var href = anchors[i].getAttribute('href') || '';
				var found = /\/watch\?v=([A-Za-z0-9_-]{11})/.exec(href);
				if (!found || seen[found[1]]) { continue; }
				seen[found[1]] = 1;
				var listMatch = /[?&]list=([A-Za-z0-9_-]+)/.exec(href);
				var listId = listMatch ? listMatch[1] : (window.__ctList || '');
				urls.push('/watch?v=' + found[1] + '&list=' + listId + '&index=' + (urls.length + 1));
			}
			if (urls.length === 0) {
				window.__ctEmpty = (window.__ctEmpty || 0) + 1;
				if (window.__ctEmpty === 4) { say('playlist page shows no videos'); }
			}
			if (urls.length > 0) {
				window.__ctNav = 1;
				var pick = Math.max(0, Math.min(window.__ctPick || 0, urls.length - 1));
				say('playlist page: ' + urls.length + ' rows, opening row ' + (pick + 1));
				location.href = urls[pick];
				return;
			}
		}
	} catch (e) {}
	// Randomize: turn on YouTube's own shuffle once per page, so both the Next key and the
// end-of-track autoplay follow a shuffled order instead of the playlist order.
window.__ctShuffleOn = function () {
	try {
		if (!window.__ctShuffle || window.__ctShuffleDone) { return; }
		var b = document.querySelector('.ytp-shuffle-button');
		if (!b) { return; }
		if (b.getAttribute('aria-checked') !== 'true') { b.click(); }
		window.__ctShuffleDone = 1;
	} catch (e) {}
};
window.__ctShuffleOn();
window.__ctTry();
	if (!window.__ctTicker) {
		window.__ctTicker = setInterval(function () { window.__ctStyle(); window.__ctShuffleOn(); window.__ctTry(); }, 500);
	}
})();";

		private const string NextScript = @"(function(){
	try { var p = document.getElementById('movie_player'); if (p && p.nextVideo) { p.nextVideo(); return; } } catch (e) {}
	try { var b = document.querySelector('.ytp-next-button'); if (b) { b.click(); } } catch (e) {}
})();";

		private const string StopScript = @"(function(){
	try { var p = document.getElementById('movie_player'); if (p && p.pauseVideo) { p.pauseVideo(); } } catch (e) {}
	try { var v = document.querySelector('video'); if (v) { v.pause(); v.muted = true; } } catch (e) {}
	try { document.querySelectorAll('video,audio').forEach(function (m) { try { m.pause(); m.muted = true; } catch (e) {} }); } catch (e) {}
})();";

		private static int pickIndex;

		private static string BuildScript()
		{
			string list = GetPlaylistId().Replace("'", "");
			return "window.__ctPick=" + pickIndex + ";window.__ctList='" + list + "';window.__ctShuffle=" +
				(shuffle ? "1" : "0") + ";" + PlaybackScript;
		}

		private static string ExtractListId(string url)
		{
			if (string.IsNullOrEmpty(url))
			{
				return "";
			}
			int marker = url.IndexOf("list=", StringComparison.OrdinalIgnoreCase);
			if (marker < 0)
			{
				return "";
			}
			string list = url.Substring(marker + 5);
			int end = list.IndexOf('&');
			if (end >= 0)
			{
				list = list.Substring(0, end);
			}
			return list.Trim();
		}

		private static string GetPlaylistId()
		{
			string value = playlistSetting != null ? (playlistSetting.Value ?? "").Trim() : "";
			if (value.Length == 0)
			{
				return "";
			}
			int marker = value.IndexOf("list=", StringComparison.OrdinalIgnoreCase);
			if (marker < 0)
			{
				return value;
			}
			string list = value.Substring(marker + 5);
			int end = list.IndexOf('&');
			if (end >= 0)
			{
				list = list.Substring(0, end);
			}
			return list.Trim();
		}

		/// <summary>
		/// Watch/mix URLs (watch?v=...&amp;list=RD...) are loaded as they are: mixes have no usable
		/// playlist page - YouTube answers "This playlist type is unviewable" there - but the watch page
		/// carries the radio queue fine.
		/// </summary>
		private static string GetWatchUrl()
		{
			string value = playlistSetting != null ? (playlistSetting.Value ?? "").Trim() : "";
			if (value.Length == 0)
			{
				return null;
			}
			if (value.IndexOf("watch?", StringComparison.OrdinalIgnoreCase) < 0)
			{
				return null;
			}
			if (value.IndexOf("v=", StringComparison.OrdinalIgnoreCase) < 0)
			{
				return null;
			}
			return value.StartsWith("http", StringComparison.OrdinalIgnoreCase)
				? value
				: "https://www.youtube.com/" + value.TrimStart('/');
		}

		/// <summary>A mix ("radio") is a watch URL with list=RD... / start_radio=1; it has no playlist page.</summary>
		private static bool LooksLikeRadio(string url)
		{
			if (string.IsNullOrEmpty(url))
			{
				return false;
			}
			if (url.IndexOf("start_radio=1", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				return true;
			}
			int marker = url.IndexOf("list=", StringComparison.OrdinalIgnoreCase);
			if (marker < 0)
			{
				return false;
			}
			return url.Substring(marker + 5).StartsWith("RD", StringComparison.OrdinalIgnoreCase);
		}

		private static void LoadCurrentUrl()
		{
			if (!browserReady)
			{
				return;
			}
			string watch = GetWatchUrl();
			if (!string.IsNullOrEmpty(watch))
			{
				bool radio = LooksLikeRadio(watch);
				string watchList = ExtractListId(watch);
				// A watch URL that points into a normal playlist still honours Randomize: start at a
				// random row of that playlist instead of always the one in the URL.
				if (!radio && watchList.Length > 0 && shuffle)
				{
					pickIndex = UnityEngine.Random.Range(0, 100);
					Log?.Log("Watch URL with playlist " + watchList + " and Randomize on: starting at a random row.");
					LoadUrl("https://www.youtube.com/playlist?list=" + Uri.EscapeDataString(watchList));
					return;
				}
				pickIndex = 0;
				if (radio && shuffle)
				{
					Log?.Log("Mix URL: Randomize is ignored, the radio queue picks its own order.");
				}
				LoadUrl(watch);
				return;
			}
			string list = GetPlaylistId();
			if (list.Length == 0)
			{
				LoadUrl("https://www.youtube.com/");
				return;
			}
			if (list.StartsWith("RD", StringComparison.OrdinalIgnoreCase))
			{
				Log?.Warning("'" + list + "' looks like a mix id. A mix has no playlist page, so paste the whole watch?v=...&list=" + list + "&start_radio=1 URL instead.");
				LoadUrl("https://www.youtube.com/");
				return;
			}
			pickIndex = (shuffle) ? UnityEngine.Random.Range(0, 100) : 0;
			LoadUrl("https://www.youtube.com/playlist?list=" + Uri.EscapeDataString(list));
		}
		private static void LoadUrl(string url)
		{
			try
			{
				Log?.Log("Loading " + url);
				SteamHTMLSurface.LoadURL(browser, url, null);
			}
			catch (Exception ex)
			{
				Log?.Warning("Could not load " + url + ": " + ex.Message);
			}
		}

		/// <summary>Keeps injecting the restyle/play script until the player is running.</summary>
		private static IEnumerator PlaybackDriver()
		{
			if (driverRunning)
			{
				yield break;
			}
			driverRunning = true;
			for (int i = 0; i < 20; i++)
			{
				yield return new WaitForSeconds(1.5f);
				if (!browserReady)
				{
					break;
				}
				try
				{
					SteamHTMLSurface.ExecuteJavascript(browser, BuildScript());
				}
				catch (Exception ex)
				{
					Log?.Warning("Could not run the player script: " + ex.Message);
					break;
				}
			}
			driverRunning = false;
		}

		private static void EnsureBrowser()
		{
			if (browserReady || browserCreating)
			{
				return;
			}
			try
			{
				if (!SteamHTMLSurface.Init())
				{
					Log?.Warning("Steam HTML surface could not initialise (is Steam running?).");
					return;
				}

				paintCallback = Callback<HTML_NeedsPaint_t>.Create(OnPaint);
				startCallback = Callback<HTML_StartRequest_t>.Create(OnStartRequest);
				finishedCallback = Callback<HTML_FinishedRequest_t>.Create(OnFinished);
				titleCallback = Callback<HTML_ChangedTitle_t>.Create(OnTitleChanged);

				browserCreating = true;
				SteamAPICall_t call = SteamHTMLSurface.CreateBrowser(null, null);
				createdResult = CallResult<HTML_BrowserReady_t>.Create(OnBrowserCreated);
				createdResult.Set(call, null);
			}
			catch (Exception ex)
			{
				browserCreating = false;
				Log?.Warning("Could not create the HTML browser: " + ex.Message);
			}
		}

		private static void OnBrowserCreated(HTML_BrowserReady_t result, bool ioFailure)
		{
			browserCreating = false;
			if (ioFailure)
			{
				Log?.Warning("Steam HTML browser creation failed.");
				return;
			}
			browser = result.unBrowserHandle;
			browserReady = true;
			SteamHTMLSurface.SetSize(browser, 1280, 720);
			SteamHTMLSurface.SetKeyFocus(browser, false); // never take keyboard/mouse focus: Steam routes input away from the game while a surface has it
			Log?.Log("Steam HTML browser ready.");
			if (active)
			{
				LoadCurrentUrl();
			}
		}

		private static void OnPaint(HTML_NeedsPaint_t paint)
		{
			try
			{
				if (browserReady && paint.unBrowserHandle != browser)
				{
					return;
				}
				if (paint.pBGRA == IntPtr.Zero)
				{
					return;
				}
				int width = (int)paint.unWide;
				int height = (int)paint.unTall;
				if (width <= 0 || height <= 0)
				{
					return;
				}
				if (browserTexture == null || browserTexture.width != width || browserTexture.height != height)
				{
					if (browserTexture != null)
					{
						UnityEngine.Object.Destroy(browserTexture);
					}
					browserTexture = new Texture2D(width, height, TextureFormat.BGRA32, false);
					browserTexture.name = "CalendarTV.Web";
					if (view != null)
					{
						view.texture = browserTexture;
					}
				}
				browserTexture.LoadRawTextureData(paint.pBGRA, width * height * 4);
				browserTexture.Apply(false);
				if (view != null)
				{
					view.texture = browserTexture;
					view.enabled = active;
					FitTexture();
				}
			}
			catch (Exception ex)
			{
				Log?.Warning("Could not update the browser texture: " + ex.Message);
			}
		}

		private static void OnStartRequest(HTML_StartRequest_t request)
		{
			try
			{
				if (request.unBrowserHandle == browser)
				{
					SteamHTMLSurface.AllowStartRequest(browser, true);
				}
			}
			catch
			{
			}
		}

		private static void OnFinished(HTML_FinishedRequest_t finished)
		{
			try
			{
				if (finished.unBrowserHandle != browser)
				{
					return;
				}
				string url = finished.pchURL ?? "";
				Log?.Log("Page loaded: " + url + " (" + (finished.pchPageTitle ?? "") + ")");
				if (active && (url.Contains("youtube.com/watch") || url.Contains("youtube.com/playlist")))
				{
					ModHelper.StartCoroutine(ModId, PlaybackDriver());
				}
			}
			catch (Exception ex)
			{
				Log?.Warning("Could not handle the page load: " + ex.Message);
			}
		}

		private static void OnTitleChanged(HTML_ChangedTitle_t changed)
		{
			try
			{
				if (changed.unBrowserHandle != browser)
				{
					return;
				}
				string title = changed.pchTitle ?? "";
				if (title.StartsWith("CT:", StringComparison.Ordinal))
				{
					Log?.Log("Player: " + title.Substring(3));
				}
			}
			catch
			{
			}
		}

		internal static void NextTrack()
		{
			if (!browserReady)
			{
				return;
			}
			try
			{
				// Always advance the queue. Reloading the page here restarted a mix from track one.
				SteamHTMLSurface.ExecuteJavascript(browser, NextScript);
			}
			catch (Exception ex)
			{
				Log?.Warning("Could not skip to the next track: " + ex.Message);
			}
		}

		// ----------------------------------------------------------------- audio

		private static void PauseMusic()
		{
			try
			{
				cassetteMachine = EmporiumEntry.Instance != null ? EmporiumEntry.Instance.cassettePlayer : null;
				if (cassetteMachine == null)
				{
					return;
				}
				int state = cassetteMachine.GetTagReadonly("CASETTE_PLAYER_STATE_INT").GetInt();
				if (state == 1)
				{
					MachineCassettePlayer.OnPlayButtonPressed(cassetteMachine, MachineCassettePlayer.GetCassetteInventory());
					cassetteWasPlaying = true;
					Log?.Log("Cassette paused for the video.");
				}
			}
			catch (Exception ex)
			{
				Log?.Warning("Could not pause the cassette player: " + ex.Message);
			}
		}

		private static void ResumeMusic()
		{
			try
			{
				if (!cassetteWasPlaying || cassetteMachine == null)
				{
					return;
				}
				int state = cassetteMachine.GetTagReadonly("CASETTE_PLAYER_STATE_INT").GetInt();
				if (state == 2)
				{
					MachineCassettePlayer.OnPlayButtonPressed(cassetteMachine, MachineCassettePlayer.GetCassetteInventory());
					Log?.Log("Cassette resumed.");
				}
				cassetteWasPlaying = false;
			}
			catch (Exception ex)
			{
				Log?.Warning("Could not resume the cassette player: " + ex.Message);
			}
		}
		// ---------------------------------------------------------------- patches

		internal sealed class ClickCatcher : MonoBehaviour, IPointerClickHandler
		{
			public void OnPointerClick(PointerEventData eventData)
			{
				if (eventData != null && eventData.button == PointerEventData.InputButton.Left)
				{
					NextTrack();
				}
			}
		}

		[HarmonyPatch(typeof(StoreCalendar), "Update")]
		internal static class SignUpdatePatch
		{
			private static void Postfix()
			{
				Tick();
			}
		}

		/// <summary>While the video is on, clicking the sign does not open the calendar window.</summary>
		[HarmonyPatch(typeof(StoreCalendar), "OnClick")]
		internal static class SignClickPatch
		{
			private static bool Prefix()
			{
				return !active;
			}
		}


	}
}
