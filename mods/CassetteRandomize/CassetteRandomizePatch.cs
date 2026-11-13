using System;
using HarmonyLib;

namespace CassetteRandomize
{
	[HarmonyPatch(typeof(MachineCassettePlayer), "CassettePlayer")]
	internal static class CassettePlayerOverride
	{
		private static bool Prefix(ref GameItem __result)
		{
			try
			{
				GameItem machine = CustomCassettePlayer.Create();
				if (machine != null)
				{
					__result = machine;
					return false;
				}
			}
			catch (Exception exception)
			{
				CassetteRandomizeMod.Log?.Error("Could not build the custom cassette player; falling back to vanilla: " + exception);
			}
			return true;
		}
	}

	[HarmonyPatch(typeof(MachineCassettePlayer), "GetDeckOwner")]
	internal static class DeckOwnerOverride
	{
		private static bool Prefix(GameInventory deck, ref GameItem __result)
		{
			if (deck != null && CustomCassettePlayer.Decks.TryGetValue(deck, out GameItem machine))
			{
				__result = machine;
				return false;
			}
			return true;
		}
	}

	// The vanilla OnTrackDone always advances with the Next button. When the
	// player owns our custom deck, route it through our shuffle-aware path
	// instead (the stock GetCassetteInventory cast cannot see a scrollable deck).
	[HarmonyPatch(typeof(AudioManager), "OnTrackDone")]
	internal static class TrackDoneOverride
	{
		private static bool Prefix()
		{
			EmporiumEntry entry = EmporiumEntry.Instance;
			GameItem player = entry?.cassettePlayer;
			if (player == null || !CustomCassettePlayer.IsCustom(player))
			{
				return true;
			}
			GameInventory deck = CustomCassettePlayer.Deck(player);
			if (deck == null)
			{
				return true;
			}
			if (CustomCassettePlayer.ShuffleActive)
			{
				if (!CustomCassettePlayer.Randomize(player))
				{
					CustomCassettePlayer.Stop(player);
				}
			}
			else
			{
				CustomCassettePlayer.Next(player, deck);
			}
			return false;
		}
	}
}
