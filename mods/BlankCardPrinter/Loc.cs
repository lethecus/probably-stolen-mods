using System;

namespace BlankCardPrinter
{
	internal static class Loc
	{
		internal static string Get(string key, params object[] args)
		{
			try
			{
				return ModHelper.GetLocalized("blank_card_printer", key, args);
			}
			catch (Exception)
			{
				return key;
			}
		}
	}
}
