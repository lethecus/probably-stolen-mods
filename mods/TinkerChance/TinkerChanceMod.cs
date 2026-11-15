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

		public void OnEnable()
		{
			TinkerSettings.Load();

			harmony = new Harmony("mod.tinker_chance");
			harmony.PatchAll(typeof(TinkerChanceMod).Assembly);

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
