namespace CassetteTapeSize
{
	public class CompactCassettesMod : IMod
	{
		public static ModLog Log { get; private set; }

		public void Init(ModManifest manifest)
		{
			Log = new ModLog(manifest);
		}

		public void OnEnable()
		{
			ModHook.OnGameLoadedLate += TapeShape.CompactLoadedTapes;
		}

		public void OnDisable()
		{
			ModHook.OnGameLoadedLate -= TapeShape.CompactLoadedTapes;
		}
	}
}
