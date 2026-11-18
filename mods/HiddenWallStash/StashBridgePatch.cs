using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

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
		// The failure is per-call and frequent, so the full stack is written once and the rest stay quiet.
		private static bool reported;
		private static bool announced;
		private static int lastCount = -1;
		private static int failures;
		private static int step;

		[HarmonyPostfix]
		private static void Postfix(bool isOwn, ref List<GameItem> __result)
		{
			step = 0;
			try
			{
				step = 1;
				if (!HiddenWallStashMod.ExposeAsStorage || __result == null)
				{
					return;
				}
				step = 2;
				GameItem stash = StashManager.Stash;
				// GameItem derives from GameItemFunc, a plain class - NOT from UnityEngine.Object.
				// Casting it to Object throws InvalidCastException on every call; plain null checks are correct.
				if (stash == null)
				{
					return;
				}
				step = 3;
				List<GameItem> extra = GraphUtils.FindAllChildrenType<GameItem>((GraphNodeStorage)(object)stash, (Func<GameItem, bool>)null, (Func<GameItem, bool>)null);
				// The stash item itself goes in too: systems that ask "which containers exist" (AutoStorage's rule
				// list, presets) need the container, not just its contents. Without it the stash cannot be picked
				// as a destination anywhere.
				step = 4;
				extra.Insert(0, stash);
				if (extra.Count == 0)
				{
					return;
				}
				step = 5;
				HashSet<int> known = new HashSet<int>(__result.Select((GameItem i) => i.uniqueId));
				for (int n = 0; n < extra.Count; n++)
				{
					step = 6;
					GameItem item = extra[n];
					if (item == null)
					{
						continue;
					}
					step = 7;
					if (isOwn && !item.IsTag("IS_OWNED_TAG"))
					{
						continue;
					}
					step = 8;
					if (!known.Add(item.uniqueId))
					{
						continue;
					}
					step = 9;
					__result.Add(item);
				}
				if (!announced || extra.Count != lastCount)
				{
					announced = true;
					lastCount = extra.Count;
					HiddenWallStashMod.Log?.Log("FindAllItem bridge: " + extra.Count + " stash node(s) exposed (stash + contents), isOwn=" + isOwn + ".");
				}
			}
			catch (Exception ex)
			{
				failures++;
				if (!reported)
				{
					reported = true;
					string type = "?";
					try
					{
						GameItem stash = StashManager.Stash;
						if (stash != null)
						{
							type = stash.GetType().FullName + " @ " + stash.GetType().Assembly.GetName().Name;
						}
					}
					catch
					{
					}
					HiddenWallStashMod.Log?.Warning(
						"FindAllItem bridge failed at step " + step + " (stash type " + type + ", isOwn=" + isOwn + "):\n" + ex);
				}
			}
		}

		/// <summary>Logged once per launch by the mod so the outcome is visible in Player.log.</summary>
		internal static string Status
		{
			get { return failures == 0 ? "no failures" : failures + " failure(s)"; }
		}
	}
}
