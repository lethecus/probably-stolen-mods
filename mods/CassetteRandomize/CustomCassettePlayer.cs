using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using TMPro;
using UnityEngine;

namespace CassetteRandomize
{
	internal static class CustomCassettePlayer
	{
		internal static readonly ConditionalWeakTable<GameInventory, GameItem> Decks =
			new ConditionalWeakTable<GameInventory, GameItem>();

		internal static bool ShuffleActive;

		internal static GameItem Create()
		{			GameGridInventory storage = new GameGridInventory(6, 6);
			// 6 columns, 7 visible rows, 90 rows total. The extra visible row
			// at the bottom stays free for dropping new tapes; the scrollbar
			// reaches the remaining rows.
			GameGridScrollableInventory deck = new GameGridScrollableInventory(6, 7, 90, 0.5f);

			MachineryButton previous = MachineryButton.FromAtlas("Window/elements_inner", "previous");
			MachineryButton play = MachineryButton.FromAtlas("Window/elements_inner", "play");
			MachineryButton stop = MachineryButton.FromAtlas("Window/elements_inner", "pause");
			MachineryButton next = MachineryButton.FromAtlas("Window/elements_inner", "next");
			Sprite shuffleIcon = ShuffleIcon();
			MachineryButton randomize;
			if (shuffleIcon != null)
			{
				// Blank face from the transport-button atlas so the dark outer shading
				// matches. Face buttons skip the built-in icon placement, so the icon
				// node is centred by hand afterwards.
				randomize = MachineryButton.FromAtlas("Window/elements_inner", "default");
				randomize.SetIcon(shuffleIcon);
				FieldInfo iconField = typeof(MachineryButton).GetField("iconNode", BindingFlags.Instance | BindingFlags.NonPublic);
				TreeNodeRender node = iconField?.GetValue(randomize) as TreeNodeRender;
				if (node != null)
				{
					MachineryStyle.Place(node, 6, 6, 16, 16);
				}
			}
			else
			{
				randomize = new MachineryButton(Loc.Get("ui.randomize"), null, -1, -1);
			}
			randomize.SetDescription(Loc.Get("ui.randomize.desc"));
			MachineryStack controls = MachineryStack.Horizontal(previous, play, stop, next, randomize);

			MachineryStack volumePlaceholder = new MachineryStack(true, 4);

			PixelWindow window = MachineHelper.CreateMachineryWindow(
				true,
				MachineHelper.Panel(LocHelper.GetLocalizedMechanic("mech_cassette_player_label_storage"), storage, "storage"),
				MachineHelper.Panel(LocHelper.GetLocalizedMechanic("mech_cassette_player_label_cassette"), deck, "cassette"),
				MachineHelper.Panel(LocHelper.GetLocalizedMechanic("mech_cassette_player_label_controls"), controls, "controls"),
				MachineHelper.Panel(LocHelper.GetLocalizedMechanic("mech_cassette_player_label_volume"), volumePlaceholder, "volume"));

			GameItem machine = ItemDirectory.CreateEmptyItem()
				.SetContentWindow(window)
				.SetSpriteAndShape("Items/items_furniture", "cassette_player");
			machine.name = LocHelper.GetLocalizedItem("item_cassette_player_name");
			machine.flavorText = "";
			machine.shortDescription = "";
			machine.EnableTag("IMPORTANT_TAG");
			machine.mayInventoryRemoveItemFunc = (GameItem _, GameInventory _) => false;
			machine.unitValue = 0L;
			machine.EnableTag("CONTAINER_TOOLTIP_TAG");

			Func<GameItem, GameInventory, bool> acceptsCassette =
				(GameItem item, GameInventory inv) => item != null
					&& GeneralHelper.IsItemOwned(item)
					&& item.IsTag("CASSETTE_TAG");
			storage.mayInventoryAddItemFunc = acceptsCassette;
			deck.mayInventoryAddItemFunc = acceptsCassette;

			storage.onSlotAddItemFunc += (GameItem item, GameInventory inv, SlotMarker slot) =>
				AudioManager.Instance.Play3("cassette_insert");
			storage.onSlotRemoveItemFunc += (GameItem item, GameInventory inv, SlotMarker slot) =>
				AudioManager.Instance.Play3("cassette_remove");

			// GameGridScrollableInventory only raises the End callbacks, not the
			// Late ones the stock grid raises.
			deck.onSlotAddItemEndFunc += (GameItem item, GameInventory inv, SlotMarker slot) =>
			{
				AudioManager.Instance.Play3("cassette_insert");
				OnCassetteInventoryChanged(machine, deck);
			};
			deck.onSlotRemoveItemEndFunc += (GameItem item, GameInventory inv, SlotMarker slot) =>
			{
				AudioManager.Instance.Play3("cassette_remove");
				OnCassetteInventoryChanged(machine, deck);
				GameItemElement element = item as GameItemElement;
				if (element != null)
				{
					element.RefreshPlayingFrame();
				}
			};

			previous.SetCallback((Action)(() => Previous(machine, deck)));
			play.SetCallback((Action)(() =>
			{
				// Re-attach the equalizer filter here as well: the load-late call can run before
				// AudioManager.audioSourceMusic exists, and without a filter the spectrum never ticks.
				DeckSettings.ApplyTone(machine);
				Play(machine, deck);
			}));
			stop.SetCallback((Action)(() => Stop(machine)));
			next.SetCallback((Action)(() => Next(machine, deck)));
			randomize.SetCallback((Action)(() =>
			{
				ShuffleActive = true;
				if (!Randomize(machine))
				{
					ShuffleActive = false;
				}
			}));
			// Audio panel replaces the plain volume buttons: it carries the spectrum,
			// seek, volume, tone and preset controls, and takes over the transport row.
			MachineryPanelRow row = window.childElement as MachineryPanelRow;
			PixelElement transport = null;
			MachineryPanel controlsPanel = row?.GetPanel("controls");
			if (controlsPanel != null && controlsPanel.content != null)
			{
				transport = controlsPanel.content;
				controlsPanel.Detach(transport);
				controlsPanel.SetHidden(true);
			}
			DeckUi ui = new DeckUi(machine, transport);
			// Opening the deck is the first reliable moment the music source exists, so make sure the
			// equalizer filter is attached even if nothing has been slid yet.
			DeckSettings.ApplyTone(machine);
			MachineryPanel equalizer = row?.GetPanel("volume");
			if (equalizer != null)
			{
				equalizer.SetLabel(Loc.Get("ui.equalizer"));
				equalizer.SetContent(ui.root);
			}
			MachineryPanel storagePanel = row?.GetPanel("storage");
			if (storagePanel != null)
			{
				new StorageDrawer(machine, row, storagePanel);
			}
			if (row != null)
			{
				row.Validate();
			}
			window.Validate();
			window.FixBounds();

			window.closeCallback = (Action)(() => EmporiumOption.Instance.SaveMusicVolume());
			window.openCallback = (Action)(() =>
			{
				ui.Refresh();
				DeckSettings.ApplyTone(machine);
				window.FixBounds();
			});

			machine.onLoaded = (GameItem loaded) =>
			{
				OnLoaded(loaded, deck);
				ui.Refresh();
				DeckSettings.ApplyTone(loaded);
			};
			DeckSettings.ApplyTone(machine);

			Decks.Remove(deck);
			Decks.Add(deck, machine);
			CassetteRandomizeMod.Log?.Log("Created the scrollable cassette player (6 columns, 7 visible rows, 90 rows total).");
			return machine;
		}

