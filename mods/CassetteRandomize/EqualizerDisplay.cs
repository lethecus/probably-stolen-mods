using UnityEngine;
using UnityEngine.UI;

namespace CassetteRandomize
{
	// Spectrum bars plus a small oscilloscope, drawn from the AudioFx taps.
	public class EqualizerDisplay : PixelElement, MachineryWidget
	{
		private const int Height = 72;
		private const int WaveHeight = 24;
		private const int DividerGap = 2;
		private const int BarGap = 1;
		private const int MinWidth = 200;
		private const int WaveStep = 2;
		private const int MaxWavePoints = 160;
		private const float WaveGain = 2.2f;
		private const float FallPerSecond = 1.6f;
		private const float PeakFallPerSecond = 0.45f;
		private const float PeakHoldSeconds = 0.35f;

		private static readonly Color BackgroundColor = MachineryStyle.SLIDER_EMPTY;
		private static readonly Color WaveColor = MachineryStyle.Hex("c4c4b0", 1f);
		private static readonly Color DividerColor = MachineryStyle.Hex("373541", 1f);

		private readonly TreeNodeRender frame;
		private readonly TreeNodeRender backdrop;
		private readonly TreeNodeRender divider;
		private readonly TreeNodeRender[] bars = new TreeNodeRender[AudioFx.Bands];
		private readonly TreeNodeRender[] peaks = new TreeNodeRender[AudioFx.Bands];
		private readonly TreeNodeRender[] wave = new TreeNodeRender[MaxWavePoints];
		private readonly float[] shown = new float[AudioFx.Bands];
		private readonly float[] peak = new float[AudioFx.Bands];
		private readonly float[] peakHold = new float[AudioFx.Bands];
		private readonly int[] waveY = new int[MaxWavePoints];

		private int requestedWidth;

		public EqualizerDisplay()
		{
			handler = TreeNodeRender.Instantiate(this, "CassetteEqualizerDisplay");
			backdrop = NewFlat(BackgroundColor);
			divider = NewFlat(DividerColor);
			for (int i = 0; i < AudioFx.Bands; i++)
			{
				bars[i] = NewFlat(MachineryStyle.PROGRESS_FILL);
				peaks[i] = NewFlat(MachineryStyle.VALUE_TEXT);
			}
			for (int i = 0; i < MaxWavePoints; i++)
			{
				wave[i] = NewFlat(WaveColor);
			}
			frame = TreeNodeRender.Instantiate().SetParent(handler);
			MachineryStyle.SetSliced(frame, MachineryStyle.ProgressFrame);
			frame.image.fillCenter = false;
			frame.image.raycastTarget = false;
			Validate();
		}

		private TreeNodeRender NewFlat(Color color)
		{
			TreeNodeRender node = TreeNodeRender.Instantiate().SetParent(handler);
			MachineryStyle.SetFlat(node, color);
			node.image.raycastTarget = false;
			return node;
		}

		public void Animate(float[] levels, float[] samples, float deltaTime)
		{
			for (int i = 0; i < AudioFx.Bands; i++)
			{
				float target = levels != null ? Mathf.Clamp01(levels[i]) : 0f;
				shown[i] = target > shown[i] ? target : Mathf.Max(target, shown[i] - FallPerSecond * deltaTime);
				if (shown[i] >= peak[i])
				{
					peak[i] = shown[i];
					peakHold[i] = PeakHoldSeconds;
				}
				else if (peakHold[i] > 0f)
				{
					peakHold[i] -= deltaTime;
				}
				else
				{
					peak[i] = Mathf.Max(shown[i], peak[i] - PeakFallPerSecond * deltaTime);
				}
			}
			PlaceBars();
			PlaceWave(samples);
		}

		private void PlaceBars()
		{
			int top = 32;
			int height = heightPixels - 3 - top;
			int inner = widthPixels - 6;
			int barWidth = Mathf.Max(1, (inner - (AudioFx.Bands - 1)) / AudioFx.Bands);
			int used = barWidth * AudioFx.Bands + (AudioFx.Bands - 1);
			int left = 3 + Mathf.Max(0, (inner - used) / 2);
			for (int i = 0; i < AudioFx.Bands; i++)
			{
				int x = left + i * (barWidth + BarGap);
				int barHeight = Mathf.RoundToInt(shown[i] * height);
				bars[i].image.enabled = barHeight > 0;
				bars[i].image.color = shown[i] > 0.85f
					? MachineryStyle.PROGRESS_FILL_DANGER
					: (shown[i] > 0.65f ? MachineryStyle.PROGRESS_FILL_WARNING : MachineryStyle.PROGRESS_FILL);
				MachineryStyle.Place(bars[i], x, top + height - barHeight, barWidth, barHeight);
				int peakHeight = Mathf.RoundToInt(peak[i] * height);
				peaks[i].image.enabled = peakHeight > 1 && peakHeight > barHeight;
				MachineryStyle.Place(peaks[i], x, top + height - peakHeight, barWidth, 1);
			}
		}

		private void PlaceWave(float[] samples)
		{
			int inner = widthPixels - 6;
			int pointCount = Mathf.Min(MaxWavePoints, inner / WaveStep);
			const int center = 15;
			const int amplitude = 11;
			for (int i = 0; i < pointCount; i++)
			{
				float value = 0f;
				if (samples != null && samples.Length != 0)
				{
					value = samples[i * samples.Length / pointCount];
				}
				waveY[i] = center - Mathf.RoundToInt(Mathf.Clamp(value * WaveGain, -1f, 1f) * amplitude);
			}
			for (int i = 0; i < MaxWavePoints; i++)
			{
				if (i >= pointCount)
				{
					wave[i].image.enabled = false;
					continue;
				}
				int previous = i > 0 ? waveY[i - 1] : waveY[i];
				int y = Mathf.Min(previous, waveY[i]);
				int height = Mathf.Abs(previous - waveY[i]) + 1;
				wave[i].image.enabled = true;
				MachineryStyle.Place(wave[i], 3 + i * WaveStep, y, WaveStep, height);
			}
		}

		public void Validate()
		{
			widthPixels = Mathf.Max(MinWidth, fillWidth ? requestedWidth : 0);
			heightPixels = Height;
			MachineryStyle.Place(backdrop, 1, 1, widthPixels - 2, heightPixels - 2);
			MachineryStyle.Place(divider, 3, 29, widthPixels - 6, 1);
			MachineryStyle.Place(frame, 0, 0, widthPixels, heightPixels);
			PlaceBars();
			PlaceWave(null);
		}

		public void ResizePixels(int width, int height)
		{
			requestedWidth = width;
			Validate();
		}

		public bool Attach(PixelElement element)
		{
			return false;
		}

		public bool Detach(PixelElement element)
		{
			return false;
		}

		public bool canResize => false;

		public int widthPixels { get; private set; }

		public int heightPixels { get; private set; }

		public TreeNodeRender handler { get; private set; }

		public RectTransform rectTransform => handler.rectTransform;

		public TreeNodeRender inner => handler;

		public bool fillWidth { get; set; } = true;

		public bool IsVisible => handler != null && handler.gameObject.activeInHierarchy;

		public int WidthPixelsClamp(int width)
		{
			return widthPixels;
		}

		public int HeightPixelsClamp(int height)
		{
			return heightPixels;
		}

		public int XClamp(int x)
		{
			return 0;
		}

		public int YClamp(int y)
		{
			return 0;
		}
	}
}
