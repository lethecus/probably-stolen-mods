using System;
using UnityEngine;

namespace TinkerChance
{
	/// <summary>
	/// Owns the settings window and the hotkey.
	///
	/// The window is drawn with IMGUI rather than the game's PixelWindow: a standalone PixelWindow
	/// cannot be constructed outside the game's UI graph (TreeNodeRender throws NullReferenceException
	/// in its constructor), and this mod only needs a slider and two buttons.
	/// </summary>
	internal sealed class TinkerSettingsHost : MonoBehaviour
	{
		private const int WindowId = 0x7A1C;

		private Rect windowRect = new Rect(120f, 120f, 340f, 196f);

		private bool visible;

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
				visible = !visible;
			}
			else if (visible && Input.GetKeyDown(KeyCode.Escape))
			{
				visible = false;
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
				return;
			}
		}

		private void OnGUI()
		{
			if (!visible)
			{
				return;
			}
			windowRect = GUI.Window(WindowId, windowRect, DrawWindow, "Tinkerer Chance");
		}

		private void DrawWindow(int id)
		{
			GUILayout.Space(6f);
			GUILayout.Label("Keep chance: " + TinkerSettings.Chance + "%");

			int picked = Mathf.RoundToInt(GUILayout.HorizontalSlider(TinkerSettings.Chance, 0f, 100f));
			if (picked != TinkerSettings.Chance)
			{
				TinkerSettings.SetChance(picked);
			}

			GUILayout.Space(6f);
			GUILayout.Label("Hotkey: " + (capturing ? "press a key..." : TinkerSettings.Hotkey.ToString()));

			if (GUILayout.Button(capturing ? "Cancel rebind" : "Change hotkey"))
			{
				capturing = !capturing;
			}

			if (GUILayout.Button("Reset to " + TinkerSettings.DefaultChance + "%"))
			{
				TinkerSettings.SetChance(TinkerSettings.DefaultChance);
			}

			GUILayout.Space(4f);
			if (GUILayout.Button("Close"))
			{
				visible = false;
			}

			GUI.DragWindow(new Rect(0f, 0f, 10000f, 22f));
		}
	}
}
