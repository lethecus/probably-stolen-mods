using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;

namespace LiquidLocaleGuard
{
	/// <summary>
	/// Stops the "invalid operation handle" chain that breaks every liquid mod on load.
	///
	/// The chain, confirmed in the 18 Nov 2026 logs:
	///   any mod patches a method whose code touches Liquid
	///     -> Liquid..cctor runs Liquid.InitLiquid()
	///       -> every liquid asks LocHelper.GetLocalizedMechanic("liquid_...")
	///         -> Unity Localization -> Addressables WaitForCompletion, before the
	///            localization system is ready -> throws
	///           -> Liquid's type initializer fails and .NET caches that failure, so every
	///              later liquid mod throws the same exception for the rest of the session.
	///
	/// Whichever liquid mod loads first loses, and all the others inherit it, which is why
	/// disabling one just moves the caution down the list.
	///
	/// This mod does two narrow things:
	///   1. during load, a liquid lookup that would throw returns a readable fallback instead,
	///      so Liquid initialises once and the poison never happens;
	///   2. after the game has loaded, the real names are written back onto the same Liquid
	///      objects, so nothing stays as "liquid_heavy_metal".
	///
	/// Only keys starting with "liquid_" are touched, and only when the lookup actually threw.
	/// </summary>
	public sealed class LiquidLocaleGuardMod : IMod
	{
		internal const string ModId = "liquid_locale_guard";

		internal static ModLog Log;

		// identifier -> localization key, read from Liquid.InitLiquid() in the current build.
		internal static readonly Dictionary<string, string> Keys = new Dictionary<string, string>(StringComparer.Ordinal)
		{
			{ "water", "liquid_water" },
			{ "heavy_metal", "liquid_heavy_metal" },
			{ "organic_waste", "liquid_organic_waste" },
			{ "microplastic", "liquid_microplastic" },
			{ "microbe", "liquid_microbes" },
			{ "chemical_contaminant", "liquid_chemical_contaminant" },
			{ "physical_contaminant", "liquid_physical_contaminant" },
			{ "mineral", "liquid_mineral" },
			{ "dream_cap_mush", "liquid_dream_cap_mush" },
			{ "dream_cap_extract", "liquid_dream_cap_extract" },
			{ "plant_matter", "liquid_plant_matter" },
			{ "alcohol", "liquid_alcohol" },
			{ "caffeine", "liquid_caffeine" },
			{ "protein", "liquid_protein" },
			{ "fat", "liquid_fat_oil" },
			{ "starch", "liquid_starch" },
			{ "sugar", "liquid_sugar" },
			{ "coffee_mush", "liquid_coffee_bean_mush" },
			{ "contraceptive", "liquid_contraceptive_hormones" },
			{ "zyanide", "liquid_zyanide" },
			{ "nightmare_extract", "liquid_nightmare_extract" },
			{ "nightmare_cap_mush", "liquid_nightmare_cap_mush" },
			{ "chlorine", "liquid_chlorine" },
			{ "synaprest", "liquid_synaprest" },
			{ "fanta", "liquid_fanta" },
			{ "methylol", "liquid_n_methylol_base" },
			{ "reducing_agent", "liquid_reducing_agent" },
			{ "acid", "liquid_acid" },
			{ "base", "liquid_base" },
			{ "stabilizing_agent", "liquid_stabilizing_agent" },
			{ "debris", "liquid_insoluble_debris" },
			{ "lipobond_agent", "liquid_lipobond_agent" },
			{ "livestock_steroid", "liquid_livestock_steroid" },
			{ "livestock_immunity", "liquid_livestock_immunity" },
			{ "wine_extract", "liquid_wine_extract" },
			{ "nomore", "liquid_nomore" }
		};

		private Harmony harmony;

		public void Init(ModManifest manifest)
		{
			Log = new ModLog(manifest);
		}

		public void OnEnable()
		{
			harmony = new Harmony("mod.liquid_locale_guard");
			harmony.PatchAll(typeof(LiquidLocaleGuardMod).Assembly);

			PreWarm();

			ModHook.OnGameLoadedLate += Repair;
			ModHook.OnWakingUpLate += Repair;

			Log?.Log("Liquid locale guard enabled: liquid lookups during load fall back instead of throwing.");
		}

