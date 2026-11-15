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

			if (!HasAssembly("ScavToolUpgrade"))
			{
				if (finalAttempt)
				{
					gaveUp = true;
				}
				return;
			}

			if (!upgradePatched)
			{
				upgradePatched = Patch(harmony, "ScavToolUpgrade", "ScavToolUpgrade.Native.Rules", "Upgrade");
			}
			if (!spendPatched)
			{
				spendPatched = Patch(harmony, "ScavToolUpgrade", "ScavToolUpgrade.Native.NewTools", "Spend");
			}

			if (finalAttempt && !upgradePatched && !spendPatched)
			{
				gaveUp = true;
				TinkerChanceMod.Log?.Warning("ScavToolUpgrade is installed but its upgrade methods could not be patched; tool upgrades keep the vanilla 20%.");
			}
		}

		private static bool HasAssembly(string assemblyName)
		{
			foreach (Assembly candidate in AppDomain.CurrentDomain.GetAssemblies())
			{
				if (candidate.GetName().Name == assemblyName)
				{
					return true;
				}
			}

			return false;
		}

		private static bool Patch(Harmony harmony, string assemblyName, string typeName, string methodName)
		{
			try
			{
				Assembly assembly = null;
				foreach (Assembly candidate in AppDomain.CurrentDomain.GetAssemblies())
				{
					if (candidate.GetName().Name == assemblyName)
					{
						assembly = candidate;
						break;
					}
				}
				if (assembly == null)
				{
					return false;
				}

				Type type = assembly.GetType(typeName, throwOnError: false);
				MethodInfo method = type?.GetMethod(methodName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
				if (method == null)
				{
					TinkerChanceMod.Log?.Warning("Could not find " + typeName + "." + methodName + " to patch.");
					return false;
				}

				harmony.Patch(method, transpiler: new HarmonyMethod(typeof(OtherModPatches), nameof(ReplaceTwenty)));
				TinkerChanceMod.Log?.Log("Patched " + typeName + "." + methodName + " to use the configured chance.");
				return true;
			}
			catch (Exception ex)
			{
				TinkerChanceMod.Log?.Warning("Could not patch " + typeName + "." + methodName + ": " + ex.Message);
				return false;
			}
		}

		/// <summary>Replaces the 20 loaded right before RNG.Roll(int) with TinkerChanceApi.Chance.</summary>
		private static IEnumerable<CodeInstruction> ReplaceTwenty(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
		{
			List<CodeInstruction> list = new List<CodeInstruction>(instructions);
			MethodInfo getter = typeof(TinkerChanceApi).GetProperty(nameof(TinkerChanceApi.Chance)).GetGetMethod();
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
					(previous.opcode == OpCodes.Ldc_I4 && Convert.ToInt32(previous.operand) == 20) ||
					(previous.opcode == OpCodes.Ldc_I4_8 && false);

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
