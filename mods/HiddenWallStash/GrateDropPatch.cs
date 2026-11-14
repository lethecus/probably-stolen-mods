using HarmonyLib;

// Dropping an item is handled by ItemMouseDragHandler.EndDrag (private). When the mouse is
// released over the grate, put the dragged item into the stash before the default logic runs.
// EndDrag then sees that the item already has a parent inventory and leaves it in place.
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
}
