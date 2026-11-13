using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace NextOpeningHighlightFix
{
	// NextOpeningVisitors' DemandHighlightHost refreshes highlights every 0.25 s
	// and catches a NullReferenceException from DemandHighlightUI.RefreshNow(),
	// logging "Could not refresh item highlights" forever. This finalizer stops
	// the exception from reaching that catch (ending the spam) and records the
	// full stack once so the underlying null can be identified.
	// RefreshNow touches PlayerStore.Instance first. Outside the store that
	// getter constructs a PlayerStore, whose GameGridInventory constructor
	// fails because RenderHandler is not ready. The host is DontDestroyOnLoad
	// and ticks in menus, producing the NRE every 0.25 s. Skip the refresh
	// whenever there is no live store scene.
	[HarmonyPatch]
	internal static class StoreOnlyGuard
	{
		private static bool Prepare()
		{
			return TargetMethod() != null;
		}

		private static MethodBase TargetMethod()
		{
			Type type = AccessTools.TypeByName("NextOpeningVisitors.DemandHighlightUI");
			return type == null ? null : AccessTools.Method(type, "RefreshNow");
		}

		private static bool Prefix()
		{
			return EmporiumEntry.Instance != null;
		}
	}

	// SetEnabled(true) runs while the game loads, before the phone UI is
	// actually open, and its early AddButton attempt throws. The button is
	// rebuilt from PhoneUIManager.OpenUI, so skip the restore attempt unless
	// the phone book UI is active.
	[HarmonyPatch]
	internal static class PhoneButtonRestoreGuard
	{
		private static bool Prepare()
		{
			return TargetMethod() != null;
		}

		private static MethodBase TargetMethod()
		{
			Type type = AccessTools.TypeByName("NextOpeningVisitors.PhoneCooldownResetButton");
			return type == null ? null : AccessTools.Method(type, "SetEnabled", new[] { typeof(bool) });
		}

		private static bool Prefix(bool enabled)
		{
			if (!enabled)
			{
				return true;
			}
			PhoneUIManager phone = PhoneUIManager.Instance;
			if (phone == null || phone.phoneBookUIManager == null)
			{
				return false;
			}
			return phone.phoneBookUIManager.gameObject.activeInHierarchy;
		}
	}

	// Same idea for the phone cooldown button restore path. Its AddButton is
	// called inside a try/catch that only logs the message, so capture the
	// stack once here and suppress the message.
	[HarmonyPatch]
	internal static class PhoneButtonGuard
	{
		private static readonly HashSet<string> Seen = new HashSet<string>();

		private static bool Prepare()
		{
			return TargetMethod() != null;
		}

		private static MethodBase TargetMethod()
		{
			Type type = AccessTools.TypeByName("NextOpeningVisitors.PhoneCooldownResetButton");
			return type == null ? null : AccessTools.Method(type, "AddButton", new[] { typeof(PhoneUIManager), typeof(PhoneBookUIManager) });
		}

		// The button is rebuilt from PhoneUIManager.OpenUI once the phone UI and
		// its font are ready, so skip the early restore attempt instead of
		// letting TextMeshPro throw on a missing font.
		private static bool Prefix(PhoneUIManager phone)
		{
			if (phone == null || phone.numberDisplay == null || phone.statusDisplay == null)
			{
				return false;
			}
			return phone.statusDisplay.font != null && phone.statusDisplay.fontSharedMaterial != null;
		}

		private static Exception Finalizer(Exception __exception)
		{
			if (__exception == null)
			{
				return null;
			}
			string key = __exception.StackTrace ?? __exception.Message;
			if (Seen.Add(key))
			{
				ModLog log = NextOpeningHighlightFixMod.Log;
				string text = "[NextOpeningHighlightFix] Suppressed phone-button exception:\n" + __exception;
				if (log != null)
				{
					log.Error(text);
				}
				else
				{
					Debug.LogError(text);
				}
			}
			return null;
		}
	}

	[HarmonyPatch]
	internal static class CreateNodeGuard
	{
		private static bool Prepare()
		{
			return TargetMethod() != null;
		}

		private static MethodBase TargetMethod()
		{
			Type type = AccessTools.TypeByName("NextOpeningVisitors.DemandHighlightFrame");
			return type == null ? null : AccessTools.Method(type, "CreateNode", new[] { typeof(TreeNodeRender), typeof(int) });
		}

		private static bool Prefix(TreeNodeRender parent, ref TreeNodeRender __result)
		{
			if (parent == null)
			{
				__result = null;
				return false;
			}
			return true;
		}
	}

	[HarmonyPatch]
	internal static class HighlightRefreshGuard
	{
		private static readonly HashSet<string> Seen = new HashSet<string>();

		private static bool Prepare()
		{
			return TargetMethod() != null;
		}

		private static MethodBase TargetMethod()
		{
			Type type = AccessTools.TypeByName("NextOpeningVisitors.DemandHighlightUI");
			return type == null ? null : AccessTools.Method(type, "RefreshNow");
		}

		private static Exception Finalizer(Exception __exception)
		{
			if (__exception == null)
			{
				return null;
			}

			string key = __exception.StackTrace ?? __exception.Message;
			if (Seen.Add(key))
			{
				ModLog log = NextOpeningHighlightFixMod.Log;
				string text = "[NextOpeningHighlightFix] Suppressed item-highlight refresh exception:\n" + __exception;
				if (log != null)
				{
					log.Error(text);
				}
				else
				{
					Debug.LogError(text);
				}
			}
			return null;
		}
	}
}