		private static Sprite shuffleIcon;

		// Shuffle glyph shipped with the mod, drawn in the same orange as the
		// vanilla transport icons.
		private static Sprite ShuffleIcon()
		{
			if (shuffleIcon != null)
			{
				return shuffleIcon;
			}
			try
			{
				byte[] bytes = ModHelper.ReadModFileBytes("cassette_randomize", "Textures/shuffle.png");
				if (bytes == null || bytes.Length == 0)
				{
					return null;
				}
				Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
				texture.name = "CassetteRandomizeShuffle";
				texture.filterMode = FilterMode.Point;
				texture.wrapMode = TextureWrapMode.Clamp;
				if (!ImageConversion.LoadImage(texture, bytes))
				{
					UnityEngine.Object.Destroy(texture);
					return null;
				}
				shuffleIcon = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
				return shuffleIcon;
			}
			catch (Exception exception)
			{
				CassetteRandomizeMod.Log?.Warning("Could not load the shuffle icon: " + exception.Message);
				return null;
			}
		}

		internal static GameInventory Deck(GameItem machine)
		{
			return machine == null ? null : MachineHelper.GetBodyStorage(machine, 1) as GameInventory;
		}

		// A save/load rebuilds the item through this factory but then overwrites
		// its tag state, so identify our player by its registered deck instead.
		internal static bool IsCustom(GameItem machine)
		{
			GameInventory deck = Deck(machine);
			return deck != null && Decks.TryGetValue(deck, out GameItem owner) && owner == machine;
		}

