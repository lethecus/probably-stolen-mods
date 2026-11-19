using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace CageExpansion
{
	/// <summary>
	/// Raises the animal cage's size cap so one cage can hold more rats without overcrowding.
	///
	/// Vanilla rules, read from AnimalCage and HusbandryHelper:
	///   * a cage grid is 10 wide and (3 + size) tall, size starting at 0, upgraded one row per
	///     metal ingot, and AnimalCage.CanUpgradeSize stops at 8 rows (CAGE_SIZE_TAG &lt; 8);
	///   * AnimalCage.UpgradeSize clamps the row count again with Mathf.Min(num + 1, 8), so raising
	///     only CanUpgradeSize would show "can upgrade" while silently doing nothing;
	///   * overcrowding is not a per-animal flag, it is occupancy: sum of ANIMAL_ITEM_TAG item sizes
	///     divided by the cage's grid cells (HusbandryHelper.GetAnimalOvercrowdingLevel), with
	///     thresholds at 55% / 70% / 90% for levels 1 / 2 / 3. Level 2 rolls 5% harm per night,
	///     level 3 rolls 15%.
	///
	/// So the only thing needed is more rows: the occupancy ratio falls by itself. This mod patches
	/// the two clamps and the tooltip's remaining-upgrade count. Nothing else is touched, and the
	/// one-ingot-per-row cost is unchanged.
	/// </summary>
	public sealed class CageExpansionMod : IMod
	{
		internal const string ModId = "cage_expansion";

		// AnimalCage.STARTING_WIDTH and AnimalCage.MAX_SIZE in the game.
		internal const int VanillaMaxHeight = 8;
		internal const int HardMaxHeight = 16;

		internal static ModLog Log;
		internal static int MaxHeight = 12;

		private static ConfigEntry<int> maxHeightEntry;

		private Harmony harmony;

		public void Init(ModManifest manifest)
		{
			Log = new ModLog(manifest);
		}

		public void OnEnable()
		{
			try
			{
				ModConfig config = ModConfig.For(ModId);
				maxHeightEntry = config.Define(
					"max_size", 12, Loc.Get("cfg.section.cage"),
					() => Loc.Get("cfg.max.label"),
					VanillaMaxHeight, HardMaxHeight,
					() => Loc.Get("cfg.max.tip"),
					false);
				MaxHeight = Clamp(maxHeightEntry.Value);
				maxHeightEntry.OnChanged += value => MaxHeight = Clamp(value);
			}
			catch (Exception ex)
			{
				Log?.Warning("Could not register the cage size setting: " + ex.Message);
			}

			harmony = new Harmony("mod.cage_expansion");
			harmony.PatchAll(typeof(CageExpansionMod).Assembly);

			Log?.Log("Cage expansion enabled: maximum cage size " + MaxHeight + " rows (vanilla " + VanillaMaxHeight + ").");
		}

		public void OnDisable()
		{
			harmony?.UnpatchAll("mod.cage_expansion");
			harmony = null;
		}

		internal static int Clamp(int value)
		{
			return Mathf.Clamp(value, VanillaMaxHeight, HardMaxHeight);
		}

		internal static int CurrentSize(GameItem cage)
		{
			if (cage == null)
			{
				return -1;
			}
			try
			{
				TagState state = cage.GetTagReadonly(AnimalCage.CAGE_SIZE_TAG);
				return state != null ? state.GetInt() : -1;
			}
			catch (Exception)
			{
				return -1;
			}
		}
	}

	/// <summary>Vanilla returns false at 8 rows; allow anything below our configured cap.</summary>
	[HarmonyPatch(typeof(AnimalCage), nameof(AnimalCage.CanUpgradeSize))]
	internal static class CanUpgradeSizePatch
	{
		[HarmonyPostfix]
		private static void Postfix(GameItem cage, ref bool __result)
		{
			if (__result)
			{
				return;
			}
			int size = CageExpansionMod.CurrentSize(cage);
			if (size >= 0 && size < CageExpansionMod.MaxHeight)
			{
				__result = true;
			}
		}
	}

	/// <summary>Replaces the hard-coded 8 in Mathf.Min(num + 1, 8) with the configured cap.</summary>
	[HarmonyPatch(typeof(AnimalCage), nameof(AnimalCage.UpgradeSize))]
	internal static class UpgradeSizePatch
	{
		[HarmonyTranspiler]
		private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			return SizeConstant.ReplaceEight(instructions, "UpgradeSize");
		}
	}

	/// <summary>Replaces the hard-coded 8 in "8 - size" so the tooltip counts up to our cap.</summary>
	[HarmonyPatch(typeof(AnimalCage), nameof(AnimalCage.CreateTooltip))]
	internal static class TooltipPatch
	{
		[HarmonyTranspiler]
		private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			return SizeConstant.ReplaceEight(instructions, "CreateTooltip");
		}
	}

	internal static class SizeConstant
	{
		internal static int GetMaxHeight()
		{
			return CageExpansionMod.MaxHeight;
		}

		private static System.Reflection.MethodInfo cached;

		// Harmony can compile a patched method more than once, so a second transpiler pass sees IL
		// that already contains our call and legitimately finds no ldc.i4.8. Only the first pass is
		// worth reporting; a later "no constant found" is not a problem.
		private static readonly HashSet<string> reported = new HashSet<string>(StringComparer.Ordinal);

		internal static IEnumerable<CodeInstruction> ReplaceEight(IEnumerable<CodeInstruction> instructions, string where)
		{
			if (cached == null)
			{
				cached = AccessTools.Method(typeof(SizeConstant), nameof(GetMaxHeight));
			}
			if (cached == null)
			{
				CageExpansionMod.Log?.Error("Could not resolve GetMaxHeight; " + where + " keeps the vanilla cap.");
				return instructions;
			}

			var result = new List<CodeInstruction>();
			int replaced = 0;
			foreach (CodeInstruction instruction in instructions)
			{
				if (instruction.opcode == OpCodes.Ldc_I4_8)
				{
					result.Add(new CodeInstruction(OpCodes.Call, cached));
					replaced++;
					continue;
				}
				result.Add(instruction);
			}

			if (replaced > 0)
			{
				if (reported.Add(where))
				{
					CageExpansionMod.Log?.Log(where + ": " + replaced + " size constant(s) redirected to the config cap.");
				}
			}
			else if (!reported.Contains(where))
			{
				CageExpansionMod.Log?.Warning(where + ": no hard-coded 8 found, the game's IL changed and this patch did nothing.");
			}
			return result;
		}
	}
}
