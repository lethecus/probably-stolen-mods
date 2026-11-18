using System;

public static class StashItemFactory
{
	public const int TabCount = 10;

	/// <summary>Mod-data key holding the size the stash was last built at, so a decode never uses a
	/// different size than the save (which would drop items that fall outside the new grid).</summary>
	public const string SizeKey = "stash_size";

	public static GameItem Create()
	{
		int width;
		int height;
		if (!StashManager.TryReadStoredSize(out width, out height))
		{
			if (StashManager.IsDecoding)
			{
				// Restoring a save: build the largest grid so nothing the save contains can be dropped.
				// ApplyConfiguredSize then shrinks to the requested size without losing anything.
				width = HiddenWallStash.HiddenWallStashMod.MaxSide;
				height = HiddenWallStash.HiddenWallStashMod.MaxSide;
			}
			else
			{
				width = HiddenWallStash.HiddenWallStashMod.StashWidth;
				height = HiddenWallStash.HiddenWallStashMod.StashHeight;
			}
		}

		MachineryTabs tabs = new MachineryTabs();
		for (int i = 0; i < TabCount; i++)
		{
			GameGridInventory grid = new GameGridInventory(width, height);
			grid.identifier = "smuggler_bay";
			tabs.AddTab(LocHelper.GetLocalizedMechanic("mech_storage_bay_tab", i + 1), MachineHelper.WrapInIntake(grid));
		}

		PixelWindow window = MachineHelper.CreateMachineryWindow(true, MachineHelper.Panel(null, tabs, "storage"));
		window.SetTitle("Hidden Wall Stash");
		GameItem stash = ItemDirectory.CreateEmptyItem(StashManager.ItemIdentifier);
		stash.SetContentWindow(window);
		stash.unitValue = 0L;
		stash.name = "Hidden Wall Stash";
		stash.shortDescription = "A concealed compartment behind a wall grate.";
		stash.EnableTag("CONTAINER_TAG");
		stash.EnableTag("ITEM_HIDDEN_TAG");
		stash.SetGameItemType("STORAGE");
		// Owned, like every other container in the shop. AutoStorage's Owned.Is() requires this before it
		// will consider an item a container, and FindAllItem(true) filters on it as well.
		stash.EnableTag("IS_OWNED_TAG");
		// AutoStorage discovers tabbed storage through its machine path (IsMachineItem -> AllMachineSlots ->
		// AddMachineryPanelSlots -> AddTabbedPanelSlots). Its container path needs ResolveInventory() to find a
		// GameInventory through the content window, which a machinery window of 10 tabs does not expose. The
		// MACHINE *tag* is only read by AutoStorage's Tags.Has; the game itself checks IsGameItemType("MACHINE"),
		// which stays unset, so this does not make the stash a machine anywhere else.
		stash.EnableTag("MACHINE");
		for (int i = 0; i < TabCount; i++)
		{
			MachineryIntake intake = tabs.GetPage(i) as MachineryIntake;
			GameGridInventory grid = intake?.content as GameGridInventory;
			if (grid == null)
			{
				HiddenWallStash.HiddenWallStashMod.Log.Error("Tab " + (i + 1) + " did not contain a grid inventory.");
				continue;
			}
			ContainerHelper.InitSmugglerBay(grid, stash);
			grid.onSlotAddItemEndFunc = (Action<GameItem, GameInventory, SlotMarker>)Delegate.Combine(grid.onSlotAddItemEndFunc, (Action<GameItem, GameInventory, SlotMarker>)StashManager.OnStashInventoryChanged);
			grid.onSlotRemoveItemEndFunc = (Action<GameItem, GameInventory, SlotMarker>)Delegate.Combine(grid.onSlotRemoveItemEndFunc, (Action<GameItem, GameInventory, SlotMarker>)StashManager.OnStashInventoryChanged);
			grid.onSlotChangeAmountItemFunc = (Action<GameItem, GameInventory, SlotMarker>)Delegate.Combine(grid.onSlotChangeAmountItemFunc, (Action<GameItem, GameInventory, SlotMarker>)StashManager.OnStashInventoryChanged);
		}

		stash.EnableTag("SYSTEM_TAG");
		stash.EnableTag("SYSTEM_TAG_UTILITY");
		return stash;
	}
}