		private static List<string> Tracks(GameInventory deck)
		{
			List<string> tracks = new List<string>();
			if (deck == null)
			{
				return tracks;
			}
			foreach (GameItem item in deck.childItems)
			{
				if (item != null && item.IsTag("CASSETTE_TAG"))
				{
					tracks.Add(item.GetTagReadonly("CASSETTE_CONTENT").GetString());
				}
			}
			return tracks;
		}

		internal static void Play(GameItem machine, GameInventory deck)
		{
			switch (machine.GetTagReadonly("CASETTE_PLAYER_STATE_INT").GetInt())
			{
			case 0:
			{
				List<string> tracks = Tracks(deck);
				if (tracks.Count > 0 && !string.IsNullOrEmpty(tracks[0]))
				{
					PlayTrackAtIndex(machine, tracks, 0);
				}
				break;
			}
			case 1:
				Pause(machine);
				break;
			case 2:
				machine.ModifyTag("CASETTE_PLAYER_STATE_INT", state => state.Enable().SetInt(1));
				AudioManager.Instance.UnpauseMusic();
				AudioManager.Instance.PlayAndCallback(AudioManager.OnTrackDone);
				RefreshPlayingFrames(machine);
				break;
			}
		}

		private static void Pause(GameItem machine)
		{
			AudioManager.StopAudioCoroutine();
			machine.ModifyTag("CASETTE_PLAYER_STATE_INT", state => state.Enable().SetInt(2));
			AudioManager.Instance.PauseMusic();
			RefreshPlayingFrames(machine);
		}

		internal static void Stop(GameItem machine)
		{
			ShuffleActive = false;
			int state = machine.GetTagReadonly("CASETTE_PLAYER_STATE_INT").GetInt();
			if (state == 1 || state == 2)
			{
				machine.ModifyTag("CASETTE_PLAYER_STATE_INT", tag => tag.Enable().SetInt(0));
				AudioManager.StopAudioCoroutine();
				AudioManager.Instance.StopMusic();
				RefreshPlayingFrames(machine);
			}
		}

		internal static void Previous(GameItem machine, GameInventory deck)
		{
			int state = machine.GetTagReadonly("CASETTE_PLAYER_STATE_INT").GetInt();
			if (state != 1 && state != 2)
			{
				return;
			}
			List<string> tracks = Tracks(deck);
			if (tracks.Count != 0)
			{
				int index = (machine.GetTagReadonly("CURRENT_PLAYING_CASSETTE_INDEX_TAG").GetInt() - 1 + tracks.Count) % tracks.Count;
				PlayTrackAtIndex(machine, tracks, index);
			}
		}

		internal static void Next(GameItem machine, GameInventory deck)
		{
			int state = machine.GetTagReadonly("CASETTE_PLAYER_STATE_INT").GetInt();
			if (state != 1 && state != 2)
			{
				return;
			}
			List<string> tracks = Tracks(deck);
			if (tracks.Count != 0)
			{
				int index = (machine.GetTagReadonly("CURRENT_PLAYING_CASSETTE_INDEX_TAG").GetInt() + 1) % tracks.Count;
				PlayTrackAtIndex(machine, tracks, index);
			}
		}

		private static void PlayTrackAtIndex(GameItem machine, List<string> tracks, int index)
		{
			AudioManager.StopAudioCoroutine();
			machine.ModifyTag("CURRENT_PLAYING_CASSETTE_INDEX_TAG", tag => tag.Enable().SetInt(index));
			machine.ModifyTag("CURRENT_PLAYING_TRACK_ID_TAG", tag => tag.Enable().SetString(tracks[index]));
			machine.ModifyTag("CASETTE_PLAYER_STATE_INT", tag => tag.Enable().SetInt(1));
			AudioManager.Instance.PlayMusic(tracks[index]);
			AudioManager.Instance.PlayAndCallback(AudioManager.OnTrackDone);
			RefreshPlayingFrames(machine);
		}

