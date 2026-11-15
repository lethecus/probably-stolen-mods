using System;
using UnityEngine;

namespace TinkerChance
{
	/// <summary>Persisted settings: the keep chance and the settings hotkey.</summary>
	internal static class TinkerSettings
	{
		internal const int DefaultChance = 20;

		internal const KeyCode DefaultHotkey = KeyCode.F4;

		internal static int Chance { get; private set; } = DefaultChance;

		internal static KeyCode Hotkey { get; private set; } = DefaultHotkey;

		internal static void Load()
		{
			Chance = Mathf.Clamp(ModHelper.GetModDataInt(TinkerChanceMod.ModId, "chance", DefaultChance), 0, 100);
			int raw = ModHelper.GetModDataInt(TinkerChanceMod.ModId, "hotkey", (int)DefaultHotkey);
			Hotkey = Enum.IsDefined(typeof(KeyCode), raw) ? (KeyCode)raw : DefaultHotkey;
		}

		internal static void SetChance(int value)
		{
			Chance = Mathf.Clamp(value, 0, 100);
			ModHelper.SetModDataInt(TinkerChanceMod.ModId, "chance", Chance);
		}

		internal static void SetHotkey(KeyCode key)
		{
			Hotkey = key;
			ModHelper.SetModDataInt(TinkerChanceMod.ModId, "hotkey", (int)key);
		}
	}
}
