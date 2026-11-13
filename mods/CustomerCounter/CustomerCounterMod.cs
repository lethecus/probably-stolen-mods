using System;

namespace CustomerCounter
{
	public class CustomerCounterMod : IMod
	{
		public const string ModId = "customer_counter";

		public static ModLog Log { get; private set; }

		public void Init(ModManifest manifest)
		{
			Log = new ModLog(manifest);
		}

		public void OnEnable()
		{
			ModHook.OnGameLoadedLate += OnGameLoadedLate;
			ModHook.OnNewGameStarted += OnNewGameStarted;
			ModHook.OnReturningStoreLate += OnReturningStoreLate;

			CounterDisplay.EnsureUpdater();
			CounterDisplay.EnsureDisplay();
		}

		public void OnDisable()
		{
			ModHook.OnGameLoadedLate -= OnGameLoadedLate;
			ModHook.OnNewGameStarted -= OnNewGameStarted;
			ModHook.OnReturningStoreLate -= OnReturningStoreLate;

			CounterDisplay.DestroyAll();
		}

		private static void OnGameLoadedLate()
		{
			CounterDisplay.EnsureUpdater();
			CounterDisplay.EnsureDisplay();
		}

		private static void OnNewGameStarted(string scenarioId)
		{
			CounterDisplay.EnsureUpdater();
			CounterDisplay.EnsureDisplay();
		}

		private static void OnReturningStoreLate()
		{
			CounterDisplay.EnsureUpdater();
			CounterDisplay.EnsureDisplay();
		}
	}
}
