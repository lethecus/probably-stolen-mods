using HarmonyLib;
using UnityEngine;

// Dropping an item is handled by ItemMouseDragHandler.EndDrag (private). When the mouse is
// released over the grate, put the dragged item into the stash before the default logic runs.
// EndDrag then sees that the item already has a parent inventory and leaves it in place.
//
// The check has to be narrow. The stash window is opened at the cursor, so it sits on top of the
// grate's screen rectangle, and this prefix runs before any window logic: without the guards below a
// drop anywhere inside the open window was read as "onto the grate" and the item was taken out of the
// player's hand and put back into whichever tab had room. That looks exactly like the item vanishing,
// because it reappears in a tab the player is not looking at.
[HarmonyPatch(typeof(ItemMouseDragHandler), "EndDrag")]
internal static class GrateDropPatch
{
	[HarmonyPrefix]
	private static void Prefix(ItemMouseDragHandler __instance, bool cancelled)
	{
		if (cancelled || __instance == null)
		{
			return;
		}
		// Never take a drop that was aimed at a window, and never take one while the stash window is
		// open: while it is open the grate is covered by it, so a release there is aimed at the stash,
		// not at the wall. Drops into the stash with the window closed are the whole point of the patch.
		if (StashManager.IsWindowVisible())
		{
			return;
		}
		if (IsPointerOverShopUi())
		{
			return;
		}
		if (!GrateBehaviour.IsPointerOverGrate())
		{
			return;
		}
		GameItem item = __instance.GetCurrentItem();
		if (item == null)
		{
			return;
		}
		StashManager.TryDropIntoStash(item);
	}

	/// <summary>
	/// True when the pointer is over a drawn pixel element. The grate is a world sprite with no
	/// TreeNodeRender, so it reports null here while the shop windows report their element.
	/// </summary>
	private static bool IsPointerOverShopUi()
	{
		try
		{
			return RenderHandler.RaycastElement<PixelElement>(Input.mousePosition) != null;
		}
		catch
		{
			// If the raycast cannot run, do nothing rather than move an item the player did not aim here.
			return true;
		}
	}
}

/// <summary>
/// A grid that has never been drawn has no background render node, and the game's ValidateBackground dereferences
/// it without checking:
///
///     background.rectTransform.sizeDelta = new Vector2(widthPixels, heightPixels);
///
/// A stash resize leaves tabs in exactly that state, and the rename path
/// (SetTabName -> ModifyTag -> ValidateShapeState -> Validate -> ValidateBackground) and the window close path
/// (RememberContentTabs -> the same chain) both threw a null reference there. The rename silently did nothing and
/// the window would not close. There is nothing to draw for such a grid, so skipping the validation is correct.
/// </summary>
[HarmonyPatch(typeof(GameGridInventory), "ValidateBackground")]
internal static class ValidateBackgroundGuard
{
	[HarmonyPrefix]
	private static bool Prefix(TreeNodeRender ___background, TreeNodeRender ___insideBorder)
	{
		try
		{
			if (___background == null || ___background.rectTransform == null)
			{
				return false;
			}
			if (___insideBorder == null || ___insideBorder.rectTransform == null)
			{
				return false;
			}
			return true;
		}
		catch
		{
			// A destroyed render node throws on access; the call it guards would fail the same way.
			return false;
		}
	}
}

/// <summary>
/// Safety net for the one state the game deletes items from. ItemMouseDragHandler.EndDrag destroys the
/// held item when the drop left it with no parent inventory, and a failed or partial move anywhere can
/// produce that state. An item that came out of the stash is put back into it instead of being deleted.
/// Only that case is touched: an item that still has a parent (being sold, trashed, consumed) is never
/// rescued, and neither is an item the mod has never seen inside the stash.
///
/// Patched on GameItemElement, not GameItem: GameItem is abstract and declares Destroy() without a body,
/// and patching that declaration makes Harmony throw BadImageFormatException, which aborts the whole mod.
/// </summary>
[HarmonyPatch(typeof(GameItemElement), "Destroy")]
internal static class StashRescuePatch
{
	[HarmonyPrefix]
	private static bool Prefix(GameItemElement __instance)
	{
		try
		{
			if (__instance == null || __instance.parentInventory != null)
			{
				return true;
			}
			if (!__instance.IsTag("IS_OWNED_TAG"))
			{
				return true;
			}
			if (!StashManager.WasInStash(__instance))
			{
				return true;
			}
			return !StashManager.TryRescue(__instance);
		}
		catch
		{
			return true;
		}
	}
}
