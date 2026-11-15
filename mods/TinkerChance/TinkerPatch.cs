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

			if (!StartingPerk.IsTinkerer() || !RNG.Roll(TinkerSettings.Chance))
			{
				material.Destroy();
			}

			return false;
		}
	}
}
