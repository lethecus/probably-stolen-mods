using System;

namespace TinkerChance
{
	internal static class Loc
	{
		internal static string Get(string key, params object[] args)
		{
			try
			{
				return ModHelper.GetLocalized("tinker_chance", key, args);
			}
			catch (Exception)
			{
				return key;
			}
		}
	}
}
