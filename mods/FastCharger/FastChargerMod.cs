using System;
using HarmonyLib;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FastCharger
{
	/// <summary>
	/// Personal mod. Adds two new luxury machines that Dr. Jackson sometimes stocks:
	///
	///   Fast Charger - an eight-bay recharger (8x2 footprint, 8x1 battery grid) that restores a
	///                  configurable amount of energy to every battery inside it each cycle.
	///   Fast Bloc    - a powerblock look-alike that charges adjacent batteries and adjacent
	///                  machines' internal cells by its own configurable amount each cycle.
	///
	/// Both keep the vanilla cycle hooks (onCycleEndSlotItemFunc / onCycleEndEarlySlotItemFunc) and
	/// the vanilla power gate, so only the amount differs. Their rates live in this mod's settings.
	///
	/// Stock gating follows the design: 20% chance from the first day the inventor's shop is
	/// accessible, +2% for every 5 days since, capped at 40%. The charger and the bloc roll
	/// independently. The first day is remembered through ModHelper mod data so it survives saves.
	/// </summary>
	public sealed class FastChargerMod : IMod
	{
		internal const string ModId = "fast_charger";
		internal const string ChargerId = "fast_charger";
		internal const string BlocId = "fast_bloc";

		internal static ModLog Log;

		private static ConfigEntry<int> chargerRate;

		private static ConfigEntry<int> blocRate;

		private static ConfigEntry<bool> animate;

		private static ConfigEntry<int> animFps;

		private static readonly List<GameItem> liveItems = new List<GameItem>();

		private static int frame;

		// Literal type lookup, so the scanner classifies it as an internal game type.
		private static readonly bool ModifiersAvailable = AccessTools.TypeByName("ItemSpriteModifier") != null;


		private static GameObject updaterObject;

		private static float nextFrameAt;

		private static bool lastPower;

		private static float nextScan;

		private static int lastTracked = -1;

		private static float nextTickLog;

		// The first failure of each kind is logged with its stack so a report can be acted on; later ones are
		// counted only. Without this a failing tick wrote one line per frame, which is what filled the dev log.
		private static bool reportedAnimationError;

		private static bool reportedScanError;

		private static bool storeWasReady;

		private static int appliedCharger = 30;

		private static int appliedBloc = 20;

		private static bool appliedAnimate = true;

		private static int appliedFps = 8;

		private const int FrameCount = 8;

		// Height of the panel strip at the top of the charger art; battery sprites are cropped by it.
		private const float PanelPixels = 10f;

		private static readonly Dictionary<string, Sprite> batteryCrops = new Dictionary<string, Sprite>();

		private static int ChargerCharge
		{
			get
			{
				return Mathf.Clamp(appliedCharger, 0, 500);
			}
		}

		private static int BlocCharge
		{
			get
			{
				return Mathf.Clamp(appliedBloc, 0, 500);
			}
		}

		private static bool AnimateEnabled
		{
			get
			{
				return appliedAnimate;
			}
		}

		private static int Fps
		{
			get
			{
				return Mathf.Clamp(appliedFps, 4, 20);
			}
		}

		private static string ChargerFrame
		{
			get
			{
				return "fast_charger_" + frame;
			}
		}

		private static string BlocFrame(bool powered)
		{
			return (powered ? "fast_bloc_on_" : "fast_bloc_") + frame;
		}

		public void Init(ModManifest manifest)
		{
			Log = new ModLog(manifest);
		}

		public void OnEnable()
		{
			try
			{
				ModConfig config = ModConfig.For(ModId);
				chargerRate = config.Define("charge_rate", 30, Loc.Get("cfg.section.charger"), () => Loc.Get("cfg.rate.label"), 0, 500, () => Loc.Get("cfg.rate.tip"), true);
				blocRate = config.Define("bloc_rate", 20, Loc.Get("cfg.section.bloc"), () => Loc.Get("cfg.bloc.label"), 0, 500, () => Loc.Get("cfg.bloc.tip"), true);
				animate = config.Define("animate_rainbow", true, Loc.Get("cfg.section.appearance"), () => Loc.Get("cfg.animate.label"), () => Loc.Get("cfg.animate.tip"), true);
				animFps = config.Define("animation_fps", 12, "Appearance", () => Loc.Get("cfg.fps.label"), 4, 20, () => Loc.Get("cfg.fps.tip"), true);
			}
			catch (Exception ex)
			{
				Log?.Warning("Could not register the charge settings: " + ex.Message);
			}

			ApplySettings();

			ModHook.OnModItemDirectoryInit += OnItemDirectory;
			ModHook.OnPlaceInventorInventoryItemLate += OnInventorStock;
			ModHook.OnGameLoadedLate += ApplySettings;
			ModHook.OnGameLoadedLate += ScanItems;
			ModHook.OnGameLoadedLate += StartAnimation;
			ModHook.OnGameLoadedLate += StampJacksonDay;
			ModHook.OnWakingUpLate += StampJacksonDay;

			// Fallback in case the mod item directory already initialised before this mod enabled.
			try
			{
				ModItemDirectory existing = UnityEngine.Object.FindObjectOfType<ModItemDirectory>();
				if (existing != null)
				{
					OnItemDirectory(existing);
				}
			}
			catch (Exception ex)
			{
				Log?.Warning("Could not look for an existing item directory: " + ex.Message);
			}

			Log?.Log("Fast charger enabled: charger " + ChargerCharge + " per cycle, bloc " + BlocCharge + " per cycle.");

			StartAnimation();
		}

		public void OnDisable()
		{
			ModHook.OnModItemDirectoryInit -= OnItemDirectory;
			ModHook.OnPlaceInventorInventoryItemLate -= OnInventorStock;
			ModHook.OnGameLoadedLate -= ApplySettings;
			ModHook.OnGameLoadedLate -= ScanItems;
			ModHook.OnGameLoadedLate -= StartAnimation;
			ModHook.OnGameLoadedLate -= StampJacksonDay;
			ModHook.OnWakingUpLate -= StampJacksonDay;

			if (updaterObject != null)
			{
				UnityEngine.Object.Destroy(updaterObject);
				updaterObject = null;
			}
		}

		private static void StartAnimation()
		{
			try
			{
				EnsureUpdater();
			}
			catch (Exception)
			{
				// Expected while the menu is still loading: the driver cannot be created yet. OnGameLoadedLate
				// calls this again once the shop exists, which is the call that matters. Logging this one was
				// the last warning the mod still wrote at startup.
			}
			if (!StoreReady())
			{
				return;
			}
			try
			{
				nextFrameAt = 0f;
				lastPower = !(PlayerStore.Instance != null && PlayerStore.Instance.isPowerOn);
			}
			catch (Exception ex)
			{
				Log?.Warning("Could not reset the rainbow animation: " + ex.Message);
			}
		}

		// A plain MonoBehaviour tick, because ModHelper's coroutine helpers do not exist in older game builds
		// and calling them there aborted OnEnable before the items were registered.
		private static void EnsureUpdater()
		{
			if (updaterObject != null)
			{
				return;
			}
			updaterObject = new GameObject("FastChargerUpdater");
			UnityEngine.Object.DontDestroyOnLoad(updaterObject);
			updaterObject.AddComponent<FastChargerUpdater>();
		}

		/// <summary>Reads the settings once per load: the sliders say "restart required", so mid-day changes wait.</summary>
		private static void ApplySettings()
		{
			try
			{
				appliedCharger = chargerRate == null ? 30 : chargerRate.Value;
				appliedBloc = blocRate == null ? 20 : blocRate.Value;
				appliedAnimate = animate == null || animate.Value;
				appliedFps = animFps == null ? 8 : animFps.Value;
				Log?.Log("Settings applied: charger " + ChargerCharge + " per cycle, bloc " + BlocCharge + " per cycle, animation " + (AnimateEnabled ? "on at " + Fps + " fps" : "off") + ".");
			}
			catch (Exception ex)
			{
				Log?.Warning("Could not apply the settings: " + ex.Message);
			}
		}

		// ----------------------------------------------------------------- items

		private static void OnItemDirectory(ModItemDirectory directory)
		{
			try
			{
				directory.Add(ChargerId, CreateFastCharger);
				directory.Add(BlocId, CreateFastBloc);
				Log?.Log("Registered " + ChargerId + " and " + BlocId + ".");
			}
			catch (Exception ex)
			{
				Log?.Warning("Could not register the items: " + ex.Message);
			}
		}

		private static GameItem CreateFastCharger()
		{
			GameGridInventory inventory = new GameGridInventory(8, 2);
			inventory.mayInventoryAddItemFunc = (GameItem item, GameInventory _) =>
			{
				if (item == null || !GeneralHelper.IsItemOwned(item) || !item.IsTag("power_source_item"))
				{
					return false;
				}
				// Disposable cells cannot be recharged; hypertech cells are refused by the charge code itself.
				return !PowerHelper.IsDisposablePowerSource(item) && !HypertechBatteryHelper.IsHypertechBattery(item);
			};

			PixelWindow window = MachineHelper.CreateMachineryWindow(true, MachineHelper.Panel(LocHelper.GetLocalizedMechanic("mech_recharger_label_battery"), inventory, "batteries"));

			GameItem machine = ItemDirectory.CreateEmptyItem()
				.SetContentWindow(window)
				.SetSpriteAndShapeFromMod(ModId, "fast_charger_0");
			machine.name = Loc.Get("item.charger.name");
			machine.shortDescription = Loc.Get("item.charger.desc");
			machine.flavorText = Loc.Get("item.charger.flavor");
			machine.unitValue = 450L;
			machine.SetGameItemType("MACHINE");
			machine.SetGameItemType("LUXURY_ITEM");
			machine.EnableTag("IMPORTANT_TAG");
			machine.EnableTag("CONTAINER_TAG");
			machine.EnableTag("manufacture");
			AudioHelper.InitSmallMachine(machine);

			machine.onCycleEndSlotItemFunc = (GameItem current, GameInventory _, SlotMarker _) =>
			{
				try
				{
					if (PlayerStore.Instance != null && PlayerStore.Instance.isPowerOn)
					{
						int amount = ChargerCharge;
						if (amount > 0)
						{
							foreach (GameItem child in inventory.childItems)
							{
								if (child != null && child.IsTag("power_source_item"))
								{
									PowerHelper.ChargeBattery(child, amount);
								}
							}
						}
					}
					current.Validate();
				}
				catch (Exception ex)
				{
					Log?.Warning("Fast charger cycle failed: " + ex.Message);
				}
			};

			inventory.onSlotAddItemEndFunc = (GameItem _, GameInventory _, SlotMarker _) => machine.Validate();
			inventory.onSlotRemoveItemEndFunc = (GameItem _, GameInventory _, SlotMarker _) => machine.Validate();
			inventory.onSlotAddItemFunc += (GameItem _, GameInventory _, SlotMarker _) => AudioManager.Instance.Play3("battery_insert");
			inventory.onSlotRemoveItemFunc += (GameItem _, GameInventory _, SlotMarker _) => AudioManager.Instance.Play3("battery_removed");

			// The animated pips need ItemSpriteModifier, which older game builds do not ship. The call is
			// isolated in its own method and only made when the type exists, so an old build never JITs it.
			if (ModifiersAvailable)
			{
				AttachSpriteModifiers(machine, inventory);
			}
			machine.onLoaded = (Action<GameItem>)Delegate.Combine(machine.onLoaded, new Action<GameItem>(Register));
			Register(machine);
			return machine;
		}

		private static void AttachSpriteModifiers(GameItem machine, GameInventory inventory)
		{
			machine.getItemSpriteModifierForItemFunc = (GameItem _) => ChargerSprites(inventory);
		}

		private static List<ItemSpriteModifier> ChargerSprites(GameInventory inventory)
		{
			List<ItemSpriteModifier> modifiers = new List<ItemSpriteModifier>();
			int index = 0;

			foreach (GameItem child in inventory.childItems)
			{
				Sprite battery = BatterySprite(child);
				if (battery != null)
				{
					modifiers.Add(new ItemSpriteModifier(battery, new Vector2(index * 16f, -PanelPixels)));
				}
				index++;
			}

			if (PlayerStore.Instance == null || !PlayerStore.Instance.isPowerOn)
			{
				return modifiers;
			}

			Sprite full = ModSprite("fast_charger_g");
			Sprite charging = ModSprite("fast_charger_o");
			index = 0;
			foreach (GameItem child in inventory.childItems)
			{
				Sprite pip = child != null && PowerHelper.IsPowerSourceFull(child) ? full : charging;
				if (pip != null)
				{
					modifiers.Add(new ItemSpriteModifier(pip, new Vector2(7f + index * 16f, -8f)));
				}
				index++;
			}

			return modifiers;
		}

		private static GameItem CreateFastBloc()
		{
			GameItem item = ItemDirectory.CreateEmptyItem()
				.SetSpriteAndShapeFromMod(ModId, "fast_bloc_0");
			item.name = Loc.Get("item.bloc.name");
			item.shortDescription = Loc.Get("item.bloc.desc");
			item.flavorText = Loc.Get("item.bloc.flavor");
			item.unitValue = 600L;
			item.SetGameItemType("MACHINE");
			item.SetGameItemType("LUXURY_ITEM");
			item.EnableTag("IMPORTANT_TAG");
			AudioHelper.InitSmallMachine(item);

			item.onCycleEndEarlySlotItemFunc = (GameItem current, GameInventory _, SlotMarker _) =>
			{
				try
				{
					bool powered = PlayerStore.Instance != null && PlayerStore.Instance.isPowerOn;
					SetBlocSprite(current, powered);
					if (!powered)
					{
						return;
					}

					int amount = BlocCharge;
					if (amount <= 0)
					{
						return;
					}

					foreach (GameItem other in current.FindAdjacentType<GameItem>())
					{
						if (other == null)
						{
							continue;
						}
						if (other.IsTag("power_source_item"))
						{
							PowerHelper.ChargeBattery(other, amount);
						}
						else if (other.IsTag("MACHINE_RECHARGABLE_TAG"))
						{
							MachineHelper.RechargeMachineBattery(other, amount);
						}
					}
				}
				catch (Exception ex)
				{
					Log?.Warning("Fast bloc cycle failed: " + ex.Message);
				}
			};

			Register(item);
			return item;
		}

		private static void Register(GameItem item)
		{
			if (item == null || liveItems.Contains(item))
			{
				return;
			}
			// Only placed machines are animated. The factory also runs to build the item definition, and those
			// prototypes have no parent inventory: pushing a sprite into one throws (an empty-message exception),
			// which used to land in the log right after a long stall. ScanItems picks the placed ones up.
			if (item.parentInventory == null)
			{
				return;
			}
			liveItems.Add(item);
		}

		/// <summary>Items restored from a save do not always pass through the factory, so find them once per load.</summary>
		private static void ScanItems()
		{
			try
			{
				if (!StoreReady())
				{
					return;
				}
				if (PlayerStore.Instance == null)
				{
					return;
				}

				int found = 0;
				List<GameItem> all = PlayerStore.Instance.FindAllItem();

				foreach (GameItem item in all)
				{
					if (item == null)
					{
						continue;
					}
					if (item.identifier == ChargerId || item.identifier == BlocId)
					{
						Register(item);
						// A restored item can bind its sprite before the mod textures are ready; bind it again here so
						// it never stays on the game's 1x1 placeholder.
						try
						{
							bool powered = PlayerStore.Instance != null && PlayerStore.Instance.isPowerOn;
							item.SetSprite(ModId, item.identifier == ChargerId ? ChargerFrame : BlocFrame(powered));
						}
						catch (Exception ex)
						{
							Log?.Warning("Could not refresh a sprite: " + ex.Message);
						}
						found++;
					}
				}
				if (liveItems.Count != lastTracked)
				{
					lastTracked = liveItems.Count;
					Log?.Log("Animation tracking " + liveItems.Count + " machine(s) (" + found + " found in this scan).");
				}
			}
			catch (Exception ex)
			{
				if (!reportedScanError)
				{
					reportedScanError = true;
					Log?.Warning("Could not scan for existing fast chargers:\n" + ex);
				}
			}
		}

		private static void SetBlocSprite(GameItem item, bool powered)
		{
			try
			{
				item.SetSprite(ModId, BlocFrame(powered));
				Refresh(item);
			}
			catch (Exception ex)
			{
				Log?.Warning("Could not swap the fast bloc sprite: " + ex.Message);
			}
		}

		/// <summary>Pushes the new sprite into the item's own Image without a full Validate.</summary>
		private static void Refresh(GameItem item)
		{
			GameItemElement element = item as GameItemElement;
			if (element != null)
			{
				element.ValidatePartialSprite();
			}
		}

		// ------------------------------------------------------------ animation

		/// <summary>
		/// True only once the shop scene exists. The updater is created from OnEnable, which runs while the
		/// main menu is still up, and in that state there is nothing to animate or scan: every call throws
		/// a null reference and the mod used to log one warning per frame for the whole menu stay.
		/// </summary>
		private static bool StoreReady()
		{
			try
			{
				EmporiumEntry entry = EmporiumEntry.Instance;
				return entry != null && entry.invElement != null;
			}
			catch (Exception)
			{
				return false;
			}
		}

		internal static void TickAnimation()
		{
			try
			{
				if (!StoreReady())
				{
					storeWasReady = false;
					return;
				}
				if (!storeWasReady)
				{
					// Entering the shop: retry immediately and allow one fresh stack per failure kind. Leaving
					// the shop resets this, so a menu stay cannot silence a real error for the whole session.
					storeWasReady = true;
					reportedAnimationError = false;
					reportedScanError = false;
					nextFrameAt = 0f;
					nextScan = 0f;
				}

				if (UnityEngine.Time.time >= nextScan)
				{
					nextScan = UnityEngine.Time.time + 2f;
					ScanItems();
				}

				bool animating = AnimateEnabled;
				bool powered = PlayerStore.Instance != null && PlayerStore.Instance.isPowerOn;

				if (animating)
				{
					if (UnityEngine.Time.time < nextFrameAt)
					{
						return;
					}
					nextFrameAt = UnityEngine.Time.time + 1f / Fps;
				}

				int next = animating ? (frame + 1) % FrameCount : 0;
				if (next != frame || powered != lastPower)
				{
					frame = next;
					lastPower = powered;
					ApplyFrames(powered);
				}

				if (UnityEngine.Time.time >= nextTickLog)
				{
					nextTickLog = UnityEngine.Time.time + 5f;
					// Deliberately silent: ScanItems already logs when the tracked machine count changes, and a
					// line every five seconds was noise in a log that is read by hand.
				}
			}
			catch (Exception ex)
			{
				// Never retry at frame rate, and keep the dev log readable: one full stack, then silence until
				// the shop is entered again.
				nextFrameAt = UnityEngine.Time.time + 2f;
				if (!reportedAnimationError)
				{
					reportedAnimationError = true;
					Log?.Warning("Rainbow animation failed; paused until the next load:\n" + ex);
				}
			}
		}

		private static void ApplyFrames(bool powered)
		{
			for (int i = liveItems.Count - 1; i >= 0; i--)
			{
				GameItem item = liveItems[i];
				if (item == null)
				{
					liveItems.RemoveAt(i);
					continue;
				}

				try
				{
					string key = item.identifier == ChargerId ? ChargerFrame : BlocFrame(powered);
					item.SetSprite(ModId, key);
					Refresh(item);
				}
				catch (Exception ex)
				{
					Log?.Warning("Could not apply an animation frame: " + ex.Message);
					liveItems.RemoveAt(i);
				}
			}
		}

		// --------------------------------------------------------------- sprites

		private static Sprite ModSprite(string key)
		{
			LoadedMod mod = FindMod(ModId);
			if (mod != null && mod.Sprites.TryGetValue(key, out Sprite sprite))
			{
				return sprite;
			}
			return null;
		}

		/// <summary>Battery sprite with its top cut off, so it tucks under the panel like in the vanilla charger.</summary>
		private static Sprite BatterySprite(GameItem item)
		{
			Sprite sprite = ItemSprite(item);
			if (sprite == null)
			{
				return null;
			}
			if (sprite.rect.height <= PanelPixels)
			{
				return null;
			}

			string key = sprite.texture.name + "#" + sprite.name + "#" + (int)sprite.rect.height;
			if (batteryCrops.TryGetValue(key, out Sprite cropped))
			{
				return cropped;
			}

			Rect rect = sprite.rect;
			cropped = Sprite.Create(sprite.texture, new Rect(rect.x, rect.y, rect.width, rect.height - PanelPixels), new Vector2(0.5f, 0f), sprite.pixelsPerUnit);
			batteryCrops[key] = cropped;
			return cropped;
		}

		private static Sprite ItemSprite(GameItem item)
		{
			if (item == null)
			{
				return null;
			}
			if (item.useSpriteFromMod)
			{
				LoadedMod mod = FindMod(item.spriteAtlasPath);
				if (mod != null && mod.Sprites.TryGetValue(item.spritePath, out Sprite sprite))
				{
					return sprite;
				}
				return null;
			}
			return RenderHandler.LoadFromAtlas(item.spriteAtlasPath, item.spritePath);
		}

		private static LoadedMod FindMod(string id)
		{
			foreach (LoadedMod mod in ModLoader.Mods)
			{
				if (mod != null && mod.Manifest != null && mod.Manifest.ID == id)
				{
					return mod;
				}
			}
			return null;
		}

		// ----------------------------------------------------------- Dr Jackson

		private static void StampJacksonDay()
		{
			try
			{
				if (PlayerStore.Instance == null || PlayerStore.Instance.storeClientData == null || StoreStation.Instance == null)
				{
					return;
				}
				if (!PlayerStore.Instance.storeClientData.isJacksonIntroduced)
				{
					return;
				}
				if (ModHelper.GetModDataInt(ModId, "jackson_first_day", -1) < 0)
				{
					ModHelper.SetModDataInt(ModId, "jackson_first_day", StoreStation.Instance.dayCounter);
					Log?.Log("Dr Jackson first seen on day " + StoreStation.Instance.dayCounter + "; luxury stock starts at 20%.");
				}
			}
			catch (Exception ex)
			{
				Log?.Warning("Could not record Dr Jackson's first day: " + ex.Message);
			}
		}

		private static void OnInventorStock(List<GameItem> owned)
		{
			try
			{
				if (PlayerStore.Instance == null || PlayerStore.Instance.storeClientData == null || StoreStation.Instance == null)
				{
					return;
				}
				if (!PlayerStore.Instance.storeClientData.isJacksonIntroduced)
				{
					return;
				}

				int day = StoreStation.Instance.dayCounter;
				int first = ModHelper.GetModDataInt(ModId, "jackson_first_day", -1);
				if (first < 0)
				{
					first = day;
					ModHelper.SetModDataInt(ModId, "jackson_first_day", first);
				}

				if (ModHelper.GetModDataInt(ModId, "last_offer_day", -1) == day)
				{
					return;
				}
				ModHelper.SetModDataInt(ModId, "last_offer_day", day);

				int chance = ChanceFor(day, first);
				bool offerCharger = RNG.Roll(chance);
				bool offerBloc = RNG.Roll(chance);

				if (offerCharger)
				{
					PlayerStore.Instance.AddDirectSellingItemToTable(ChargerId);
				}
				if (offerBloc)
				{
					PlayerStore.Instance.AddDirectSellingItemToTable(BlocId);
				}

				Log?.Log("Dr Jackson stock (day " + day + ", day " + (day - first) + " after introduction, chance " + chance + "%): fast charger " + (offerCharger ? "offered" : "no") + ", fast bloc " + (offerBloc ? "offered" : "no") + ".");
			}
			catch (Exception ex)
			{
				Log?.Warning("Could not roll Dr Jackson's luxury stock: " + ex.Message);
			}
		}

		private static int ChanceFor(int day, int firstDay)
		{
			int days = day - firstDay;
			if (days < 0)
			{
				days = 0;
			}
			return Mathf.Clamp(20 + 2 * (days / 5), 20, 40);
		}
	}
}
