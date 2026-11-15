using System;
using UnityEngine;

namespace CassetteRandomize
{
	// Bass/treble shelving, stereo balance, mono fold-down and the spectrum/wave
	// taps used by the equalizer display. Ported from the Hi-Fi Expansion Kit so
	// the whole feature lives inside this mod.
	public class AudioFx : MonoBehaviour
	{
		private struct Biquad
		{
			public float b0;
			public float b1;
			public float b2;
			public float a1;
			public float a2;
		}

		private class Settings
		{
			public bool shelves;
			public Biquad low;
			public Biquad high;
			public bool voice;
			public Biquad voiceFilter;
			public float leftGain = 1f;
			public float rightGain = 1f;
			public bool mono;
		}

		public const int Bands = 24;
		public const int ScopeSize = 128;

		private const float DbPerStep = 1.2f;
		private const float BassFrequency = 200f;
		private const float TrebleFrequency = 4000f;
		private const float VoiceFrequency = 2500f;
		private const float VoiceQ = 1f;
		private const float BandLowest = 50f;
		private const float BandQ = 4f;
		private const float DisplayTiltDbPerOctave = 3f;
		private const float DisplayFloorDb = -58f;
		private const float WindPitchForward = 0.1f;
		private const float WindPitchBackward = 0.07f;
		private const float WindPitchChangePerSecond = 0.6f;
		private const float WindHopSeconds = 0.3f;
		private const float WindHopInterval = 0.14f;
		private const float WindEndMargin = 0.25f;
		private const float SoftKnee = 0.8f;

		private static AudioFx instance;

		private volatile Settings settings;
		private AudioSource source;
		private int sampleRate;

		private readonly float[] lowZ1 = new float[2];
		private readonly float[] lowZ2 = new float[2];
		private readonly float[] highZ1 = new float[2];
		private readonly float[] highZ2 = new float[2];
		private readonly float[] voiceZ1 = new float[2];
		private readonly float[] voiceZ2 = new float[2];
		private readonly Biquad[] bandFilters = new Biquad[Bands];
		private readonly float[] bandZ1 = new float[Bands];
		private readonly float[] bandZ2 = new float[Bands];
		private readonly float[] bandSum = new float[Bands];
		private readonly float[] bandTiltDb = new float[Bands];
		private readonly float[] levels = new float[Bands];
		private readonly float[] scope = new float[ScopeSize];

		private bool winding;
		private float windPitch = 1f;
		private float lastHopTime;

		public float[] Levels => levels;

		public float[] Scope => scope;

		public bool IsPlaying => settings != null && source != null && source.isPlaying;

		public static AudioFx Ensure()
		{
			if (instance != null)
			{
				return instance;
			}
			AudioManager manager = AudioManager.Instance;
			if (manager == null || manager.audioSourceMusic == null)
			{
				return null;
			}
			instance = manager.audioSourceMusic.gameObject.AddComponent<AudioFx>();
			instance.source = manager.audioSourceMusic;
			CassetteRandomizeMod.Log?.Log("Equalizer filter attached to " + manager.audioSourceMusic.gameObject.name + ".");
			return instance;
		}

		public static void Shutdown()
		{
			if (instance != null)
			{
				UnityEngine.Object.Destroy(instance);
				instance = null;
			}
		}

		private void Awake()
		{
			sampleRate = Mathf.Max(8000, AudioSettings.outputSampleRate);
			float ratio = Mathf.Pow(280f, 1f / (Bands - 1));
			float frequency = BandLowest;
			for (int i = 0; i < Bands; i++)
			{
				bandFilters[i] = BandPass(Mathf.Min(frequency, sampleRate * 0.45f), BandQ);
				bandTiltDb[i] = DisplayTiltDbPerOctave * Mathf.Log(frequency / BandLowest, 2f);
				frequency *= ratio;
			}
		}

		public void Bypass()
		{
			settings = null;
		}

		public void SetTone(Tone tone)
		{
			Settings next = new Settings();
			next.shelves = tone.bass != 0 || tone.treble != 0;
			next.low = LowShelf(BassFrequency, tone.bass * DbPerStep);
			next.high = HighShelf(TrebleFrequency, tone.treble * DbPerStep);
			next.voice = tone.voice != 0;
			next.voiceFilter = Peaking(VoiceFrequency, VoiceQ, tone.voice * DbPerStep);
			float pan = Mathf.Clamp(tone.balance / 10f, -1f, 1f);
			next.leftGain = pan > 0f ? 1f - pan : 1f;
			next.rightGain = pan < 0f ? 1f + pan : 1f;
			next.mono = tone.mono;
			settings = next;
		}

