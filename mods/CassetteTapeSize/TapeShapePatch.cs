using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace CassetteTapeSize
{
	internal static class TapeShape
	{
		internal const string CassetteTag = "CASSETTE_TAG";

		private static bool logged;

		internal static bool IsTape(GameItem item)
		{
			return item != null && item.IsTag(CassetteTag);
		}

		// A tape sprite is authored on a 2x2 slice of the atlas, so the built shape
		// occupies four cells. Anything with a footprint wider or taller than one
		// cell is compacted down to a single cell.
		internal static bool TooBig(GridShape shape)
		{
			return shape != null && (shape.globalWidth > 1 || shape.globalHeight > 1);
		}

		// Keep the top-left cell the item already occupies so shrinking never moves
		// it into another item's space.
		internal static GridShape Compact(GridShape shape)
		{
			GridShape compact = new GridShapeBuilder(1, 1)
				.SetDataFill(1)
				.SetPosition(shape.minX, shape.minY)
				.Build();
			if (!logged)
			{
				logged = true;
				CompactCassettesMod.Log?.Log("Cassette tape shapes are now 1x1 (was " + shape.globalWidth + "x" + shape.globalHeight + ").");
			}
			return compact;
		}

		// Safety net for anything that reached a live inventory without passing
		// through the setter guard (for example an item restored straight from a
		// save graph). Runs once per load and reports what it found.
		internal static void CompactLoadedTapes()
		{
			EmporiumEntry entry = EmporiumEntry.Instance;
			if (entry == null)
			{
				return;
			}
			List<GameItem> items = new List<GameItem>();
			try
			{
				items.AddRange(entry.GetAllItems());
			}
			catch
			{
			}
			GameItem player = entry.cassettePlayer;
			if (player != null)
			{
				items.Add(player);
				items.AddRange(GraphUtils.FindAllChildrenType<GameItem>(player));
			}

			int seen = 0;
			int resized = 0;
			foreach (GameItem item in items)
			{
				if (!IsTape(item))
				{
					continue;
				}
				seen++;
				GridShape shape = item.shape;
				if (TooBig(shape))
				{
					item.SetShape(Compact(shape));
					resized++;
				}
				else if (TooBig(item.modifiedShape))
				{
					item.ValidateShapeState();
					resized++;
				}
			}

			int remaining = 0;
			foreach (GameItem item in items)
			{
				if (IsTape(item) && (TooBig(item.shape) || TooBig(item.modifiedShape)))
				{
					remaining++;
				}
			}
			CompactCassettesMod.Log?.Log(
				"Compact cassettes load pass: " + seen + " tape(s) seen, " + resized + " resized, " + remaining + " still larger than 1x1.");
		}
	}

	// Every shape assignment on a cassette item is funneled through this setter,
	// which covers the save/load decode path where the stored 2x2 shape is written
	// straight onto the item.
	[HarmonyPatch(typeof(GameItem), "set_shape")]
	internal static class ShapeSetterPatch
	{
		private static void Prefix(GameItem __instance, ref GridShape value)
		{
			if (TapeShape.IsTape(__instance) && TapeShape.TooBig(value))
			{
				value = TapeShape.Compact(value);
			}
		}
	}

	// The decode path also writes modifiedShape straight from the save, so the
	// same guard is needed there; anything computed from an already compact shape
	// is 1x1 and passes through untouched.
	[HarmonyPatch(typeof(GameItem), "set_modifiedShape")]
	internal static class ModifiedShapeSetterPatch
	{
		private static void Prefix(GameItem __instance, ref GridShape value)
		{
			if (TapeShape.IsTape(__instance) && TapeShape.TooBig(value))
			{
				value = TapeShape.Compact(value);
			}
		}
	}

	// The item element draws its sprite at the atlas's native size, so a compacted
	// tape would still paint two cells wide and overlap its neighbours. Fit the art
	// to the single cell it now occupies (2x2 art scales down uniformly).
	[HarmonyPatch(typeof(GameItemElement), "ValidateSelectLocked")]
	internal static class TapeArtPatch
	{
		private static void Postfix(GameItemElement __instance)
		{
			if (!TapeShape.IsTape(__instance))
			{
				return;
			}
			GridShape shape = __instance.modifiedShape;
			if (shape == null || shape.width != 1 || shape.height != 1)
			{
				return;
			}
			TreeNodeRender background = __instance.background;
			Sprite sprite = background?.image?.sprite;
			if (sprite == null)
			{
				return;
			}
			int gridSize = UISettings.current.gridSize;
			if (sprite.rect.width <= gridSize && sprite.rect.height <= gridSize)
			{
				return;
			}
			PlaceInCell(background, gridSize);
			TreeNodeRender fake = __instance.fakeBackground;
			if (fake != null && fake.image != null)
			{
				PlaceInCell(fake, gridSize);
			}
		}

		// Anchor the art to the item's top-left corner and centre it inside the one
		// cell it now occupies. Sliced images keep their native border size, so the
		// art is switched to Simple scaling first.
		private static void PlaceInCell(TreeNodeRender node, int gridSize)
		{
			node.image.type = Image.Type.Simple;
			RectTransform rect = node.rectTransform;
			rect.anchorMin = new Vector2(0f, 1f);
			rect.anchorMax = new Vector2(0f, 1f);
			rect.pivot = new Vector2(0.5f, 0.5f);
			rect.sizeDelta = new Vector2(gridSize, gridSize);
			rect.anchoredPosition = new Vector2(gridSize * 0.5f, -gridSize * 0.5f);
		}
	}

	// Cassettes are built sprite-first and tagged afterwards, so the setter guard
	// cannot see the tag at construction time. Compact the shape once the tag is
	// in place, which also recomputes modifiedShape and refreshes any inventory.
	[HarmonyPatch(typeof(CassetteHelper), "InitCassetteItem")]
	internal static class InitCassetteItemPatch
	{
		private static void Postfix(GameItem gameItem)
		{
			if (gameItem == null)
			{
				return;
			}
			GridShape shape = gameItem.shape;
			if (TapeShape.TooBig(shape))
			{
				gameItem.SetShape(TapeShape.Compact(shape));
			}
		}
	}
}
