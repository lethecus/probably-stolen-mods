using System;
using TMPro;
using UnityEngine;

namespace CassetteRandomize
{
	// Equalizer panel: spectrum, seek slider with clock, volume, bass/treble/balance,
	// mono, three presets and the transport buttons moved in from the controls panel.
	public class DeckUi
	{
		private const int VolumeStep = 5;
		private const float SeekSmoothTime = 1.15f;
		private const float SeekMaxSpeed = 0.22f;
		private const float SeekCoastSeconds = 0.35f;
		private const float SeekStopSpeed = 0.004f;
		private const string NoTime = "-:-- / -:--";
		private const int TransportButtonSize = 28;
		private const int TransportButtonGap = 10;
		private const int PresetButtonWidth = 26;
		private const int SaveButtonWidth = 56;
		private const int PresetButtonHeight = 20;

		internal static DeckUi Active
		{
			get
			{
				GameItem player = EmporiumEntry.Instance != null ? EmporiumEntry.Instance.cassettePlayer : null;
				if (player == null)
				{
					return null;
				}
				return Built.TryGetValue(player, out DeckUi ui) ? ui : null;
			}
		}

		private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<GameItem, DeckUi> Built =
			new System.Runtime.CompilerServices.ConditionalWeakTable<GameItem, DeckUi>();

		public readonly MachineryStack root;
		public readonly EqualizerDisplay display;

		private readonly GameItem machine;
		private readonly MachineryText seekText;
		private readonly MachinerySlider seekSlider;
		private readonly MachineryText volumeText;
		private readonly MachinerySlider volumeSlider;
		private readonly MachineryText bassText;
		private readonly MachinerySlider bassSlider;
		private readonly MachineryText trebleText;
		private readonly MachinerySlider trebleSlider;
		private readonly MachineryText balanceText;
		private readonly MachinerySlider balanceSlider;
		private readonly MachineryRadioButton monoSwitch;
		private readonly MachineryButton[] presetButtons = new MachineryButton[DeckSettings.PresetCount];
		private readonly MachineryButton saveButton;

		private bool saving;
		private bool refreshing;
		private bool seeking;
		private bool coasting;
		private float seekTarget;
		private float seekPosition;
		private float seekSpeed;
		private float followPosition;
		private string shownTime;

