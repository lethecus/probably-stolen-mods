using System;
using UnityEngine;

namespace HiddenWallStash
{
	public class HiddenWallStashMod : IMod
	{
		public const string ModId = "hidden_wall_stash";

		public const int MinSide = 5;
		public const int MaxSide = 25;
		public const int DefaultWidth = 15;
		public const int DefaultHeight = 13;

		public static ModLog Log { get; private set; }

		private static ConfigEntry<int> widthSetting;
		private static ConfigEntry<int> heightSetting;
		private static ConfigEntry<bool> exposeSetting;

		/// <summary>
		/// Default true: an ordinary container's contents are part of the shop, so the stash behaves the
		/// same way unless the player turns it off. This is what lets nightly machines, hoppers and
		/// AutoStorage reach the stash (see StashBridgePatch).
		/// </summary>
		public static bool ExposeAsStorage
		{
			get { return exposeSetting == null || exposeSetting.Value; }
		}

		/// <summary>Cells per row for a new stash, clamped to the allowed range.</summary>
		public static int StashWidth
		{
			get { return Mathf.Clamp(widthSetting != null ? widthSetting.Value : DefaultWidth, MinSide, MaxSide); }
		}

		/// <summary>Rows per tab for a new stash, clamped to the allowed range.</summary>
		public static int StashHeight
		{
			get { return Mathf.Clamp(heightSetting != null ? heightSetting.Value : DefaultHeight, MinSide, MaxSide); }
		}

		public void Init(ModManifest manifest)
		{
			Log = new ModLog(manifest);
		}

		public void OnEnable()
		{
			try
			{
				ModConfig config = ModConfig.For(ModId);
				widthSetting = config.Define(
					"stash_width",
					DefaultWidth,
					Loc.Get("cfg.section.size"),
					() => Loc.Get("cfg.width.label"),
					MinSide,
					MaxSide,
					() => Loc.Get("cfg.width.tip"));
				heightSetting = config.Define(
					"stash_height",
					DefaultHeight,
					Loc.Get("cfg.section.size"),
					() => Loc.Get("cfg.height.label"),
					MinSide,
					MaxSide,
					() => Loc.Get("cfg.height.tip"));
				exposeSetting = config.Define(
					"expose_as_storage",
					true,
					Loc.Get("cfg.section.behaviour"),
					() => Loc.Get("cfg.storage.label"),
					() => Loc.Get("cfg.storage.tip"),
					false);
			}
			catch (Exception ex)
			{
				Log?.Warning("Could not register the size settings: " + ex.Message);
			}

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
			// The old identifier stays registered so a save written before the rename still decodes; the item
			// itself is then renamed in AttachHost.
			if (!string.Equals(StashManager.ItemId, StashManager.ItemIdentifier, StringComparison.Ordinal))
			{
				dir.Add(StashManager.ItemIdentifier, StashItemFactory.Create);
			}
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
