using System;

namespace ContaminantConverter
{
	internal static class Loc
	{
		internal static string Get(string key, params object[] args)
		{
			try
			{
				return ModHelper.GetLocalized(ContaminantConverterMod.ModId, key, args);
			}
			catch (Exception)
			{
				return key;
			}
		}
	}
}
