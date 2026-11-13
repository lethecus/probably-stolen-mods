using UnityEngine;

namespace CustomerCounter
{
	public static class CounterArt
	{
		public const int Width = 57;
		public const int Height = 24;

		// palette taken from the in-game clock
		private static readonly Color32 Outline = new Color32(0x3b, 0x31, 0x3d, 0xff);
		private static readonly Color32 BezelLight = new Color32(0x75, 0x70, 0x6b, 0xff);
		private static readonly Color32 BezelMid = new Color32(0x61, 0x51, 0x53, 0xff);
		private static readonly Color32 BezelShadow = new Color32(0x68, 0x5d, 0x5c, 0xff);
		private static readonly Color32 Amber = new Color32(0xcd, 0x9a, 0x72, 0xff);
		private static readonly Color32 Lcd = new Color32(0xd8, 0xc8, 0x96, 0xff);
		private static readonly Color32 LcdHi = new Color32(0xec, 0xe4, 0xc6, 0xff);
		private static readonly Color32 LcdShade = new Color32(0xc5, 0xb1, 0x82, 0xff);
		private static readonly Color32 Ink = new Color32(0x5b, 0x45, 0x46, 0xff);
		private static readonly Color32 Clear = new Color32(0, 0, 0, 0);

		private static readonly string[] Icon =
		{
			"..###..",
			".#####.",
			".#####.",
			"..###..",
			"...#...",
			".#####.",
			"#######",
			"#######",
			"#######"
		};

		// 5x9 pixel digits sized like the clock's LCD glyphs
		private static readonly string[][] Digits =
		{
			new[] { "01110", "10001", "10001", "10001", "10001", "10001", "10001", "10001", "01110" },
			new[] { "00100", "01100", "00100", "00100", "00100", "00100", "00100", "00100", "01110" },
			new[] { "01110", "10001", "00001", "00001", "00010", "00100", "01000", "10000", "11111" },
			new[] { "11111", "00001", "00010", "00100", "00010", "00001", "00001", "10001", "01110" },
			new[] { "00010", "00110", "01010", "10010", "11111", "00010", "00010", "00010", "00010" },
			new[] { "11111", "10000", "10000", "11110", "00001", "00001", "00001", "10001", "01110" },
			new[] { "00110", "01000", "10000", "10000", "11110", "10001", "10001", "10001", "01110" },
			new[] { "11111", "00001", "00010", "00010", "00100", "00100", "01000", "01000", "01000" },
			new[] { "01110", "10001", "10001", "10001", "01110", "10001", "10001", "10001", "01110" },
			new[] { "01110", "10001", "10001", "10001", "01111", "00001", "00001", "00010", "01100" }
		};

		public static Texture2D CreateTexture()
		{
			Texture2D texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
			texture.name = "CustomerCounterPanel";
			texture.filterMode = FilterMode.Point;
			texture.wrapMode = TextureWrapMode.Clamp;
			return texture;
		}

		public static void Draw(Texture2D texture, int count)
		{
			Color32[] pixels = new Color32[Width * Height];
			for (int i = 0; i < pixels.Length; i++)
			{
				pixels[i] = Clear;
			}

			DrawCasing(pixels);
			DrawIcon(pixels, 7, 7);
			DrawNumber(pixels, count, 47, 7);
			DrawDots(pixels);

			texture.SetPixels32(pixels);
			texture.Apply(false, false);
		}

		private static void DrawCasing(Color32[] pixels)
		{
			// outline
			for (int x = 0; x < Width; x++)
			{
				Px(pixels, x, 0, Outline);
				Px(pixels, x, Height - 1, Outline);
			}
			for (int y = 0; y < Height; y++)
			{
				Px(pixels, 0, y, Outline);
				Px(pixels, Width - 1, y, Outline);
			}

			// body
			for (int y = 1; y < Height - 1; y++)
			{
				for (int x = 1; x < Width - 1; x++)
				{
					Px(pixels, x, y, BezelMid);
				}
			}

			// lit rim: thick at the top-left and bottom lip
			for (int x = 1; x < Width - 1; x++)
			{
				Px(pixels, x, 1, BezelLight);
				Px(pixels, x, 2, BezelLight);
				Px(pixels, x, Height - 2, BezelLight);
				Px(pixels, x, Height - 3, BezelLight);
				Px(pixels, x, Height - 4, BezelLight);
			}
			for (int y = 1; y < Height - 1; y++)
			{
				Px(pixels, 1, y, BezelLight);
				Px(pixels, 2, y, BezelLight);
			}

			// shaded/bevelled right edge
			for (int y = 1; y < Height - 1; y++)
			{
				Px(pixels, Width - 2, y, BezelShadow);
				Px(pixels, Width - 3, y, BezelShadow);
				Px(pixels, Width - 4, y, BezelLight);
				Px(pixels, Width - 5, y, BezelLight);
			}

			// amber gasket ring
			for (int x = 4; x <= 50; x++)
			{
				Px(pixels, x, 4, Amber);
				Px(pixels, x, 18, Amber);
			}
			for (int y = 4; y <= 18; y++)
			{
				Px(pixels, 4, y, Amber);
				Px(pixels, 50, y, Amber);
			}

			// LCD face
			for (int y = 5; y <= 17; y++)
			{
				for (int x = 5; x <= 49; x++)
				{
					Px(pixels, x, y, Lcd);
				}
			}
			for (int x = 5; x <= 49; x++)
			{
				Px(pixels, x, 5, LcdHi);
				Px(pixels, x, 17, LcdShade);
			}

			// rounded outer corners
			Px(pixels, 0, 0, Clear);
			Px(pixels, Width - 1, 0, Clear);
			Px(pixels, 0, Height - 1, Clear);
			Px(pixels, Width - 1, Height - 1, Clear);
		}

		private static void DrawIcon(Color32[] pixels, int x0, int y0)
		{
			for (int y = 0; y < Icon.Length; y++)
			{
				string row = Icon[y];
				for (int x = 0; x < row.Length; x++)
				{
					if (row[x] == '#')
					{
						Px(pixels, x0 + x, y0 + y, Ink);
					}
				}
			}
		}

		private static void DrawNumber(Color32[] pixels, int count, int rightX, int y0)
		{
			int value = Mathf.Clamp(count, 0, 99);
			int ones = value % 10;
			int tens = value / 10;
			DrawDigit(pixels, ones, rightX - 4, y0);
			if (tens > 0)
			{
				DrawDigit(pixels, tens, rightX - 10, y0);
			}
		}

		private static void DrawDigit(Color32[] pixels, int digit, int x0, int y0)
		{
			string[] glyph = Digits[digit];
			for (int y = 0; y < glyph.Length; y++)
			{
				string row = glyph[y];
				for (int x = 0; x < row.Length; x++)
				{
					if (row[x] == '1')
					{
						Px(pixels, x0 + x, y0 + y, Ink);
					}
				}
			}
		}

		private static void DrawDots(Color32[] pixels)
		{
			for (int x = 35; x <= 47; x += 2)
			{
				Px(pixels, x, 16, Ink);
			}
		}

		private static void Px(Color32[] pixels, int x, int y, Color32 color)
		{
			if (x < 0 || x >= Width || y < 0 || y >= Height)
			{
				return;
			}
			pixels[(Height - 1 - y) * Width + x] = color;
		}
	}
}
