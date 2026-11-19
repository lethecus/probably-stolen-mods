using System;

namespace SaveTheNewspaper
{
	internal static class Loc
	{
		internal static string Get(string key, params object[] args)
		{
			try
			{
				return ModHelper.GetLocalized(SaveTheNewspaperMod.ModId, key, args);
			}
			catch (Exception)
			{
				return key;
			}
		}
	}
}
