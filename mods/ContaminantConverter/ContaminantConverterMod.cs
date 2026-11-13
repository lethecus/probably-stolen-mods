using System;
using System.Collections.Generic;

namespace ContaminantConverter
{
	public class ContaminantConverterMod : IMod
	{
		public const string ModId = "contaminant_converter";

		private const int InventorStockChance = 25;

		public static ModLog Log { get; private set; }

		public void Init(ModManifest manifest)
		{
			Log = new ModLog(manifest);
			ModHook.OnModItemDirectoryInit += OnModItemDirectoryInit;
			ModHook.OnPlaceInventorInventoryItemLate += OnInventorStock;
			ModHook.OnCreateTooltipLate += MachineContaminantConverter.CreateTooltip;
			ModHelper.AddDebugFunction(ModId, "Spawn", "Contaminant Converter", SpawnForTesting);
		}

		public void OnEnable()
		{
		}

		public void OnDisable()
		{
			ModHook.OnModItemDirectoryInit -= OnModItemDirectoryInit;
			ModHook.OnPlaceInventorInventoryItemLate -= OnInventorStock;
			ModHook.OnCreateTooltipLate -= MachineContaminantConverter.CreateTooltip;
			ModHelper.RemoveDebugFunctions(ModId);
		}

		private static void OnModItemDirectoryInit(ModItemDirectory directory)
		{
			if (directory != null && !directory.Has(ConverterConfig.ItemId))
			{
				directory.Add(ConverterConfig.ItemId, MachineContaminantConverter.Create);
			}
		}

		// The converter is no longer a starter item. Dr. Jackson's shop stocks one
		// now and then, and only while the player does not already own one.
		private static void OnInventorStock(List<GameItem> ownedItems)
		{
			try
			{
				if (EmporiumEntry.Instance == null || PlayerStore.Instance == null)
				{
					return;
				}
				if (OwnsConverter())
				{
					return;
				}
				if (!RNG.Roll(InventorStockChance))
				{
					return;
				}
				PlayerStore.Instance.AddDirectSellingItemToTable(ConverterConfig.ItemId, false, false, false, 0);
				Log?.Log("Dr. Jackson is stocking a Contaminant Converter today.");
			}
			catch (Exception exception)
			{
				Log?.Error("Could not stock the Contaminant Converter at Dr. Jackson's: " + exception);
			}
		}

		private static bool OwnsConverter()
		{
			foreach (GameItem item in EmporiumEntry.Instance.GetAllItems())
			{
				if (item != null && string.Equals(item.identifier, ConverterConfig.ItemId))
				{
					return true;
				}
			}
			return false;
		}

		private static void SpawnForTesting()
		{
			GameItem item = ModHelper.SpawnItem(ConverterConfig.ItemId);
			if (item != null)
			{
				ModHelper.MoveItemToInventory(item);
			}
		}
	}
}
