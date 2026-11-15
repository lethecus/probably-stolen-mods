using HarmonyLib;
using UnityEngine;

namespace TinkerChance
{
	public class TinkerChanceMod : IMod
	{
		internal const string ModId = "tinker_chance";

		public static ModLog Log { get; private set; }

		private static GameObject host;

		private static Harmony harmony;

		public void Init(ModManifest manifest)
		{
			Log = new ModLog(manifest);
		}

		private static void ReloadSettings()
		{
			TinkerSettings.Load();
			if (Log != null)
			{
				Log.Log("Settings loaded: keep chance " + TinkerSettings.Chance + "%, hotkey " + TinkerSettings.Hotkey + ".");
			}
		}

		public void OnEnable()
		{
			// Defaults only - the mod-data store needs a loaded run.
			TinkerSettings.Load();
			ModHook.OnGameLoadedLate += ReloadSettings;

			harmony = new Harmony("mod.tinker_chance");
			harmony.PatchAll(typeof(TinkerChanceMod).Assembly);

			// Fallback for testing: set the chance from the debug menu, no window needed.
			ModHelper.AddDebugFunction(ModId, "Tinkerer", "Chance 0%", delegate { TinkerSettings.SetChance(0); }, false);
			ModHelper.AddDebugFunction(ModId, "Tinkerer", "Chance 20%", delegate { TinkerSettings.SetChance(20); }, false);
			ModHelper.AddDebugFunction(ModId, "Tinkerer", "Chance 50%", delegate { TinkerSettings.SetChance(50); }, false);
			ModHelper.AddDebugFunction(ModId, "Tinkerer", "Chance 100%", delegate { TinkerSettings.SetChance(100); }, false);
			ModHelper.AddDebugFunction(ModId, "Tinkerer", "Test consume (spawn scrap)", delegate
			{
				GameItem item = ModHelper.SpawnItem("scrap_metal");
				if (item == null)
				{
					if (Log != null) { Log.Log("Test: could not spawn scrap_metal"); }
					return;
				}
				if (Log != null) { Log.Log("Test: calling ConsumeUpgradeMaterial at " + TinkerSettings.Chance + "%"); }
				StartingPerkContent.ConsumeUpgradeMaterial(item);
				if (Log != null) { Log.Log("Test: finished (see the Upgrade material line above)"); }
			}, false);

			ModHelper.AddDebugFunction(ModId, "Tinkerer", "Log current chance", delegate
			{
				if (Log != null)
				{
					Log.Log("Current keep chance: " + TinkerSettings.Chance + "%");
				}
			}, false);

			host = new GameObject("TinkerChance.Settings");
			Object.DontDestroyOnLoad(host);
			host.AddComponent<TinkerSettingsHost>();

			if (Log != null)
			{
				Log.Log("Tinkerer keep chance " + TinkerSettings.Chance + "%, settings hotkey " + TinkerSettings.Hotkey + ".");
			}
		}

		public void OnDisable()
		{
			ModHook.OnGameLoadedLate -= ReloadSettings;
			ModHelper.RemoveDebugFunctions(ModId);
			if (harmony != null)
			{
				harmony.UnpatchAll("mod.tinker_chance");
				harmony = null;
			}
			if (host != null)
			{
				Object.Destroy(host);
				host = null;
			}
		}
	}
}
