using UnityEngine;

namespace CassetteRandomize
{
	public struct Tone
	{
		public const int Range = 10;

		public int bass;
		public int treble;
		public int voice;
		public int balance;
		public bool mono;

		public string Encode()
		{
			return bass + "," + treble + "," + balance + "," + (mono ? 1 : 0) + "," + voice;
		}

		public static bool TryDecode(string text, out Tone tone)
		{
			tone = default;
			if (string.IsNullOrEmpty(text))
			{
				return false;
			}
			string[] parts = text.Split(',');
			if ((parts.Length != 4 && parts.Length != 5)
				|| !int.TryParse(parts[0], out int bass)
				|| !int.TryParse(parts[1], out int treble)
				|| !int.TryParse(parts[2], out int balance)
				|| !int.TryParse(parts[3], out int mono))
			{
				return false;
			}
			tone.bass = Mathf.Clamp(bass, -Range, Range);
			tone.treble = Mathf.Clamp(treble, -Range, Range);
			tone.balance = Mathf.Clamp(balance, -Range, Range);
			tone.mono = mono != 0;
			// Older saves and presets carry four fields; they simply have no voice band.
			tone.voice = parts.Length == 5 && int.TryParse(parts[4], out int voice) ? Mathf.Clamp(voice, -Range, Range) : 0;
			return true;
		}
	}

	// Equalizer state and presets live on the machine as tags, so they travel with
	// the save exactly like the cassette deck contents. The tag names match the
	// Hi-Fi Expansion Kit, so settings made with that mod carry over.
	internal static class DeckSettings
	{
		internal const string ToneTag = "HIFI_KIT_TONE_STRING";
		internal const string PresetsTag = "HIFI_KIT_PRESETS_STRING";
		internal const int PresetCount = 3;

		private const char PresetSeparator = '|';

		internal static Tone GetTone(GameItem machine)
		{
			if (machine == null)
			{
				return default;
			}
			Tone.TryDecode(machine.GetTagReadonly(ToneTag).GetString(), out Tone tone);
			return tone;
		}

		internal static void SetTone(GameItem machine, Tone tone)
		{
			if (machine == null)
			{
				return;
			}
			string encoded = tone.Encode();
			machine.ModifyTag(ToneTag, tag => tag.Enable().SetString(encoded), false);
			ApplyTone(machine);
		}

		private static string[] PresetSlots(GameItem machine)
		{
			string[] slots = new string[PresetCount];
			if (machine == null)
			{
				return slots;
			}
			string text = machine.GetTagReadonly(PresetsTag).GetString();
			if (string.IsNullOrEmpty(text))
			{
				return slots;
			}
			string[] saved = text.Split(PresetSeparator);
			for (int i = 0; i < PresetCount && i < saved.Length; i++)
			{
				slots[i] = saved[i];
			}
			return slots;
		}

		internal static bool TryGetPreset(GameItem machine, int slot, out Tone tone)
		{
			tone = default;
			if (slot < 0 || slot >= PresetCount)
			{
				return false;
			}
			return Tone.TryDecode(PresetSlots(machine)[slot], out tone);
		}

		internal static void SavePreset(GameItem machine, int slot, Tone tone)
		{
			if (machine == null || slot < 0 || slot >= PresetCount)
			{
				return;
			}
			string[] slots = PresetSlots(machine);
			slots[slot] = tone.Encode();
			string joined = string.Join(PresetSeparator.ToString(), slots);
			machine.ModifyTag(PresetsTag, tag => tag.Enable().SetString(joined), false);
		}

		internal static void ApplyTone(GameItem machine)
		{
			AudioFx fx = AudioFx.Ensure();
			if (fx == null)
			{
				CassetteRandomizeMod.Log?.Warning("Equalizer filter unavailable: no music AudioSource yet.");
				return;
			}
			if (machine != null && EmporiumEntry.Instance != null && machine == EmporiumEntry.Instance.cassettePlayer)
			{
				Tone tone = GetTone(machine);
				fx.SetTone(tone);
				string encoded = tone.Encode();
				if (encoded != lastApplied)
				{
					lastApplied = encoded;
					CassetteRandomizeMod.Log?.Log("Equalizer tone: bass=" + tone.bass + " treble=" + tone.treble + " voice=" + tone.voice + " balance=" + tone.balance + " mono=" + tone.mono + ".");
				}
			}
			else
			{
				fx.Bypass();
			}
		}

		private static string lastApplied;
	}
}
