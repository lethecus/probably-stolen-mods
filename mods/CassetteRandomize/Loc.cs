using System;

namespace CassetteRandomize
{
	internal static class Loc
	{
		internal static string Get(string key, params object[] args)
		{
			try
			{
				return ModHelper.GetLocalized("cassette_randomize", key, args);
			}
			catch (Exception)
			{
				return key;
			}
		}
	}
}
