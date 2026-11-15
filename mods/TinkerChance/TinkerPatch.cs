using HarmonyLib;

namespace TinkerChance
{
	/// <summary>
	/// Replaces the hardcoded 20% roll in StartingPerkContent.ConsumeUpgradeMaterial with the
	/// configured chance. The original body is:
	///   if (!StartingPerk.IsTinkerer() || !RNG.Roll(20)) material.Destroy();
	/// </summary>
	[HarmonyPatch(typeof(StartingPerkContent), nameof(StartingPerkContent.ConsumeUpgradeMaterial))]
	internal static class ConsumeUpgradeMaterialPatch
	{
		[HarmonyPrefix]
		private static bool Prefix(GameItem material)
		{
			if (material == null)
			{
				return false;
			}

			bool tinkerer = StartingPerk.IsTinkerer();
			int chance = TinkerSettings.Chance;
			bool keep = tinkerer && RNG.Roll(chance);

			if (!keep)
			{
				material.Destroy();
			}

			if (TinkerChanceMod.Log != null)
			{
				TinkerChanceMod.Log.Log(string.Format(
					"Upgrade material {0} (Tinkerer={1}, chance={2}%)",
					keep ? "kept" : "consumed",
					tinkerer,
					chance));
			}

			return false;
		}
	}
}