		private void Update()
		{
			if (winding && source != null)
			{
				source.pitch = Mathf.MoveTowards(source.pitch, windPitch, WindPitchChangePerSecond * Time.unscaledDeltaTime);
				if (windPitch == 1f && Mathf.Approximately(source.pitch, 1f))
				{
					source.pitch = 1f;
					winding = false;
				}
			}
			DeckUi ui = DeckUi.Active;
			if (ui != null && ui.display.IsVisible)
			{
				ui.Tick(this, Time.unscaledDeltaTime);
			}
		}

		private void OnDisable()
		{
			if (winding && source != null)
			{
				source.pitch = 1f;
			}
			winding = false;
			windPitch = 1f;
		}

		public bool TryGetPosition(out float position, out float length)
		{
			position = 0f;
			length = 0f;
			if (source == null || source.clip == null || source.clip.length <= 0f || (!source.isPlaying && source.time <= 0f))
			{
				return false;
			}
			length = source.clip.length;
			position = Mathf.Clamp(source.time, 0f, length);
			return true;
		}

		public void Wind(float normalized, float speed)
		{
			if (source == null || source.clip == null || source.clip.length <= 0f)
			{
				return;
			}
			float length = source.clip.length;
			float target = Mathf.Clamp(Mathf.Clamp01(normalized) * length, 0f, Mathf.Max(0f, length - WindEndMargin));
			if (Mathf.Abs(target - source.time) > WindHopSeconds && Time.unscaledTime - lastHopTime > WindHopInterval)
			{
				source.time = target;
				lastHopTime = Time.unscaledTime;
			}
			speed = Mathf.Clamp(speed, -1f, 1f);
			windPitch = 1f + speed * (speed >= 0f ? WindPitchForward : WindPitchBackward);
			winding = true;
		}

		public void EndWind()
		{
			windPitch = 1f;
		}

		private void OnAudioFilterRead(float[] data, int channels)
		{
			Settings current = settings;
			if (current == null || channels < 1)
			{
				return;
			}
			int frames = data.Length / channels;
			bool stereo = channels >= 2;
			int scopeIndex = 0;
			for (int i = 0; i < frames; i++)
			{
				int baseIndex = i * channels;
				float left = data[baseIndex];
				float right = stereo ? data[baseIndex + 1] : left;
				if (current.shelves)
				{
					left = Run(ref current.low, left, ref lowZ1[0], ref lowZ2[0]);
					left = Run(ref current.high, left, ref highZ1[0], ref highZ2[0]);
					if (stereo)
					{
						right = Run(ref current.low, right, ref lowZ1[1], ref lowZ2[1]);
						right = Run(ref current.high, right, ref highZ1[1], ref highZ2[1]);
					}
					else
					{
						right = left;
					}
				}
				if (current.voice)
				{
					left = Run(ref current.voiceFilter, left, ref voiceZ1[0], ref voiceZ2[0]);
					if (stereo)
					{
						right = Run(ref current.voiceFilter, right, ref voiceZ1[1], ref voiceZ2[1]);
					}
					else
					{
						right = left;
					}
				}
				float mono = (left + right) * 0.5f;
				if (scopeIndex < ScopeSize && i % 4 == 0)
				{
					scope[scopeIndex++] = mono;
				}
				for (int band = 0; band < Bands; band++)
				{
					float sample = Run(ref bandFilters[band], mono, ref bandZ1[band], ref bandZ2[band]);
					bandSum[band] += sample * sample;
				}
				if (current.mono)
				{
					left = mono;
					right = mono;
				}
				left = Soften(left * current.leftGain);
				right = Soften(right * current.rightGain);
				data[baseIndex] = left;
				if (stereo)
				{
					data[baseIndex + 1] = right;
				}
			}
			if (frames > 0)
			{
				for (int band = 0; band < Bands; band++)
				{
					float rms = Mathf.Sqrt(bandSum[band] / frames);
					bandSum[band] = 0f;
					float db = rms > 1E-06f ? 20f * Mathf.Log10(rms) + bandTiltDb[band] : -120f;
					levels[band] = Mathf.Clamp01((db - DisplayFloorDb) / 46f);
				}
			}
		}

