using HarmonyLib;

namespace WaterJugTools
{
	// The shop tap calls AddWater with a fixed 9,999,999-part request. Anything
	// larger than that (12 L/20 L tanks) stays partly empty. -1 means "fill the
	// container's remaining capacity", so every container tops off completely.
	[HarmonyPatch(typeof(WaterHelper), "AddWater")]
	internal static class TapFillsToCapacityPatch
	{
		private static void Prefix(ref int addAmount)
		{
			if (addAmount == 9999999)
			{
				addAmount = -1;
			}
		}
	}

	// For an owned liquid container with something in it, enable the existing
	// Unload row so it can be used for the Empty action.
	[HarmonyPatch(typeof(ItemContextHandler), "UpdateConditions")]
	internal static class EmptyConditionsPatch
	{
		private static void Postfix(GameItem ___currentItem, ref bool ___canUnload)
		{
			if (CanEmpty(___currentItem))
			{
				___canUnload = true;
			}
		}

		internal static bool CanEmpty(GameItem item)
		{
			return item != null
				&& item.IsTag("LIQUID_CONTAINER_TAG")
				&& GeneralHelper.IsItemOwned(item)
				&& !WaterHelper.IsEmpty(item);
		}
	}

	// Rename that row to Empty for liquid containers, keeping the normal
	// hover/selected/disabled colours.
	[HarmonyPatch(typeof(ItemContextHandler), "SetButtonColor")]
	internal static class EmptyLabelPatch
	{
		private static bool Prefix(
			RichTextElement button,
			RichTextElement selectedButton,
			bool isEnabled,
			RichTextElement ___buttonUnload,
			GameItem ___currentItem,
			RenderHandler.ColorPalette ___textColor,
			RenderHandler.ColorPalette ___textDisabledColor,
			RenderHandler.ColorPalette ___textSelectColor,
			RenderHandler.ColorPalette ___textSelectDisabledColor)
		{
			if (button != ___buttonUnload || ___currentItem == null)
			{
				return true;
			}
			if (!___currentItem.IsTag("LIQUID_CONTAINER_TAG"))
			{
				return true;
			}

			bool selected = button == selectedButton;
			RenderHandler.ColorPalette color;
			if (isEnabled)
			{
				color = selected ? ___textSelectColor : ___textColor;
			}
			else
			{
				color = selected ? ___textSelectDisabledColor : ___textDisabledColor;
			}
			button.SetText("Empty", 10000, color);
			return false;
		}
	}

	// Perform the Empty action instead of the normal Unload handling.
	[HarmonyPatch(typeof(ItemContextHandler), "TriggerButton")]
	internal static class EmptyActionPatch
	{
		private static bool Prefix(
			RichTextElement button,
			ItemContextHandler __instance,
			RichTextElement ___buttonUnload,
			GameItem ___currentItem,
			bool ___canUnload)
		{
			if (button != ___buttonUnload || ___currentItem == null || !___canUnload)
			{
				return true;
			}
			if (!___currentItem.IsTag("LIQUID_CONTAINER_TAG"))
			{
				return true;
			}
			if (!GeneralHelper.IsItemOwned(___currentItem))
			{
				return true;
			}

			WaterHelper.EmptyContainer(___currentItem);
			AudioHelper.DumpWater();
			WaterHelper.UpdateWaterBottleSound(___currentItem);
			__instance.Stop();
			return false;
		}
	}
}