		public void OnDisable()
		{
			ModHook.OnGameLoadedLate -= Repair;
			ModHook.OnWakingUpLate -= Repair;
			harmony?.UnpatchAll("mod.liquid_locale_guard");
			harmony = null;
		}

		/// <summary>
		/// Touch Liquid deliberately while this mod is the one running. With the guard in place it
		/// initialises cleanly even though localization is not ready, and every liquid mod that loads
		/// afterwards finds a working type instead of a poisoned one. If this throws, this mod loaded
		/// after the damage was already done and the session cannot be recovered.
		/// </summary>
		internal static void PreWarm()
		{
			try
			{
				List<Liquid> liquids = Liquid.Liquids;
				if (liquids != null)
				{
					Log?.Log("Liquid pre-warmed early: " + liquids.Count + " entries; names are restored after load.");
				}
			}
			catch (Exception ex)
			{
				Log?.Error("Liquid was already initialised and had failed before this mod loaded (" + ex.GetType().Name +
					"). This mod is too late in the load order: install it into the Mods folder (manual/Nexus install), " +
					"or have your liquid mods list liquid_locale_guard as a prerequisite, then restart.");
			}
		}

		/// <summary>Readable stand-in used only while localization is not ready.</summary>
		internal static string Fallback(string key)
		{
			if (string.IsNullOrEmpty(key))
			{
				return "";
			}
			string text = key.StartsWith("liquid_", StringComparison.Ordinal) ? key.Substring(7) : key;
			var builder = new StringBuilder(text.Length);
			bool upper = true;
			foreach (char c in text)
			{
				if (c == '_')
				{
					builder.Append(' ');
					upper = true;
					continue;
				}
				builder.Append(upper ? char.ToUpperInvariant(c) : c);
				upper = false;
			}
			return builder.ToString();
		}

		/// <summary>Put the real localized names back once the table is available.</summary>
		internal static void Repair()
		{
			try
			{
				List<Liquid> liquids = Liquid.Liquids;
				if (liquids == null)
				{
					return;
				}
				int fixedCount = 0;
				int waiting = 0;
				foreach (Liquid liquid in liquids)
				{
					if (liquid == null || liquid.identifier == null)
					{
						continue;
					}
					if (!Keys.TryGetValue(liquid.identifier, out string key))
					{
						continue;
					}
					string fallback = Fallback(key);
					string name = null;
					try
					{
						name = LocHelper.GetLocalizedMechanic(key);
					}
					catch (Exception)
					{
						name = null;
					}
					if (string.IsNullOrEmpty(name) || name == fallback)
					{
						waiting++;
						continue;
					}
					liquid.displayName = name;
					fixedCount++;
				}
				if (fixedCount > 0)
				{
					Log?.Log("Liquid names restored from the localization table: " + fixedCount + " of " + liquids.Count + ".");
				}
				else if (waiting > 0)
				{
					Log?.Log("Liquid names still waiting on the localization table (" + waiting + "); will retry after the next night.");
				}
			}
			catch (Exception ex)
			{
				Log?.Warning("Liquid name repair failed: " + ex.Message);
			}
		}
	}

	/// <summary>Swallows only the load-time failure for liquid keys; everything else still throws.</summary>
	[HarmonyPatch(typeof(LocHelper), "GetLocalizedMechanic")]
	internal static class LocalizedMechanicGuard
	{
		private static bool warned;

		[HarmonyFinalizer]
		private static Exception Finalizer(string key, Exception __exception, ref string __result)
		{
			if (__exception == null)
			{
				return null;
			}
			if (key == null || !key.StartsWith("liquid_", StringComparison.Ordinal))
			{
				return __exception;
			}
			__result = LiquidLocaleGuardMod.Fallback(key);
			if (!warned)
			{
				warned = true;
				LiquidLocaleGuardMod.Log?.Warning("Localization was not ready for '" + key + "'; used '" + __result +
					"' so Liquid could initialise. Names are restored after load.");
			}
			return null;
		}
	}
}
