using System;

namespace HiddenWallStash
{
	public class HiddenWallStashMod : IMod
	{
		public const string ModId = "hidden_wall_stash";

		public static ModLog Log { get; private set; }

		public void Init(ModManifest manifest)
		{
			Log = new ModLog(manifest);
		}

		public void OnEnable()
		{
			ModHook.OnModItemDirectoryInit += OnModItemDirectoryInit;
			ModHook.OnGameLoadedInit += OnGameLoadedInit;
			ModHook.OnGameLoadedLate += OnGameLoadedLate;
			ModHook.OnNewGameStarted += OnNewGameStarted;
			ModHook.OnReturningStoreLate += OnReturningStoreLate;
			ModHook.OnLeavingStoreEarly += OnLeavingStoreEarly;
			ModHook.OnGoingSleepEarly += OnGoingSleepEarly;
			StashManager.EnsureUpdater();
		}

		public void OnDisable()
		{
			ModHook.OnModItemDirectoryInit -= OnModItemDirectoryInit;
			ModHook.OnGameLoadedInit -= OnGameLoadedInit;
			ModHook.OnGameLoadedLate -= OnGameLoadedLate;
			ModHook.OnNewGameStarted -= OnNewGameStarted;
			ModHook.OnReturningStoreLate -= OnReturningStoreLate;
			ModHook.OnLeavingStoreEarly -= OnLeavingStoreEarly;
			ModHook.OnGoingSleepEarly -= OnGoingSleepEarly;

			StashManager.FlushNow();
			StashManager.HideWindow();
		}

		private static void OnModItemDirectoryInit(ModItemDirectory dir)
		{
			dir.Add(StashManager.ItemId, StashItemFactory.Create);
		}

		private static void OnGameLoadedInit()
		{
			StashManager.ResetForLoad();
		}

		private static void OnGameLoadedLate()
		{
			StashManager.EnsureStash();
			StashManager.EnsureUpdater();
			GrateFactory.Ensure();
		}

		private static void OnNewGameStarted(string scenarioId)
		{
			StashManager.StartNewGame();
			StashManager.EnsureUpdater();
			GrateFactory.Ensure();
		}

		private static void OnReturningStoreLate()
		{
			StashManager.EnsureStash();
			StashManager.EnsureUpdater();
			GrateFactory.Ensure();
		}

		private static void OnLeavingStoreEarly()
		{
			StashManager.FlushNow();
		}

		private static void OnGoingSleepEarly()
		{
			StashManager.FlushNow();
		}
	}
}