		private static void RefreshPlayingFrames(GameItem machine)
		{
			GameInventory deck = Deck(machine);
			if (deck == null)
			{
				return;
			}
			foreach (GameItem item in deck.childItems)
			{
				GameItemElement element = item as GameItemElement;
				if (element != null)
				{
					element.RefreshPlayingFrame();
				}
			}
		}

		private static void OnLoaded(GameItem machine, GameInventory deck)
		{
			switch (machine.GetTagReadonly("CASETTE_PLAYER_STATE_INT").GetInt())
			{
			case 2:
				machine.ModifyTag("CASETTE_PLAYER_STATE_INT", tag => tag.Enable().SetInt(0));
				RefreshPlayingFrames(machine);
				break;
			case 1:
			{
				List<string> tracks = Tracks(deck);
				if (tracks.Count == 0)
				{
					machine.ModifyTag("CASETTE_PLAYER_STATE_INT", tag => tag.Enable().SetInt(0));
					break;
				}
				string current = machine.GetTagReadonly("CURRENT_PLAYING_TRACK_ID_TAG").GetString();
				int index = string.IsNullOrEmpty(current) ? -1 : tracks.IndexOf(current);
				if (index < 0)
				{
					index = Mathf.Clamp(machine.GetTagReadonly("CURRENT_PLAYING_CASSETTE_INDEX_TAG").GetInt(), 0, tracks.Count - 1);
				}
				if (!AudioManager.Instance.CanPlay(tracks[index]))
				{
					machine.ModifyTag("CASETTE_PLAYER_STATE_INT", tag => tag.Enable().SetInt(0));
					RefreshPlayingFrames(machine);
				}
				else
				{
					PlayTrackAtIndex(machine, tracks, index);
				}
				break;
			}
			}
		}

		private static void OnCassetteInventoryChanged(GameItem machine, GameInventory deck)
		{
			if (EmporiumEntry.Instance == null
				|| machine != EmporiumEntry.Instance.cassettePlayer
				|| machine.GetTagReadonly("CASETTE_PLAYER_STATE_INT").GetInt() == 0)
			{
				return;
			}
			List<string> tracks = Tracks(deck);
			if (tracks.Count == 0)
			{
				Stop(machine);
				return;
			}
			string current = machine.GetTagReadonly("CURRENT_PLAYING_TRACK_ID_TAG").GetString();
			int index = string.IsNullOrEmpty(current) ? -1 : tracks.IndexOf(current);
			if (index >= 0)
			{
				machine.ModifyTag("CURRENT_PLAYING_CASSETTE_INDEX_TAG", tag => tag.Enable().SetInt(index));
				RefreshPlayingFrames(machine);
			}
			else
			{
				int chosen = Mathf.Clamp(machine.GetTagReadonly("CURRENT_PLAYING_CASSETTE_INDEX_TAG").GetInt(), 0, tracks.Count - 1);
				PlayTrackAtIndex(machine, tracks, chosen);
			}
		}

		internal static bool Randomize(GameItem machine)
		{
			if (machine == null || AudioManager.Instance == null)
			{
				return false;
			}
			GameInventory deck = Deck(machine);
			if (deck == null)
			{
				return false;
			}
			List<string> tracks = Tracks(deck);
			if (tracks.Count == 0)
			{
				return false;
			}

			List<int> playable = new List<int>();
			for (int i = 0; i < tracks.Count; i++)
			{
				string track = tracks[i];
				if (!string.IsNullOrEmpty(track) && AudioManager.Instance.CanPlay(track))
				{
					playable.Add(i);
				}
			}
			if (playable.Count == 0)
			{
				return false;
			}

			int current = machine.GetTagReadonly("CURRENT_PLAYING_CASSETTE_INDEX_TAG").GetInt();
			int index = PickShuffled(machine, tracks, playable, current);

			// Route through the normal inventory-changed path so the audio
			// coroutine and green playing-frame overlay behave like Next.
			machine.ModifyTag("CASETTE_PLAYER_STATE_INT", tag => tag.Enable().SetInt(1));
			machine.ModifyTag("CURRENT_PLAYING_CASSETTE_INDEX_TAG", tag => tag.Enable().SetInt(index));
			machine.ModifyTag("CURRENT_PLAYING_TRACK_ID_TAG", tag => tag.Enable().SetString(""));
			OnCassetteInventoryChanged(machine, deck);
			return true;
		}

		private sealed class ShuffleBag
		{
			internal readonly Dictionary<string, int> Remaining = new Dictionary<string, int>();

