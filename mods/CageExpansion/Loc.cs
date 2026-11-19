using System;

namespace CageExpansion
{
	internal static class Loc
	{
		internal static string Get(string key, params object[] args)
		{
			try
			{
				return ModHelper.GetLocalized(CageExpansionMod.ModId, key, args);
			}
			catch (Exception)
			{
				return key;
			}
		}
	}
}
