using System;
using System.Collections.Generic;
using UnityEngine;

namespace AutoSorterPlus
{
	public class Core : IMod
	{
		internal const string ModId = "auto_sorter_plus";

		internal static ModLog Log;

		internal static ConfigEntry<string> SortHotkey;
		internal static ConfigEntry<string> OverviewHotkey;
		internal static ConfigEntry<string> SettingsHotkey;
		internal static ConfigEntry<string> ToggleHotkey;

		private GameObject _updater;
		private static bool _capturing;

		internal static string Loc(string key, params object[] args)
		{
			try
			{
				return ModHelper.GetLocalized(ModId, key, args);
			}
			catch
			{
				return key;
			}
		}

		internal static string GetModData(string key)
		{
			return ModHelper.GetModData(ModId, key, null);
		}

		internal static void SetModData(string key, string value)
		{
			ModHelper.SetModData(ModId, key, value);
		}

		internal static List<string> ModDataKeys()
		{
			return ModHelper.GetModDataKeys(ModId) ?? new List<string>();
		}

		internal static void LogMsg(string message)
		{
			Log?.Log(message);
		}

		internal static void LogWarning(string message)
		{
			Log?.Warning(message);
		}

		internal static void LogError(string message)
		{
			Log?.Error(message);
		}

		public void Init(ModManifest manifest)
		{
			Log = new ModLog(manifest);
			ModConfig config = ModHelper.GetConfig(ModId);
			SortHotkey = config.Define("SortHotkey", "Ctrl+R", "Hotkeys", () => Loc("asp.hotkey.sort"));
			OverviewHotkey = config.Define("OverviewHotkey", "Ctrl+L", "Hotkeys", () => Loc("asp.hotkey.overview"));
			SettingsHotkey = config.Define("SettingsHotkey", "Mouse1", "Hotkeys", () => Loc("asp.hotkey.settings"));
			ToggleHotkey = config.Define("ToggleHotkey", "Ctrl+Shift+O", "Hotkeys", () => Loc("asp.hotkey.toggle"));
			LogMsg("v" + manifest.ModVersion + " loaded.");
		}

		public void OnEnable()
		{
			ModHook.OnGameLoadedNormal += OnGameLoaded;
			_updater = new GameObject("AutoSorterPlus.Updater");
			UnityEngine.Object.DontDestroyOnLoad(_updater);
			_updater.AddComponent<Updater>();
		}

		public void OnDisable()
		{
			ModHook.OnGameLoadedNormal -= OnGameLoaded;
			UI.CloseAll();
			if (_updater != null)
			{
				UnityEngine.Object.Destroy(_updater);
			}
			_updater = null;
		}

		private static void OnGameLoaded()
		{
			Rules.ResetForSave();
		}

		internal static void Tick()
		{
			try
			{
				if (!PlayerStore.HasInstance)
				{
					return;
				}
				Rules.Load();
				if (UI.IsCapturing)
				{
					return;
				}
				if (Down(SortHotkey))
				{
					Engine.Sort();
				}
				else if (Down(OverviewHotkey))
				{
					UI.ToggleOverview();
				}
				else if (Down(ToggleHotkey))
				{
					Rules.GlobalEnabled = !Rules.GlobalEnabled;
					LogMsg(Loc(Rules.GlobalEnabled ? "asp.log.globalOn" : "asp.log.globalOff"));
				}
				else if (Down(SettingsHotkey))
				{
					UI.TryOpenFromClick();
				}
			}
			catch (Exception exception)
			{
				LogError("Tick: " + exception);
			}
		}

		private static bool Down(ConfigEntry<string> entry)
		{
			if (entry == null || _capturing || string.IsNullOrEmpty(entry.Value))
			{
				return false;
			}
			string[] parts = entry.Value.Split('+');
			bool ctrl = false;
			bool alt = false;
			bool shift = false;
			int mouse = -1;
			KeyCode key = KeyCode.None;
			for (int i = 0; i < parts.Length; i++)
			{
				string part = parts[i].Trim();
				if (part.Equals("Ctrl", StringComparison.OrdinalIgnoreCase))
				{
					ctrl = true;
				}
				else if (part.Equals("Alt", StringComparison.OrdinalIgnoreCase))
				{
					alt = true;
				}
				else if (part.Equals("Shift", StringComparison.OrdinalIgnoreCase))
				{
					shift = true;
				}
				else if (part.StartsWith("Mouse", StringComparison.OrdinalIgnoreCase)
					&& int.TryParse(part.Substring(5), out int button))
				{
					mouse = button;
				}
				else if (!Enum.TryParse(part, true, out key))
				{
					key = KeyCode.None;
				}
			}
			bool ctrlHeld = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
			bool altHeld = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
			bool shiftHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
			if (ctrl != ctrlHeld || alt != altHeld || shift != shiftHeld)
			{
				return false;
			}
			if (mouse >= 0)
			{
				return Input.GetMouseButtonDown(mouse);
			}
			return key != KeyCode.None && Input.GetKeyDown(key);
		}

		internal sealed class Updater : MonoBehaviour
		{
			private void Update()
			{
				Tick();
			}
		}
	}
}
