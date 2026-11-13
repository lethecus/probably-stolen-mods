namespace WaterJugTools
{
	public class WaterJugToolsMod : IMod
	{
		public static ModLog Log { get; private set; }

		public void Init(ModManifest manifest)
		{
			Log = new ModLog(manifest);
		}

		public void OnEnable()
		{
		}

		public void OnDisable()
		{
		}
	}
}
