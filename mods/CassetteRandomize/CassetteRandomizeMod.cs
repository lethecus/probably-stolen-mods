namespace CassetteRandomize
{
	public class CassetteRandomizeMod : IMod
	{
		public static ModLog Log { get; private set; }

		public void Init(ModManifest manifest)
		{
			Log = new ModLog(manifest);
		}

		public void OnEnable()
		{
			ModHook.OnGameLoadedInit += OnGameLoadedInit;
			ModHook.OnGameLoadedLate += OnGameLoadedLate;
		}

		public void OnDisable()
		{
			ModHook.OnGameLoadedInit -= OnGameLoadedInit;
			ModHook.OnGameLoadedLate -= OnGameLoadedLate;
			AudioFx.Shutdown();
		}

		// Shuffle is a session preference: a fresh load always starts with it off.
		private static void OnGameLoadedInit()
		{
			CustomCassettePlayer.ShuffleActive = false;
		}

		// The equalizer filter is attached to the music source, so re-apply the saved
		// tone once the store and its cassette player exist.
		private static void OnGameLoadedLate()
		{
			DeckSettings.ApplyTone(EmporiumEntry.Instance?.cassettePlayer);
		}
	}
}
