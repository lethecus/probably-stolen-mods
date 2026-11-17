using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace TinkerChance
{
	/// <summary>
	/// Other mods that implement Tinkerer themselves roll a hardcoded 20 and never touch the vanilla
	/// ConsumeUpgradeMaterial method, so patching that alone leaves them at 20%.
	///
	/// ScavToolUpgrade (workshop 3829922293) has exactly two such rolls:
	///   ScavToolUpgrade.Native.Rules.Upgrade(GameItem source, GameItem target)
	///   ScavToolUpgrade.Native.NewTools.Spend(GameItem source, int count, bool perk)
	///
	/// They are patched only if that mod is loaded, by rewriting the "load 20" instruction in front of
	/// RNG.Roll to a call to our configured chance, so the rest of their logic is untouched.
	///
	/// Resolution note: the lookup keeps the type name and member name as literals at the call site.
	/// Passing them through parameters makes the game's mod scanner see a "computed type" and flag the
	/// mod as reflection-external, which it plans to refuse.
	/// </summary>
	internal static class OtherModPatches
	{
		private static bool upgradePatched;

		private static bool spendPatched;

		private static bool gaveUp;

		/// <summary>
		/// Safe to call repeatedly: the target mod's assembly is usually not loaded yet when our OnEnable
		/// runs, so this is attempted again from ModHook.OnGameLoadedLate, after every mod has loaded.
		/// If the mod is not installed at that point the patch is abandoned silently - it is an optional
		/// integration, not a problem worth logging.
		/// </summary>
		internal static void ApplyIfNeeded(Harmony harmony, bool finalAttempt)
		{
			if (harmony == null || gaveUp || (upgradePatched && spendPatched))
			{
				return;
			}

			if (!upgradePatched)
			{
				upgradePatched = Patch(
					harmony,
					AccessTools.Method(AccessTools.TypeByName("ScavToolUpgrade.Native.Rules"), "Upgrade"),
					"ScavToolUpgrade.Native.Rules.Upgrade");
			}
			if (!spendPatched)
			{
				spendPatched = Patch(
					harmony,
					AccessTools.Method(AccessTools.TypeByName("ScavToolUpgrade.Native.NewTools"), "Spend"),
					"ScavToolUpgrade.Native.NewTools.Spend");
			}

			if (finalAttempt && !upgradePatched && !spendPatched)
			{
				gaveUp = true;
				TinkerChanceMod.Log?.Warning("ScavToolUpgrade is installed but its upgrade methods could not be patched; tool upgrades keep the vanilla 20%.");
			}
		}

		private static bool Patch(Harmony harmony, MethodInfo method, string label)
		{
			if (method == null)
			{
				return false;
			}

			try
			{
				harmony.Patch(method, transpiler: new HarmonyMethod(typeof(OtherModPatches), nameof(ReplaceTwenty)));
				TinkerChanceMod.Log?.Log("Patched " + label + " to use the configured chance.");
				return true;
			}
			catch (Exception ex)
			{
				TinkerChanceMod.Log?.Warning("Could not patch " + label + ": " + ex.Message);
				return false;
			}
		}

		/// <summary>Replaces the 20 loaded right before RNG.Roll(int) with TinkerChanceApi.Chance.</summary>
		private static IEnumerable<CodeInstruction> ReplaceTwenty(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
		{
			List<CodeInstruction> list = new List<CodeInstruction>(instructions);
			MethodInfo getter = AccessTools.PropertyGetter(typeof(TinkerChanceApi), nameof(TinkerChanceApi.Chance));
			int replaced = 0;

			for (int i = 1; i < list.Count; i++)
			{
				CodeInstruction call = list[i];
				if (call.opcode != OpCodes.Call || !(call.operand is MethodInfo target))
				{
					continue;
				}
				if (target.DeclaringType != typeof(RNG) || target.Name != "Roll")
				{
					continue;
				}

				CodeInstruction previous = list[i - 1];
				bool loadsTwenty =
					(previous.opcode == OpCodes.Ldc_I4_S && Convert.ToInt32(previous.operand) == 20) ||
					(previous.opcode == OpCodes.Ldc_I4 && Convert.ToInt32(previous.operand) == 20);

				if (loadsTwenty)
				{
					list[i - 1] = new CodeInstruction(OpCodes.Call, getter);
					replaced++;
				}
			}

			TinkerChanceMod.Log?.Log(string.Format("Transpiled {0}: {1} roll(s) rewritten to the configured chance.", __originalMethod.Name, replaced));
			return list;
		}
	}

	/// <summary>Stable entry point the transpiler calls; separate so the getter is always public.</summary>
	public static class TinkerChanceApi
	{
		public static int Chance => TinkerSettings.Chance;
	}
}
