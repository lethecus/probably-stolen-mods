using System;
using System.Collections.Generic;

namespace BlankCardPrinter;

public class PrinterMod : IMod
{
	private const string ModId = "blank_card_printer";

	private const string ModName = "Blank Card Printer";

	private const string PrinterId = "blank_card_printer.printer";

	private const string CardId = "blank_keycard";

	private ModLog log;

	private ConfigEntry<int> printCost;

	public void Init(ModManifest manifest)
	{
		log = new ModLog(manifest);
		ModConfig config = ModHelper.GetConfig(manifest.ID);
		printCost = config.Define("print_cost", 0, "General", (Func<string>)(() => "Credits per card"), (int?)0, (int?)200, (Func<string>)(() => "Credits deducted each time the printer produces a card."), false);
	}

	public void OnEnable()
	{
		ModHook.OnModItemDirectoryInit += RegisterItems;
		ModHook.OnPlaceInventorInventoryItemLate += StockInventor;
		ModHelper.AddLootEntry("blank_card_printer", "toolTable", "blank_card_printer.printer", 5f);
		ModHelper.AddDebugFunction("blank_card_printer", "Spawn", "Spawn Card Printer", (Action)(() =>
		{
			ModHelper.MoveItemToInventory(ModHelper.SpawnItem("blank_card_printer.printer"));
		}), false);
		ModHelper.AddDebugFunction("blank_card_printer", "Spawn", "Spawn Blank Keycard", (Action)(() =>
		{
			ModHelper.MoveItemToInventory(ModHelper.SpawnItem("blank_keycard"));
		}), false);
	}

	public void OnDisable()
	{
		ModHook.OnModItemDirectoryInit -= RegisterItems;
		ModHook.OnPlaceInventorInventoryItemLate -= StockInventor;
		ModHelper.RemoveDebugFunctions("blank_card_printer");
	}

	private void RegisterItems(ModItemDirectory dir)
	{
		((Directory<GameItem>)(object)dir).Add("blank_card_printer.printer", (Func<GameItem>)(() =>
		{
			GameItem val = ItemDirectory.CreateEmptyItem("blank_card_printer.printer");
			val.name = "Card Printer";
			val.shortDescription = "Presses out blank access cards. Don't ask where the plastic comes from.";
			val.SetSpriteAndShapeFromMod("blank_card_printer", "card_printer");
			val.SetContentWindow(BuildWindow(val));
			return val;
		}));
	}

	private PixelWindow BuildWindow(GameItem printer)
	{
		MachineryText status = MachineryText.Status("status", "Ready");
		MachineryButton val = new MachineryButton("Print blank card", (Action)(() =>
		{
			Print(status, printer);
		}), -1, -1);
		return MachineHelper.CreateMachineryWindow(true, new MachineryPanel[1] { MachineHelper.Panel("Card Printer", (PixelElement)(object)MachineryStack.Vertical(new PixelElement[2]
		{
			(PixelElement)val,
			(PixelElement)status
		}), "control", true) });
	}

	private void Print(MachineryText status, GameItem printer)
	{
		int value = printCost.Value;
		if (value > 0)
		{
			ModHelper.AddCredit(-value, false);
		}
		GameItem card = ModHelper.SpawnItem("blank_keycard");
		if ((object)card == null)
		{
			log.Log((object)"Could not create a blank keycard.");
			return;
		}
		// Deposit into the inventory that currently holds the printer (storage container,
		// shelf, toolbox, table, extra storage page, ...). Fall back to the main
		// inventory when the printer is not inside anything or the container is full.
		GraphNodeStorage target = ((object)printer != null) ? printer.parentInventory : null;
		if (target != null)
		{
			GraphUtils.TryAcceptAll(target, card);
		}
		if (card.parentInventory == null)
		{
			ModHelper.MoveItemToInventory(card);
		}
		if (card.parentInventory == null)
		{
			GraphUtils.DestroyIfUnplaced(card, "blank_card_printer: no room for printed blank keycard");
			log.Log((object)"No room for the printed card; it was discarded.");
			return;
		}
		log.Log((object)"Printed a blank keycard.");
	}

	private void StockInventor(List<GameItem> owned)
	{
		PlayerStore.Instance.AddDirectSellingItemToTable("blank_card_printer.printer", false, false, false, 0);
	}
}
