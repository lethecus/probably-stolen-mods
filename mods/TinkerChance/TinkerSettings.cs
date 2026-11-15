using System;
using UnityEngine;

namespace TinkerChance
{
	/// <summary>
	/// Persisted settings: the keep chance and the settings hotkey.
	///
	/// Mod data only exists while a run is loaded (PlayerStore.Instance.ModData()), and that instance is a
	/// Unity fake-null at mod-enable time, so reading it during OnEnable throws. Load() therefore falls back
	/// to defaults and is called again from ModHook.OnGameLoadedLate once the world is available.
	/// </summary>
	internal static class TinkerSettings
	{
		internal const int DefaultChance = 20;

		internal const KeyCode DefaultHotkey = KeyCode.F4;

		internal static int Chance { get; private set; } = DefaultChance;

		internal static KeyCode Hotkey { get; private set; } = DefaultHotkey;

		internal static bool Loaded { get; private set; }

		internal static void Load()
		{
			try
			{
				Chance = Mathf.Clamp(ModHelper.GetModDataInt(TinkerChanceMod.ModId, "chance", DefaultChance), 0, 100);
				int raw = ModHelper.GetModDataInt(TinkerChanceMod.ModId, "hotkey", (int)DefaultHotkey);
				Hotkey = Enum.IsDefined(typeof(KeyCode), raw) ? (KeyCode)raw : DefaultHotkey;
				Loaded = true;
			}
			catch (Exception ex)
			{
				Chance = DefaultChance;
				Hotkey = DefaultHotkey;
				Loaded = false;
				TinkerChanceMod.Log?.Warning("Settings not readable yet, using defaults (" + ex.GetType().Name + ").");
			}
		}

		internal static void SetChance(int value)
		{
			Chance = Mathf.Clamp(value, 0, 100);
			Save("chance", Chance);
		}

		internal static void SetHotkey(KeyCode key)
		{
			Hotkey = key;
			Save("hotkey", (int)key);
		}

		private static void Save(string key, int value)
		{
			try
			{
				ModHelper.SetModDataInt(TinkerChanceMod.ModId, key, value);
			}
			catch (Exception ex)
			{
				TinkerChanceMod.Log?.Warning("Could not save " + key + " (" + ex.GetType().Name + ").");
			}
		}
	}
}
