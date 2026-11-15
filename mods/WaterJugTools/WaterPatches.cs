using HarmonyLib;

namespace WaterJugTools
{
	// The shop tap calls AddWater with a fixed 9,999,999-part request. Anything
	// larger than that (12 L/20 L tanks and modded multi-litre tanks) stays partly
	// empty. -1 means "fill the container's remaining capacity", so every container
	// tops off completely.
	//
	// Other mods patch AddWater as well, and two things can eat the change:
	//  - a prefix that asks for object[] __args makes Harmony write that array back
	//    over the arguments after the prefixes run, discarding a plain ref change;
	//  - a later prefix can replace the value again.
	// So the amount is edited through the shared __args array, and a postfix tops
	// the container up if the original request still reached AddWater unchanged.
	[HarmonyPatch(typeof(WaterHelper), "AddWater")]
	internal static class TapFillsToCapacityPatch
	{
		private const int TapRequest = 9999999;

		private static bool toppingUp;

		private static void Prefix(object[] __args)
		{
			if (__args == null || __args.Length < 3 || !(__args[2] is int amount) || amount != TapRequest)
			{
				return;
			}

			__args[2] = -1;
			WaterJugToolsMod.Log?.Log("Tap fill: request " + amount + " -> fill to capacity.");
		}

		private static void Postfix(GameItem waterContainer, int grade, int __2, bool __runOriginal)
		{
			if (!__runOriginal || toppingUp || __2 != TapRequest || waterContainer == null)
			{
				return;
			}

			toppingUp = true;
			try
			{
				int before = WaterHelper.GetTotalVolume(waterContainer);
				WaterHelper.AddWater(waterContainer, grade);
				int after = WaterHelper.GetTotalVolume(waterContainer);
				WaterJugToolsMod.Log?.Log("Tap fill: another mod kept the fixed amount; topped up " + before + " -> " + after + ".");
			}
			finally
			{
				toppingUp = false;
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
