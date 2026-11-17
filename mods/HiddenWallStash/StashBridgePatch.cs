using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HiddenWallStash
{
	/// <summary>
	/// Makes the stash's contents count as shop storage, so the same systems that reach ordinary
	/// containers also reach this one:
	///
	/// - the three nightly machine passes in PlayerStore.EndNight (onCycleEndEarly / Slot / Late)
	/// - HopperHelper.RunWindow(1/2/3), which is phase based rather than screen based
	/// - AutoStorage, which builds its target list from PlayerStore.FindAllItem
	/// - power/service accounting, which also walks that list
	///
	/// Security inspection is unaffected: StartInspection walks a fixed set of EmporiumEntry windows
	/// (front, back, showcase, hidden, trash) and never calls FindAllItem, so the stash stays hidden.
	/// </summary>
	[HarmonyPatch(typeof(PlayerStore), "FindAllItem")]
	internal static class FindAllItemPatch
	{
		[HarmonyPostfix]
		private static void Postfix(bool isOwn, ref List<GameItem> __result)
		{
			try
			{
				if (!HiddenWallStashMod.ExposeAsStorage || __result == null)
				{
					return;
				}
				GameItem stash = StashManager.Stash;
				if ((Object)(object)stash == (Object)null)
				{
					return;
				}
				List<GameItem> extra = GraphUtils.FindAllChildrenType<GameItem>((GraphNodeStorage)(object)stash, (Func<GameItem, bool>)null, (Func<GameItem, bool>)null);
				if (extra == null || extra.Count == 0)
				{
					return;
				}
				HashSet<int> known = new HashSet<int>(__result.Select((GameItem i) => i.uniqueId));
				for (int n = 0; n < extra.Count; n++)
				{
					GameItem item = extra[n];
					if (item == null)
					{
						continue;
					}
					if (isOwn && !item.IsTag("IS_OWNED_TAG"))
					{
						continue;
					}
					if (!known.Add(item.uniqueId))
					{
						continue;
					}
					__result.Add(item);
				}
			}
			catch (Exception ex)
			{
				HiddenWallStashMod.Log?.Warning("FindAllItem bridge failed: " + ex.Message);
			}
		}
	}
}