		private static float Run(ref Biquad filter, float input, ref float z1, ref float z2)
		{
			float output = filter.b0 * input + z1;
			z1 = filter.b1 * input - filter.a1 * output + z2;
			z2 = filter.b2 * input - filter.a2 * output;
			if (z1 > -1E-20f && z1 < 1E-20f)
			{
				z1 = 0f;
			}
			if (z2 > -1E-20f && z2 < 1E-20f)
			{
				z2 = 0f;
			}
			return output;
		}

		private static float Soften(float value)
		{
			float magnitude = value < 0f ? -value : value;
			if (magnitude <= SoftKnee)
			{
				return value;
			}
			float over = (magnitude - SoftKnee) / 0.2f;
			float limited = SoftKnee + 0.2f * (over / (1f + over));
			return value < 0f ? -limited : limited;
		}

		private Biquad LowShelf(float frequency, float gainDb)
		{
			float a = Mathf.Pow(10f, gainDb / 40f);
			float w = MathF.PI * 2f * frequency / sampleRate;
			float cos = Mathf.Cos(w);
			float alpha = Mathf.Sin(w) / 2f * Mathf.Sqrt(2f);
			float beta = 2f * Mathf.Sqrt(a) * alpha;
			float denom = a + 1f + (a - 1f) * cos + beta;
			Biquad q = default;
			q.b0 = a * (a + 1f - (a - 1f) * cos + beta) / denom;
			q.b1 = 2f * a * (a - 1f - (a + 1f) * cos) / denom;
			q.b2 = a * (a + 1f - (a - 1f) * cos - beta) / denom;
			q.a1 = -2f * (a - 1f + (a + 1f) * cos) / denom;
			q.a2 = (a + 1f + (a - 1f) * cos - beta) / denom;
			return q;
		}

		private Biquad HighShelf(float frequency, float gainDb)
		{
			float a = Mathf.Pow(10f, gainDb / 40f);
			float w = MathF.PI * 2f * Mathf.Min(frequency, sampleRate * 0.45f) / sampleRate;
			float cos = Mathf.Cos(w);
			float alpha = Mathf.Sin(w) / 2f * Mathf.Sqrt(2f);
			float beta = 2f * Mathf.Sqrt(a) * alpha;
			float denom = a + 1f - (a - 1f) * cos + beta;
			Biquad q = default;
			q.b0 = a * (a + 1f + (a - 1f) * cos + beta) / denom;
			q.b1 = -2f * a * (a - 1f + (a + 1f) * cos) / denom;
			q.b2 = a * (a + 1f + (a - 1f) * cos - beta) / denom;
			q.a1 = 2f * (a - 1f - (a + 1f) * cos) / denom;
			q.a2 = (a + 1f - (a - 1f) * cos - beta) / denom;
			return q;
		}

		// RBJ peaking EQ: boosts or cuts a band around the centre frequency while
		// leaving everything outside it untouched. Used for the voice presence band.
		private Biquad Peaking(float frequency, float quality, float gainDb)
		{
			float a = Mathf.Pow(10f, gainDb / 40f);
			float w = MathF.PI * 2f * Mathf.Min(frequency, sampleRate * 0.45f) / sampleRate;
			float cos = Mathf.Cos(w);
			float alpha = Mathf.Sin(w) / (2f * quality);
			float denom = 1f + alpha / a;
			Biquad q = default;
			q.b0 = (1f + alpha * a) / denom;
			q.b1 = -2f * cos / denom;
			q.b2 = (1f - alpha * a) / denom;
			q.a1 = -2f * cos / denom;
			q.a2 = (1f - alpha / a) / denom;
			return q;
		}

		private Biquad BandPass(float frequency, float quality)
		{
			float w = MathF.PI * 2f * frequency / sampleRate;
			float alpha = Mathf.Sin(w) / (2f * quality);
			float denom = 1f + alpha;
			Biquad q = default;
			q.b0 = alpha / denom;
			q.b1 = 0f;
			q.b2 = -alpha / denom;
			q.a1 = -2f * Mathf.Cos(w) / denom;
			q.a2 = (1f - alpha) / denom;
			return q;
		}
	}
}
