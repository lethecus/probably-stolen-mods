using System;

public static class StashItemFactory
{
	public const int TabCount = 10;
	public const int GridWidth = 15;
	public const int GridHeight = 13;

	public static GameItem Create()
	{
		MachineryTabs tabs = new MachineryTabs();
		for (int i = 0; i < TabCount; i++)
		{
			GameGridInventory grid = new GameGridInventory(GridWidth, GridHeight);
			grid.identifier = "smuggler_bay";
			tabs.AddTab(LocHelper.GetLocalizedMechanic("mech_storage_bay_tab", i + 1), MachineHelper.WrapInIntake(grid));
		}

		PixelWindow window = MachineHelper.CreateMachineryWindow(true, MachineHelper.Panel(null, tabs, "storage"));
		window.SetTitle("Hidden Wall Stash");
		GameItem stash = ItemDirectory.CreateEmptyItem(StashManager.ItemId);
		stash.SetContentWindow(window);
		stash.unitValue = 0L;
		stash.name = "Hidden Wall Stash";
		stash.shortDescription = "A concealed compartment behind a wall grate.";
		stash.EnableTag("CONTAINER_TAG");
		stash.EnableTag("ITEM_HIDDEN_TAG");
		stash.SetGameItemType("STORAGE");

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
