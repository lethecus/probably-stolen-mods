using System;
using UnityEngine;

namespace TinkerChance
{
	/// <summary>
	/// Persisted settings: the keep chance and the settings hotkey.
	///
	/// These live in the mod's own data folder as a small key=value file rather than in the run's
	/// mod data, because run data is only written when the player saves the game and is tied to one
	/// save slot. A file is available from the moment the mod is enabled, so the chance is already in
	/// effect before any save is loaded, and it survives quitting without saving.
	/// </summary>
	internal static class TinkerSettings
	{
		internal const int DefaultChance = 20;

		internal const KeyCode DefaultHotkey = KeyCode.F4;

		private const string FileName = "settings.txt";

		internal static int Chance { get; private set; } = DefaultChance;

		internal static KeyCode Hotkey { get; private set; } = DefaultHotkey;

		internal static bool Loaded { get; private set; }

		internal static void Load()
		{
			int chance = DefaultChance;
			KeyCode hotkey = DefaultHotkey;

			try
			{
				string text = ModHelper.ReadModDataFileText(TinkerChanceMod.ModId, FileName);
				if (!string.IsNullOrEmpty(text))
				{
					foreach (string rawLine in text.Split('\n'))
					{
						string line = rawLine.Trim();
						if (line.Length == 0 || line[0] == '#')
						{
							continue;
						}

						int separator = line.IndexOf('=');
						if (separator <= 0)
						{
							continue;
						}

						string key = line.Substring(0, separator).Trim();
						string value = line.Substring(separator + 1).Trim();

						if (key == "chance")
						{
							int parsed;
							if (int.TryParse(value, out parsed))
							{
								chance = Mathf.Clamp(parsed, 0, 100);
							}
						}
						else if (key == "hotkey")
						{
							KeyCode parsedKey;
							if (Enum.TryParse(value, out parsedKey) && parsedKey != KeyCode.None && Enum.IsDefined(typeof(KeyCode), parsedKey))
							{
								hotkey = parsedKey;
							}
						}
					}
				}

				Chance = chance;
				Hotkey = hotkey;
				Loaded = true;
			}
			catch (Exception ex)
			{
				Chance = DefaultChance;
				Hotkey = DefaultHotkey;
				Loaded = false;
				TinkerChanceMod.Log?.Warning("Could not read settings, using defaults (" + ex.GetType().Name + ").");
			}
		}

		internal static void SetChance(int value)
		{
			Chance = Mathf.Clamp(value, 0, 100);
			Save();
		}

		internal static void SetHotkey(KeyCode key)
		{
			Hotkey = key;
			Save();
		}

		private static void Save()
		{
			try
			{
				ModHelper.WriteModDataFileText(TinkerChanceMod.ModId, FileName, "chance=" + Chance + "\nhotkey=" + Hotkey + "\n");
			}
			catch (Exception ex)
			{
				TinkerChanceMod.Log?.Warning("Could not save settings (" + ex.GetType().Name + ").");
			}
		}
	}
}
