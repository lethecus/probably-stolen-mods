using System;

namespace CalendarTV
{
	internal static class Loc
	{
		internal static string Get(string key, params object[] args)
		{
			try
			{
				return ModHelper.GetLocalized(CalendarTVMod.ModId, key, args);
			}
			catch (Exception)
			{
				return key;
			}
		}
	}
}
