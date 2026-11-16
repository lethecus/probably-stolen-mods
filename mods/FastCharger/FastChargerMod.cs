using System;
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

		private static bool lastPower;

		private static float nextScan;

		private static int lastTracked = -1;

		private static float nextTickLog;

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
				chargerRate = config.Define("charge_rate", 30, "Fast charger", () => "Charge per cycle", 0, 500, () => "Energy restored to each battery per machine cycle. Vanilla recharger is 3 (6 with the Commercial Power upgrade).", true);
				blocRate = config.Define("bloc_rate", 20, "Fast bloc", () => "Charge per cycle", 0, 500, () => "Energy restored to each adjacent battery or machine cell per cycle. Vanilla powerblock is 2 (4 with the Commercial Power upgrade).", true);
				animate = config.Define("animate_rainbow", true, "Appearance", () => "Animate rainbow", () => "Scrolls the rainbow colours across the machines. Off keeps the static frame 0 art.", true);
				animFps = config.Define("animation_fps", 12, "Appearance", () => "Animation speed (fps)", 4, 20, () => "Rainbow frames per second. 8 is a calm scroll, 12-16 is clearly moving.", true);
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
		}

		private static void StartAnimation()
		{
			try
			{
				ModHelper.StopAllCoroutines(ModId);
				ModHelper.StartCoroutine(ModId, AnimateLoop());
			}
			catch (Exception ex)
			{
				Log?.Warning("Could not start the rainbow animation: " + ex.Message);
			}
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
			GameGridInventory inventory = new GameGridInventory(8, 1);
			inventory.mayInventoryAddItemFunc = (GameItem item, GameInventory _) =>
			{
				if (item == null || !GeneralHelper.IsItemOwned(item))
				{
					return false;
				}
				return !PowerHelper.IsDisposablePowerSource(item) && item.IsTag("power_source_item");
			};

			PixelWindow window = MachineHelper.CreateMachineryWindow(true, MachineHelper.Panel(LocHelper.GetLocalizedMechanic("mech_recharger_label_battery"), inventory, "batteries"));

			GameItem machine = ItemDirectory.CreateEmptyItem()
				.SetContentWindow(window)
				.SetSpriteAndShapeFromMod(ModId, "fast_charger_0");
			machine.name = "Fast Charger";
			machine.shortDescription = "Eight battery bays and a luxury charging array. Restores far more energy per cycle than a standard recharger; the amount is set in the Fast Charger mod settings.";
			machine.flavorText = "Station-grade hardware, sold to people who do not like waiting.";
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

			machine.getItemSpriteModifierForItemFunc = (GameItem _) => ChargerSprites(inventory);
			machine.onLoaded = (Action<GameItem>)Delegate.Combine(machine.onLoaded, new Action<GameItem>(Register));
			Register(machine);
			return machine;
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
			item.name = "Fast Bloc";
			item.shortDescription = "A luxury power block. Rapidly charges adjacent batteries and the internal cells of nearby machines; the amount is set in the Fast Charger mod settings.";
			item.flavorText = "The orange stripe means it is working. Probably.";
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
			liveItems.Add(item);
		}

		/// <summary>Items restored from a save do not always pass through the factory, so find them once per load.</summary>
		private static void ScanItems()
		{
			try
			{
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
				Log?.Warning("Could not scan for existing fast chargers: " + ex.Message);
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

		private static IEnumerator AnimateLoop()
		{
			while (true)
			{
				float wait = 0.5f;
				try
				{
					if (UnityEngine.Time.time >= nextScan)
					{
						nextScan = UnityEngine.Time.time + 2f;
						ScanItems();
					}

					bool animating = AnimateEnabled;
					bool powered = PlayerStore.Instance != null && PlayerStore.Instance.isPowerOn;
					int next = animating ? (frame + 1) % FrameCount : 0;

					if (next != frame || powered != lastPower)
					{
						frame = next;
						lastPower = powered;
						ApplyFrames(powered);
					}

					wait = animating ? 1f / Fps : 0.5f;

					if (UnityEngine.Time.time >= nextTickLog)
					{
						nextTickLog = UnityEngine.Time.time + 5f;
						Log?.Log("Anim tick: frame " + frame + ", " + liveItems.Count + " item(s), " + (animating ? Fps + " fps" : "paused") + ".");
					}
				}
				catch (Exception ex)
				{
					Log?.Warning("Rainbow animation failed: " + ex.Message);
					wait = 1f;
				}

				yield return new WaitForSeconds(wait);
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