		public DeckUi(GameItem machine, PixelElement transport)
		{
			this.machine = machine;
			display = new EqualizerDisplay();
			seekText = new MachineryText(NoTime, 6, HorizontalAlignmentOptions.Center, false);
			seekSlider = new MachinerySlider(0f, -1).SetRange(0f, 1f, 0f);
			seekSlider.onChanged = OnSeek;

			volumeText = NewText();
			volumeSlider = new MachinerySlider(0f, -1).SetRange(0f, 100f, VolumeStep);
			volumeSlider.onChanged = OnVolumeChanged;
			volumeSlider.onReleased = _ => SaveVolume();

			bassText = NewText();
			bassSlider = NewToneSlider();
			trebleText = NewText();
			trebleSlider = NewToneSlider();
			balanceText = NewText();
			balanceSlider = NewToneSlider();

			monoSwitch = new MachineryRadioButton("Mono", false, _ => OnToneChanged());
			monoSwitch.SetDescription("Plays both channels as one.");

			MachineryStack transportStack = transport as MachineryStack;
			MachineryStack transportTop = null;
			MachineryStack transportBottom = null;
			if (transportStack != null)
			{
				transportStack.gap = TransportButtonGap;
				transportTop = new MachineryStack(false, TransportButtonGap);
				transportBottom = new MachineryStack(false, TransportButtonGap);
				int index = 0;
				foreach (PixelElement element in transportStack.elements)
				{
					MachineryButton button = element as MachineryButton;
					if (button == null)
					{
						continue;
					}
					bool labelled = button.handler != null && button.handler.text != null && !string.IsNullOrEmpty(button.handler.text.text);
					button.SetSize(labelled ? 96 : TransportButtonSize, TransportButtonSize);
					(index < 3 ? transportTop : transportBottom).Add(button);
					index++;
				}
				transportStack.Validate();
			}

			MachineryStack presetRow = new MachineryStack(false, 4);
			presetRow.Add(monoSwitch);
			for (int i = 0; i < DeckSettings.PresetCount; i++)
			{
				int slot = i;
				presetButtons[i] = new MachineryButton((i + 1).ToString(), () => OnPresetPressed(slot), PresetButtonWidth, PresetButtonHeight);
				presetButtons[i].SetFillWidth(false);
				presetRow.Add(presetButtons[i]);
			}
			saveButton = new MachineryButton("Save", OnSavePressed, SaveButtonWidth, PresetButtonHeight);
			saveButton.SetFillWidth(false);
			presetRow.Add(saveButton);

			MachineryStack volumeColumn = new MachineryStack(true, 3);
			volumeColumn.Add(volumeText);
			volumeColumn.Add(volumeSlider);
			volumeColumn.Add(balanceText);
			volumeColumn.Add(balanceSlider);

			MachineryStack toneColumn = new MachineryStack(true, 3);
			toneColumn.Add(bassText);
			toneColumn.Add(bassSlider);
			toneColumn.Add(trebleText);
			toneColumn.Add(trebleSlider);

			MachineryStack sliders = new MachineryStack(false, 8);
			sliders.Add(volumeColumn);
			sliders.Add(toneColumn);

			root = new MachineryStack(true, 4);
			root.Add(display);
			root.Add(seekSlider);
			root.Add(seekText);
			if (transportTop != null)
			{
				root.Add(transportTop);
			}
			if (transportBottom != null && transportBottom.elements.Count > 0)
			{
				root.Add(transportBottom);
			}
			root.Add(sliders);
			root.Add(presetRow);

			Built.Remove(machine);
			Built.Add(machine, this);
			Refresh();
		}

		private static MachineryText NewText()
		{
			return new MachineryText("", 6, HorizontalAlignmentOptions.Left, false);
		}

		private MachinerySlider NewToneSlider()
		{
			MachinerySlider slider = new MachinerySlider(0f, -1).SetRange(-Tone.Range, Tone.Range, 1f);
			slider.onChanged = _ => OnToneChanged();
			return slider;
		}

		public void Refresh()
		{
			refreshing = true;
			try
			{
				if (EmporiumOption.Instance != null)
				{
					volumeSlider.SetValue(Mathf.Round(EmporiumOption.Instance.musicVolume * 100f), false);
				}
				ShowTone(DeckSettings.GetTone(machine));
				saving = false;
				RefreshTexts();
				RefreshPresetButtons();
			}
			finally
			{
				refreshing = false;
			}
		}

		public void Tick(AudioFx fx, float deltaTime)
		{
			bool playing = fx.IsPlaying;
			display.Animate(playing ? fx.Levels : null, playing ? fx.Scope : null, deltaTime);
			string text = NoTime;
			if (!fx.TryGetPosition(out float position, out float length))
			{
				if (seeking)
				{
					seeking = false;
					fx.EndWind();
				}
				followPosition = 0f;
				seekSlider.SetValue(0f, false);
			}
			else if (seeking)
			{
				if (!coasting && !Input.GetMouseButton(0))
				{
					coasting = true;
					seekTarget = Mathf.Clamp01(seekPosition + seekSpeed * SeekCoastSeconds);
				}
				seekPosition = Mathf.SmoothDamp(seekPosition, seekTarget, ref seekSpeed, SeekSmoothTime, SeekMaxSpeed, deltaTime);
				seekPosition = Mathf.Clamp01(seekPosition);
				seekSlider.SetValue(seekPosition, false);
				fx.Wind(seekPosition, seekSpeed / SeekMaxSpeed);
				if (coasting && Mathf.Abs(seekSpeed) < SeekStopSpeed)
				{
					seeking = false;
					fx.EndWind();
				}
				text = Clock(seekPosition * length) + " / " + Clock(length);
			}
			else
			{
				followPosition = position / length;
				seekSlider.SetValue(followPosition, false);
				text = Clock(position) + " / " + Clock(length);
			}
			if (text != shownTime)
			{
				shownTime = text;
				seekText.SetText(text);
			}
		}

