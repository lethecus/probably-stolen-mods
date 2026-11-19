using System;

namespace HiddenWallStash
{
	/// <summary>
	/// Localized string lookup for this mod.
	///
	/// The loader reads Localization\&lt;locale&gt;.csv from the mod folder and ModHelper.GetLocalized resolves the
	/// current game language, falling back to "en" when a locale is missing. Never build visible text from
	/// concatenated fragments: pass a format string with {0} placeholders and the values as args, so translators
	/// can move the placeholder where their grammar needs it.
	///
	/// If the lookup fails (older loader, missing table) the key is returned, which makes the gap obvious on
	/// screen instead of silently showing English in a Chinese game.
	/// </summary>
	internal static class Loc
	{
		internal static string Get(string key, params object[] args)
		{
			try
			{
				return ModHelper.GetLocalized(HiddenWallStashMod.ModId, key, args);
			}
			catch (Exception)
			{
				return key;
			}
		}
	}
}
