using System;

namespace FastCharger
{
	/// <summary>
	/// Localized lookup. The loader reads Localization\&lt;locale&gt;.csv from the mod folder and
	/// ModHelper.GetLocalized resolves the current game language, falling back to "en".
	/// Returning the key on failure makes a missing table obvious on screen instead of hiding it.
	/// </summary>
	internal static class Loc
	{
		internal static string Get(string key, params object[] args)
		{
			try
			{
				return ModHelper.GetLocalized(FastChargerMod.ModId, key, args);
			}
			catch (Exception)
			{
				return key;
			}
		}
	}
}
