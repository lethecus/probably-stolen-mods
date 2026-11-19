using System;
using System.Collections.Generic;
using HarmonyLib;

namespace SaveTheNewspaper
{
	/// <summary>
	/// Keeps the morning newspaper from deleting itself overnight.
	///
	/// Vanilla gives the newspaper item a cycle-end callback that destroys it:
	///     gameItem.onCycleEndSlotItemFunc = (item, inv, slot) => item.Destroy();
	/// and <c>EmporiumEntry.EndNight</c> runs that callback for every owned item it can find - walking the
	/// front and back counters, the inventory, the showcase, the doc window, the hidden window, the afterhour
	/// window and the vending windows, recursively, so a paper inside a dossier is found too.
	///
	/// This mod clears that callback on the newspapers found by <c>FindAllItem</c> before the night pass runs.
	/// Nothing else is touched: the paper left on the front counter is still destroyed, because the counter is
	/// emptied separately by <c>ClearFrontInv</c>. The ones you put away survive.
	/// </summary>
	public sealed class SaveTheNewspaperMod : IMod
	{
		internal const string ModId = "save_the_newspaper";

		internal static ModLog Log;

		private static ConfigEntry<bool> keepSetting;

		private static Harmony harmony;

		public void Init(ModManifest manifest)
		{
			Log = new ModLog(manifest);
		}

		public void OnEnable()
		{
			try
			{
				ModConfig config = ModConfig.For(ModId);
				keepSetting = config.Define(
					"keep_newspaper",
					true,
					Loc.Get("cfg.section.newspaper"),
					() => Loc.Get("cfg.keep.label"),
					() => Loc.Get("cfg.keep.tip"),
					false);
			}
			catch (Exception ex)
			{
				Log?.Warning("Could not register the setting: " + ex.Message);
			}

			try
			{
				harmony = new Harmony("mod.save_the_newspaper");
				var endNight = AccessTools.Method(typeof(PlayerStore), "EndNight", Type.EmptyTypes);
				if (endNight == null)
				{
					Log?.Warning("PlayerStore.EndNight not found; nothing patched.");
					return;
				}
				harmony.Patch(endNight, prefix: new HarmonyMethod(typeof(SaveTheNewspaperMod), nameof(EndNightPrefix)));
				Log?.Log("Enabled: the newspaper is kept overnight while the setting is on.");
			}
			catch (Exception ex)
			{
				Log?.Warning("Could not patch EndNight: " + ex.Message);
			}
		}

		public void OnDisable()
		{
			try
			{
				harmony?.UnpatchAll("mod.save_the_newspaper");
			}
			catch
			{
			}
			harmony = null;
		}

		/// <summary>
		/// Runs before the night pass, which is where the newspaper's own destroy callback fires.
		/// </summary>
		private static void EndNightPrefix(PlayerStore __instance)
		{
			try
			{
				if (keepSetting == null || !keepSetting.Value || __instance == null)
				{
					return;
				}

				List<GameItem> items = __instance.FindAllItem();
				for (int i = 0; i < items.Count; i++)
				{
					GameItem item = items[i];
					if (item == null || item.identifier != "newspaper")
					{
						continue;
					}
					item.onCycleEndSlotItemFunc = null;
				}
			}
			catch (Exception ex)
			{
				Log?.Warning("Could not keep the newspaper: " + ex.Message);
			}
		}
	}
}
