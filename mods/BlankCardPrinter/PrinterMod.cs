using System;
using System.Collections.Generic;

namespace BlankCardPrinter;

public class PrinterMod : IMod
{
	private const string ModId = "blank_card_printer";

	private const string ModName = "Blank Card Printer";

	private const string PrinterId = "blank_card_printer.printer";

	private const string CardId = "blank_keycard";

	// Easy mode keeps the original cost; normal mode also eats one printer material per card.
	private const int EasyPower = 3;

	private const int NormalPower = 6;

	private const int PrintCredits = 10;

	private const int MaterialPerCard = 1;

	private ModLog log;

	private ConfigEntry<bool> easyMode;

	private bool EasyMode => easyMode == null || easyMode.Value;

	public void Init(ModManifest manifest)
	{
		log = new ModLog(manifest);
	}

	public void OnEnable()
	{
		try
		{
			easyMode = ModConfig.For(ModId).Define("easy_mode", true, "Balance", () => "Easy mode", () => "On: 3 power and 10 credits per card, no material (original behaviour). Off: normal mode, 1 unit of 3D Printing Filament per card plus 6 power and 10 credits. Normal mode adds a filament slot; changing this needs a reload because the machine window is built when the printer is created.", false);
		}
		catch (Exception ex)
		{
			log?.Warning("Could not register the balance setting: " + ex.Message);
		}

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
			val.shortDescription = "Presses out blank access cards. Easy mode needs a battery, 3 power and 10 credits per card. Normal mode also uses 1 unit of 3D Printing Filament per card.";
			val.SetSpriteAndShapeFromMod("blank_card_printer", "card_printer");
			val.SetContentWindow(BuildWindow(val));
			return val;
		}));
	}

	private PixelWindow BuildWindow(GameItem printer)
	{
		GameSlotInventory battery = new GameSlotInventory();
		MachineHelper.SetupBatterySlot(battery, printer);

		GameSlotInventory material = null;
		if (!EasyMode)
		{
			material = new GameSlotInventory();
			material.mayInventoryAddItemFunc = (GameItem item, GameInventory _) => item != null && item.IsTag("PRINTER_MATERIAL_TAG");
		}

		MachineryText status = MachineryText.Status("status", "Ready");

		GameSlotInventory materialSlot = material;
		MachineryButton printButton = new MachineryButton("Print blank card", (Action)(() =>
		{
			Print(status, battery, materialSlot, printer);
		}), -1, -1);

		int power = EasyMode ? EasyPower : NormalPower;

		MachineryText costWarning = new MachineryText();
		costWarning.SetText("Costs " + PrintCredits + " credits per card");
		costWarning.SetColor(RenderHandler.GetColorFromPalette(RenderHandler.ColorPalette.LightOrange));

		MachineryText powerWarning = new MachineryText();
		powerWarning.SetText("Uses " + power + " power (battery required)");
		powerWarning.SetColor(RenderHandler.GetColorFromPalette(RenderHandler.ColorPalette.LightCyan));

		MachineryText materialWarning = new MachineryText();
		if (!EasyMode)
		{
			materialWarning.SetText("Uses " + MaterialPerCard + " 3D Printing Filament per card");
			materialWarning.SetColor(RenderHandler.GetColorFromPalette(RenderHandler.ColorPalette.LightGreen));
		}

		List<PixelElement> controls = new List<PixelElement>();
		controls.Add((PixelElement)(object)printButton);
		controls.Add((PixelElement)(object)costWarning);
		controls.Add((PixelElement)(object)powerWarning);
		if (!EasyMode)
		{
			controls.Add((PixelElement)(object)materialWarning);
		}
		controls.Add((PixelElement)(object)status);

		if (EasyMode)
		{
			return MachineHelper.CreateMachineryWindow(true, new MachineryPanel[2]
			{
				MachineHelper.PowerPanel(battery),
				MachineHelper.Panel("Card Printer", (PixelElement)(object)MachineryStack.Vertical(controls.ToArray()), "control", true)
			});
		}

		return MachineHelper.CreateMachineryWindow(true, new MachineryPanel[3]
		{
			MachineHelper.PowerPanel(battery),
			MachineHelper.Panel("3D Filament", (PixelElement)(object)material, "material"),
			MachineHelper.Panel("Card Printer", (PixelElement)(object)MachineryStack.Vertical(controls.ToArray()), "control", true)
		});
	}

	private void Print(MachineryText status, GameSlotInventory battery, GameSlotInventory material, GameItem printer)
	{
		int power = EasyMode ? EasyPower : NormalPower;

		GameItem powerSource = battery.childItem;
		if (powerSource == null)
		{
			SetStatus(status, "Insert a battery", RenderHandler.ColorPalette.LightRed);
			return;
		}
		if (!PowerHelper.CanDrawPowerSource(powerSource, power))
		{
			SetStatus(status, "Not enough power (" + power + " needed)", RenderHandler.ColorPalette.LightRed);
			return;
		}
		if (!PlayerStore.Instance.HasCredit(PrintCredits))
		{
			SetStatus(status, "Not enough credits (" + PrintCredits + " needed)", RenderHandler.ColorPalette.LightRed);
			return;
		}

		GameItem filament = material != null ? material.childItem : null;
		if (!EasyMode)
		{
			if (filament == null || !filament.IsTag("PRINTER_MATERIAL_TAG"))
			{
				SetStatus(status, "Insert 3D Printing Filament", RenderHandler.ColorPalette.LightRed);
				return;
			}
			if (filament.unitCount < MaterialPerCard)
			{
				SetStatus(status, "Needs " + MaterialPerCard + " 3D Printing Filament", RenderHandler.ColorPalette.LightRed);
				return;
			}
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

		PowerHelper.DrawPowerSource(powerSource, power);
		ModHelper.AddCredit(-PrintCredits, false);

		if (!EasyMode && filament != null)
		{
			filament.SetAmount(filament.unitCount - MaterialPerCard);
			if (filament.unitCount <= 0)
			{
				filament.Destroy();
			}
		}

		string used = EasyMode ? "" : (", -" + MaterialPerCard + " 3D Printing Filament");
		SetStatus(status, "Printed (-" + PrintCredits + " credits" + used + ")", RenderHandler.ColorPalette.LightGreen);
		log.Log((object)("Printed a blank keycard for " + PrintCredits + " credits, " + power + " power" + (EasyMode ? "." : " and " + MaterialPerCard + " 3D Printing Filament.")));
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