		private static string Clock(float seconds)
		{
			int whole = Mathf.Max(0, Mathf.FloorToInt(seconds));
			return whole / 60 + ":" + (whole % 60).ToString("00");
		}

		private void ShowTone(Tone tone)
		{
			bassSlider.SetValue(tone.bass, false);
			trebleSlider.SetValue(tone.treble, false);
			balanceSlider.SetValue(tone.balance, false);
			monoSwitch.SetOn(tone.mono, false);
		}

		private Tone ReadTone()
		{
			return new Tone
			{
				bass = bassSlider.GetInt(),
				treble = trebleSlider.GetInt(),
				balance = balanceSlider.GetInt(),
				mono = monoSwitch.isOn
			};
		}

		private static string Signed(int value)
		{
			return value <= 0 ? value.ToString() : "+" + value;
		}

		private void RefreshTexts()
		{
			volumeText.SetText("Volume: " + volumeSlider.GetInt() + "%");
			bassText.SetText("Bass: " + Signed(bassSlider.GetInt()));
			trebleText.SetText("Treble: " + Signed(trebleSlider.GetInt()));
			int balance = balanceSlider.GetInt();
			string side = balance == 0
				? "Center"
				: (balance < 0 ? "Left " + -balance : "Right " + balance);
			balanceText.SetText("Balance: " + side);
		}

		private void RefreshPresetButtons()
		{
			saveButton.SetText(saving ? "Slot?" : "Save");
			saveButton.SetDescription("Press, then pick a slot to store the current settings.");
			for (int i = 0; i < DeckSettings.PresetCount; i++)
			{
				bool filled = DeckSettings.TryGetPreset(machine, i, out _);
				string description = saving
					? "Store the current settings in slot " + (i + 1) + "."
					: (filled ? "Load preset " + (i + 1) + "." : "Preset " + (i + 1) + " is empty. Press Save, then this button.");
				presetButtons[i].SetDescription(description);
			}
			root.Validate();
		}

		private void OnSeek(float value)
		{
			if (!seeking)
			{
				seeking = true;
				seekPosition = followPosition;
				seekSpeed = 0f;
			}
			coasting = false;
			seekTarget = Mathf.Clamp01(value);
			seekSlider.SetValue(seekPosition, false);
		}

		private void OnVolumeChanged(float value)
		{
			if (refreshing || EmporiumOption.Instance == null)
			{
				return;
			}
			EmporiumOption.Instance.musicVolume = Mathf.Clamp01(value / 100f);
			EmporiumOption.Instance.ApplyMusicVolume();
			RefreshTexts();
		}

		private void SaveVolume()
		{
			if (EmporiumOption.Instance != null)
			{
				EmporiumOption.Instance.SaveMusicVolume();
			}
		}

		private void OnToneChanged()
		{
			if (refreshing)
			{
				return;
			}
			DeckSettings.SetTone(machine, ReadTone());
			RefreshTexts();
		}

		private void OnSavePressed()
		{
			saving = !saving;
			RefreshPresetButtons();
		}

		private void OnPresetPressed(int slot)
		{
			if (saving)
			{
				DeckSettings.SavePreset(machine, slot, ReadTone());
				saving = false;
				RefreshPresetButtons();
				return;
			}
			if (!DeckSettings.TryGetPreset(machine, slot, out Tone tone))
			{
				return;
			}
			refreshing = true;
			try
			{
				ShowTone(tone);
			}
			finally
			{
				refreshing = false;
			}
			DeckSettings.SetTone(machine, tone);
			RefreshTexts();
		}
	}
}
