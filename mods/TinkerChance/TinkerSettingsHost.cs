using System;
using UnityEngine;

namespace TinkerChance
{
	/// <summary>
	/// Owns the settings window and the hotkey. Created by the mod on enable and destroyed on disable,
	/// so nothing survives the mod being switched off.
	/// </summary>
	internal sealed class TinkerSettingsHost : MonoBehaviour
	{
		private PixelWindow window;

		private MachineryText chanceLabel;

		private MachineryText hotkeyLabel;

		private MachinerySlider slider;

		private bool capturing;

		private void Update()
		{
			if (capturing)
			{
				if (Input.anyKeyDown)
				{
					CaptureKey();
				}
				return;
			}

			if (Input.GetKeyDown(TinkerSettings.Hotkey))
			{
				Toggle();
			}
			else if (window != null && Input.GetKeyDown(KeyCode.Escape))
			{
				Close();
			}
		}

		private void CaptureKey()
		{
			foreach (KeyCode key in Enum.GetValues(typeof(KeyCode)))
			{
				if (key == KeyCode.None)
				{
					continue;
				}
				string name = key.ToString();
				if (name.StartsWith("Mouse", StringComparison.Ordinal) || name.StartsWith("Joystick", StringComparison.Ordinal))
				{
					continue;
				}
				if (!Input.GetKeyDown(key))
				{
					continue;
				}

				TinkerSettings.SetHotkey(key);
				capturing = false;
				UpdateHotkeyLabel();
				return;
			}
		}

		private void Toggle()
		{
			if (window == null)
			{
				Open();
			}
			else
			{
				Close();
			}
		}

		private void Open()
		{
			window = new PixelWindow(canMove: true, "Tinkerer Chance", WindowStyle.Machinery);
			MachineryPanelRow row = new MachineryPanelRow();
			window.Attach(row);

			chanceLabel = new MachineryText(null, 10);
			UpdateChanceLabel();

			slider = new MachinerySlider(TinkerSettings.Chance, 150);
			slider.SetRange(0f, 100f, 1f);
			slider.onChanged = delegate(float value)
			{
				TinkerSettings.SetChance(Mathf.RoundToInt(value));
				UpdateChanceLabel();
			};
			slider.onReleased = delegate(float value)
			{
				TinkerSettings.SetChance(Mathf.RoundToInt(value));
				UpdateChanceLabel();
			};

			hotkeyLabel = new MachineryText(null, 10);
			UpdateHotkeyLabel();

			MachineryButton rebind = new MachineryButton("Change hotkey", delegate
			{
				capturing = true;
				hotkeyLabel.SetText("Press a key...");
			}, -1, -1);

			MachineryButton reset = new MachineryButton("Reset to 20%", delegate
			{
				TinkerSettings.SetChance(TinkerSettings.DefaultChance);
				if (slider != null)
				{
					slider.SetInt(TinkerSettings.DefaultChance);
				}
				UpdateChanceLabel();
			}, -1, -1);

			MachineryButton close = new MachineryButton("Close", Close, -1, -1);

			MachineryStack stack = MachineryStack.Vertical(chanceLabel, slider, hotkeyLabel, rebind, reset, close);
			row.Add(MachineHelper.Panel("Tinkerer", stack, "control", true));
			window.Show();
		}

		private void UpdateChanceLabel()
		{
			if (chanceLabel != null)
			{
				chanceLabel.SetText("Keep chance: " + TinkerSettings.Chance + "%");
			}
		}

		private void UpdateHotkeyLabel()
		{
			if (hotkeyLabel != null)
			{
				hotkeyLabel.SetText("Hotkey: " + TinkerSettings.Hotkey);
			}
		}

		private void Close()
		{
			capturing = false;
			if (window != null)
			{
				window.Destroy();
				window = null;
			}
		}

		private void OnDestroy()
		{
			Close();
		}
	}
}
