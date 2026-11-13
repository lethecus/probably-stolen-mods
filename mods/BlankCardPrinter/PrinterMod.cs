using System;
using System.Collections.Generic;

namespace BlankCardPrinter;

public class PrinterMod : IMod
{
	private const string ModId = "blank_card_printer";

	private const string ModName = "Blank Card Printer";

	private const string PrinterId = "blank_card_printer.printer";

	private const string CardId = "blank_keycard";

	// One print costs this much power and this many credits.
	private const int PrintPower = 3;

	private const int PrintCredits = 10;

	private ModLog log;

	public void Init(ModManifest manifest)
	{
		log = new ModLog(manifest);
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
			val.shortDescription = "Presses out blank access cards. Needs a battery: 3 power and 10 credits per card.";
			val.SetSpriteAndShapeFromMod("blank_card_printer", "card_printer");
			val.SetContentWindow(BuildWindow(val));
			return val;
		}));
	}

	private PixelWindow BuildWindow(GameItem printer)
	{
		GameSlotInventory battery = new GameSlotInventory();
		MachineHelper.SetupBatterySlot(battery, printer);

		MachineryText status = MachineryText.Status("status", "Ready");

		MachineryButton printButton = new MachineryButton("Print blank card", (Action)(() =>
		{
			Print(status, battery, printer);
		}), -1, -1);

		MachineryText costWarning = new MachineryText();
		costWarning.SetText("Costs " + PrintCredits + " credits per card");
		costWarning.SetColor(RenderHandler.GetColorFromPalette(RenderHandler.ColorPalette.LightOrange));

		MachineryText powerWarning = new MachineryText();
		powerWarning.SetText("Uses " + PrintPower + " power (battery required)");
		powerWarning.SetColor(RenderHandler.GetColorFromPalette(RenderHandler.ColorPalette.LightCyan));

		return MachineHelper.CreateMachineryWindow(true, new MachineryPanel[2]
		{
			MachineHelper.PowerPanel(battery),
			MachineHelper.Panel("Card Printer", (PixelElement)(object)MachineryStack.Vertical(new PixelElement[4]
			{
				(PixelElement)printButton,
				(PixelElement)costWarning,
				(PixelElement)powerWarning,
				(PixelElement)status
			}), "control", true)
		});
	}

	private void Print(MachineryText status, GameSlotInventory battery, GameItem printer)
	{
		GameItem powerSource = battery.childItem;
		if (powerSource == null)
		{
			SetStatus(status, "Insert a battery", RenderHandler.ColorPalette.LightRed);
			return;
		}
		if (!PowerHelper.CanDrawPowerSource(powerSource, PrintPower))
		{
			SetStatus(status, "Not enough power (" + PrintPower + " needed)", RenderHandler.ColorPalette.LightRed);
			return;
		}
		if (!PlayerStore.Instance.HasCredit(PrintCredits))
		{
			SetStatus(status, "Not enough credits (" + PrintCredits + " needed)", RenderHandler.ColorPalette.LightRed);
			return;
		}

		GameItem card = ModHelper.SpawnItem(CardId);
		if ((object)card == null)
		{
			SetStatus(status, "Could not create a card", RenderHandler.ColorPalette.LightRed);
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
			SetStatus(status, "No room for the card", RenderHandler.ColorPalette.LightRed);
			log.Log((object)"No room for the printed card; nothing was charged.");
			return;
		}

		PowerHelper.DrawPowerSource(powerSource, PrintPower);
		ModHelper.AddCredit(-PrintCredits, false);
		SetStatus(status, "Printed (-" + PrintCredits + " credits)", RenderHandler.ColorPalette.LightGreen);
		log.Log((object)("Printed a blank keycard for " + PrintCredits + " credits and " + PrintPower + " power."));
	}

	private static void SetStatus(MachineryText status, string text, RenderHandler.ColorPalette color)
	{
		if (status != null)
		{
			status.SetText(text);
			status.SetColor(RenderHandler.GetColorFromPalette(color));
		}
	}

	private void StockInventor(List<GameItem> owned)
	{
		PlayerStore.Instance.AddDirectSellingItemToTable("blank_card_printer.printer", false, false, false, 0);
	}
}