			internal readonly Dictionary<string, int> Played = new Dictionary<string, int>();
		}

		// One bag per cassette player, session-scoped: a load starts a fresh cycle.
		private static readonly ConditionalWeakTable<GameItem, ShuffleBag> Bags =
			new ConditionalWeakTable<GameItem, ShuffleBag>();

		private static readonly System.Random ShuffleRandom = new System.Random();

		// Random without replacement: every playable tape is drawn once per cycle, then
		// the cycle reshuffles. Drawn by track identity and copy count, so a song on
		// three cassettes is drawn three times while duplicate songs never distort the
		// rest of the deck. The pool is reconciled with the deck on every draw, so a
		// tape added or removed mid-cycle joins or leaves the remaining pool at once.
		private static int PickShuffled(GameItem machine, List<string> tracks, List<int> playable, int current)
		{
			ShuffleBag bag = Bags.GetValue(machine, _ => new ShuffleBag());

			Dictionary<string, int> counts = new Dictionary<string, int>();
			foreach (int i in playable)
			{
				string track = tracks[i];
				counts[track] = counts.TryGetValue(track, out int n) ? n + 1 : 1;
			}

			Trim(bag.Remaining, counts);
			Trim(bag.Played, counts);
			foreach (KeyValuePair<string, int> pair in counts)
			{
				int have = Count(bag.Remaining, pair.Key) + Count(bag.Played, pair.Key);
				if (have < pair.Value)
				{
					bag.Remaining[pair.Key] = Count(bag.Remaining, pair.Key) + (pair.Value - have);
				}
			}

			if (bag.Remaining.Count == 0)
			{
				foreach (KeyValuePair<string, int> pair in bag.Played)
				{
					bag.Remaining[pair.Key] = Count(bag.Remaining, pair.Key) + pair.Value;
				}
				bag.Played.Clear();
			}

			string currentTrack = current >= 0 && current < tracks.Count ? tracks[current] : null;
			string chosen = Draw(bag.Remaining, currentTrack);
			if (chosen == null)
			{
				return playable[UnityEngine.Random.Range(0, playable.Count)];
			}

			bag.Remaining[chosen] = Count(bag.Remaining, chosen) - 1;
			if (bag.Remaining[chosen] <= 0)
			{
				bag.Remaining.Remove(chosen);
			}
			bag.Played[chosen] = Count(bag.Played, chosen) + 1;

			return ResolveIndex(tracks, playable, chosen, current);
		}

		private static void Trim(Dictionary<string, int> pool, Dictionary<string, int> counts)
		{
			foreach (string key in new List<string>(pool.Keys))
			{
				if (counts.TryGetValue(key, out int allowed))
				{
					if (pool[key] > allowed)
					{
						pool[key] = allowed;
					}
				}
				else
				{
					pool.Remove(key);
				}
			}
		}

		private static int Count(Dictionary<string, int> pool, string key)
		{
			return pool.TryGetValue(key, out int n) ? n : 0;
		}

		private static string Draw(Dictionary<string, int> pool, string avoid)
		{
			int total = 0;
			foreach (int n in pool.Values)
			{
				total += n;
			}
			if (total <= 0)
			{
				return null;
			}

			for (int attempt = 0; attempt < 8; attempt++)
			{
				int pick = ShuffleRandom.Next(total);
				foreach (KeyValuePair<string, int> pair in pool)
				{
					pick -= pair.Value;
					if (pick >= 0)
					{
						continue;
					}
					if (attempt < 7 && pair.Key == avoid && pool.Count > 1)
					{
						break;
					}
					return pair.Key;
				}
			}

			foreach (KeyValuePair<string, int> pair in pool)
			{
				if (pair.Value > 0)
				{
					return pair.Key;
				}
			}
			return null;
		}

		private static int ResolveIndex(List<string> tracks, List<int> playable, string track, int current)
		{
			List<int> matches = new List<int>();
			foreach (int i in playable)
			{
				if (tracks[i] == track)
				{
					matches.Add(i);
				}
			}
			if (matches.Count == 0)
			{
				return playable[UnityEngine.Random.Range(0, playable.Count)];
			}
			if (matches.Count > 1)
			{
				matches.Remove(current);
				if (matches.Count == 0)
				{
					matches.Add(current);
				}
			}
			return matches[ShuffleRandom.Next(matches.Count)];
		}
	}
}
