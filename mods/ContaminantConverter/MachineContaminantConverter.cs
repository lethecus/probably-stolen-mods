using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using TMPro;
using UnityEngine;

namespace ContaminantConverter
{
	public static class MachineContaminantConverter
	{
		private sealed class Context
		{
			public GameSlotInventory Battery;
			public GameGridInventory Modules;
			public GameSlotInventory Source;
			public GameSlotInventory Target;
			public MachineryRadioList Contaminants;
			public MachineryRadioButton Acid;
			public MachineryRadioButton Zyanide;
			public MachineryRadioGroup ChemicalGroup;
			public MachineryText Product;
			public MachineryButton Convert;
			public MachineryText Status;
			public MachineryStack OptionsStack;
			public PixelWindow Window;
		}

		private static readonly ConditionalWeakTable<GameItem, Context> Contexts = new ConditionalWeakTable<GameItem, Context>();

		public static GameItem Create()
		{
			GameSlotInventory battery = new GameSlotInventory();
			GameGridInventory modules = new GameGridInventory(ConverterConfig.ModuleShape, 7);
			GameSlotInventory source = new GameSlotInventory();
			GameSlotInventory target = new GameSlotInventory();

			List<string> captions = new List<string>();
			for (int i = 0; i < ConverterConfig.Contaminants.Length; i++)
			{
				captions.Add(ContaminantName(i));
			}
			// Only one contaminant at a time.
			MachineryRadioList contaminants = new MachineryRadioList(captions, 0, null);

			MachineryRadioButton acid = new MachineryRadioButton(LiquidName("acid", "Acid"), true);
			MachineryRadioButton zyanide = new MachineryRadioButton(LiquidName("zyanide", "Zyanide"), false);
			MachineryRadioGroup chemicalGroup = new MachineryRadioGroup();
			chemicalGroup.Add(acid);
			chemicalGroup.Add(zyanide);

			MachineryText product = new MachineryText("Product: " + LiquidName("acid", "Acid"), 6, HorizontalAlignmentOptions.Left, false);
			product.SetFillWidth(false);

			MachineryText status = new MachineryText("Ready", 6, HorizontalAlignmentOptions.Left, false);
			status.SetFillWidth(false);

			MachineryButton convert = new MachineryButton("Convert", null, -1, -1);

			MachineryStack chemicalRow = MachineryStack.Horizontal(acid, zyanide);
			MachineryStack optionsStack = MachineryStack.Vertical(contaminants, product, chemicalRow);

			PixelElement controlContent = MachineryStack.Vertical(convert, status);

			MachineryPanel[] panels =
			{
				MachineHelper.PowerPanel(battery),
				MachineHelper.Panel("Modules", modules, "module", true),
				MachineHelper.Panel("Source", source, "source", true),
				MachineHelper.Panel("Target", target, "target", true),
				MachineHelper.Panel("Extract", optionsStack, "options", true),
				MachineHelper.Panel("Control", controlContent, "control", true)
			};

			PixelWindow contentWindow = MachineHelper.CreateMachineryWindow(true, panels);
			GameItem machine = ItemDirectory.CreateEmptyItem((string)null).SetContentWindow(contentWindow);
			ApplySprite(machine, false);
			ForceShape(machine);
			machine.name = ConverterConfig.Name;
			machine.shortDescription = "Extracts one selected contaminant from the source container and converts it into a useful chemical in the target container.";
			machine.flavorText = "A compact chemistry bench: dirty water in, reagents out.";
			machine.unitValue = ConverterConfig.UnitValue;
			machine.SetGameItemType("MACHINE");
			machine.EnableTag("CONTAINER_TAG", false);
			machine.EnableTag("STANDARD_MACHINE_TAG", false);
			MachineryHelper.InitMachinery(machine, ConverterConfig.PowerPerPass, false);
			MachineHelper.SetupBatterySlot(battery, machine);
			MachineHelper.SetupModuleBay(modules, machine, null, ConverterConfig.AllowedModuleTypes);
			SetupLiquidSlot(source);
			SetupLiquidSlot(target);
			AudioHelper.InitMetalMachine(machine);

			machine.ModifyTag(ConverterConfig.TagSelected, state => state.Enable().SetInt(0));
			machine.ModifyTag(ConverterConfig.TagChemical, state => state.Enable().SetInt(0));

			Context ctx = new Context
			{
				Battery = battery,
				Modules = modules,
				Source = source,
				Target = target,
				Contaminants = contaminants,
				Acid = acid,
				Zyanide = zyanide,
				ChemicalGroup = chemicalGroup,
				Product = product,
				Convert = convert,
				Status = status,
				OptionsStack = optionsStack,
				Window = contentWindow
			};
			Contexts.Remove(machine);
			Contexts.Add(machine, ctx);

			source.onSlotAddItemEndFunc = (Action<GameItem, GameInventory, SlotMarker>)Delegate.Combine(source.onSlotAddItemEndFunc, (Action<GameItem, GameInventory, SlotMarker>)((GameItem item, GameInventory inv, SlotMarker slot) =>
			{
				ApplySprite(machine, true);
				SetStatus(ctx, "Ready", RenderHandler.ColorPalette.LightGray);
			}));
			source.onSlotRemoveItemEndFunc = (Action<GameItem, GameInventory, SlotMarker>)Delegate.Combine(source.onSlotRemoveItemEndFunc, (Action<GameItem, GameInventory, SlotMarker>)((GameItem item, GameInventory inv, SlotMarker slot) =>
			{
				ApplySprite(machine, false);
				SetStatus(ctx, "Ready", RenderHandler.ColorPalette.LightGray);
			}));
			target.onSlotAddItemEndFunc = (Action<GameItem, GameInventory, SlotMarker>)Delegate.Combine(target.onSlotAddItemEndFunc, (Action<GameItem, GameInventory, SlotMarker>)((GameItem item, GameInventory inv, SlotMarker slot) =>
			{
				SetStatus(ctx, "Ready", RenderHandler.ColorPalette.LightGray);
			}));
			target.onSlotRemoveItemEndFunc = (Action<GameItem, GameInventory, SlotMarker>)Delegate.Combine(target.onSlotRemoveItemEndFunc, (Action<GameItem, GameInventory, SlotMarker>)((GameItem item, GameInventory inv, SlotMarker slot) =>
			{
				SetStatus(ctx, "Ready", RenderHandler.ColorPalette.LightGray);
			}));

			convert.SetCallback((Action)(() => TryConvert(machine)));

			contaminants.group.onChanged = (Action<int>)((int index) =>
			{
				SaveSelections(machine, ctx);
				UpdateProduct(ctx);
				SetStatus(ctx, "Ready", RenderHandler.ColorPalette.LightGray);
			});
			chemicalGroup.onChanged = (Action<int>)((int index) =>
			{
				SaveSelections(machine, ctx);
				UpdateProduct(ctx);
				SetStatus(ctx, "Ready", RenderHandler.ColorPalette.LightGray);
			});

			machine.onLoaded = (GameItem loaded) =>
			{
				loaded.name = ConverterConfig.Name;
				loaded.shortDescription = "Extracts one selected contaminant from the source container and converts it into a useful chemical in the target container.";
				loaded.flavorText = "A compact chemistry bench: dirty water in, reagents out.";
				bool hasSource = Contexts.TryGetValue(loaded, out Context loadedCtx) && loadedCtx.Source.childItem != null;
				ApplySprite(loaded, hasSource);
				ForceShape(loaded);
				if (loadedCtx != null)
				{
					ApplySelections(loaded, loadedCtx);
				}
			};

			ApplySelections(machine, ctx);
			return machine;
		}

		private static string ContaminantName(int index)
		{
			return LiquidName(ConverterConfig.Contaminants[index], ConverterConfig.Contaminants[index]);
		}

		private static string LiquidName(string id, string fallback)
		{
			try
			{
				Liquid liquid = Liquid.GetLiquidFromId(id);
				if (liquid != null && !string.IsNullOrEmpty(liquid.displayName))
				{
					return liquid.displayName;
				}
			}
			catch (Exception)
			{
			}
			return fallback;
		}

		private static void SetupLiquidSlot(GameSlotInventory slot)
		{
			slot.mayInventoryAddItemFunc = (Func<GameItem, GameInventory, bool>)((GameItem item, GameInventory inv) =>
				item != null
				&& GeneralHelper.IsItemOwned(item)
				&& item.IsTag("LIQUID_CONTAINER_TAG")
				&& !item.IsTag("WINE_CONTAINER_TAG"));
			slot.SetBackgroundFadeSprite("Items/water_container3", "small", 0.5f, 0.5f);
		}

		private static void ApplySprite(GameItem machine, bool hasLiquid)
		{
			if (machine == null)
			{
				return;
			}
			string key = hasLiquid ? "contaminant_converter_active" : "contaminant_converter";
			try
			{
				LoadedMod mod = ModLoader.Mods.Find((LoadedMod m) => m.Manifest.ID.Equals(ContaminantConverterMod.ModId));
				if (mod != null && mod.Sprites != null && mod.Sprites.ContainsKey(key))
				{
					machine.SetSpriteAndShapeFromMod(ContaminantConverterMod.ModId, key);
					return;
				}
			}
			catch (Exception)
			{
			}
			machine.SetSpriteAndShape("Items/items_tool2", hasLiquid ? "chem_finisher_b" : "chem_finisher");
		}

		private static void ForceShape(GameItem machine)
		{
			if (machine != null)
			{
				machine.SetShape(new GridShapeBuilder().SetData(ConverterConfig.ShapeData, ConverterConfig.ShapeWidth));
			}
		}

		private static int SelectedIndex(Context ctx)
		{
			int index = ctx.Contaminants.selected;
			if (index < 0 || index >= ConverterConfig.Contaminants.Length)
			{
				index = 0;
			}
			return index;
		}

		private static int SelectedChemical(Context ctx)
		{
			int chemical = ctx.ChemicalGroup.selected;
			return chemical == 1 ? 1 : 0;
		}

		private static string SelectedProductId(Context ctx)
		{
			int index = SelectedIndex(ctx);
			if (index == 0)
			{
				return SelectedChemical(ctx) == 1 ? ConverterConfig.AltChemicalProduct : ConverterConfig.Products[0];
			}
			return ConverterConfig.Products[index];
		}

		private static string SelectedProductName(Context ctx)
		{
			string id = SelectedProductId(ctx);
			return LiquidName(id, id);
		}

		private static void UpdateProduct(Context ctx)
		{
			int index = SelectedIndex(ctx);
			bool chemical = index == 0;
			if (chemical)
			{
				ctx.Acid.Show();
				ctx.Zyanide.Show();
			}
			else
			{
				ctx.Acid.Hide();
				ctx.Zyanide.Hide();
			}
			ctx.Product.SetText("Product: " + SelectedProductName(ctx));
			ctx.OptionsStack.Validate();
			if (ctx.Window != null)
			{
				ctx.Window.Validate();
			}
		}

		private static void SaveSelections(GameItem machine, Context ctx)
		{
			int selected = SelectedIndex(ctx);
			int chemical = SelectedChemical(ctx);
			machine.ModifyTag(ConverterConfig.TagSelected, state => state.Enable().SetInt(selected));
			machine.ModifyTag(ConverterConfig.TagChemical, state => state.Enable().SetInt(chemical));
		}

		private static void ApplySelections(GameItem machine, Context ctx)
		{
			int selected = machine.GetTagReadonly(ConverterConfig.TagSelected).GetInt();
			if (selected < 0 || selected >= ConverterConfig.Contaminants.Length)
			{
				selected = 0;
			}
			ctx.Contaminants.Select(selected, false);
			int chemical = machine.GetTagReadonly(ConverterConfig.TagChemical).GetInt();
			ctx.Acid.SetOn(chemical != 1, false);
			ctx.Zyanide.SetOn(chemical == 1, false);
			UpdateProduct(ctx);
		}

		private static int GetVolumePerPass(GameItem machine)
		{
			int bonus = 0;
			try
			{
				bonus = MachineryHelper.GetCurrentPerformanceBonus(machine);
			}
			catch (Exception)
			{
				bonus = 0;
			}
			float multiplier = 1f + (float)bonus / 100f;
			if (multiplier < 0f)
			{
				multiplier = 0f;
			}
			return (int)Math.Round((double)ConverterConfig.BaseVolumePerPass * (double)multiplier);
		}

		private static int GetPowerCost(GameItem machine)
		{
			int power = ConverterConfig.PowerPerPass;
			try
			{
				power = MachineryHelper.GetMachinePowerUsage(machine);
			}
			catch (Exception)
			{
				power = ConverterConfig.PowerPerPass;
			}
			// Efficiency modules may drive the stat below one; the machine
			// never runs for free.
			return Math.Max(1, power);
		}

		private static void SetStatus(Context ctx, string text, RenderHandler.ColorPalette color)
		{
			if (ctx != null && ctx.Status != null)
			{
				ctx.Status.SetText(text);
				ctx.Status.SetColor(RenderHandler.GetColorFromPalette(color));
			}
		}

		public static bool TryConvert(GameItem machine)
		{
			if (machine == null || !Contexts.TryGetValue(machine, out Context ctx))
			{
				return false;
			}

			GameItem source = ctx.Source.childItem;
			GameItem target = ctx.Target.childItem;
			if (source == null)
			{
				SetStatus(ctx, "Place a source container", RenderHandler.ColorPalette.LightRed);
				return false;
			}
			if (target == null)
			{
				SetStatus(ctx, "Place a target container", RenderHandler.ColorPalette.LightRed);
				return false;
			}
			if (!source.IsTag("LIQUID_CONTAINER_TAG") || !target.IsTag("LIQUID_CONTAINER_TAG"))
			{
				SetStatus(ctx, "Both slots need liquid containers", RenderHandler.ColorPalette.LightRed);
				return false;
			}

			GameItem battery = ctx.Battery.childItem;
			if (battery == null)
			{
				SetStatus(ctx, "Insert a battery", RenderHandler.ColorPalette.LightRed);
				return false;
			}
			int power = GetPowerCost(machine);
			if (!PowerHelper.CanDrawPowerSource(battery, power))
			{
				SetStatus(ctx, "Not enough power", RenderHandler.ColorPalette.LightRed);
				return false;
			}

			int targetFree = WaterHelper.GetFreeCapacity(target);
			if (targetFree <= 0)
			{
				SetStatus(ctx, "Target is full", RenderHandler.ColorPalette.LightRed);
				return false;
			}

			int index = SelectedIndex(ctx);
			string part = Liquid.GetCurrentPartFromId(ConverterConfig.Contaminants[index]);
			int amount = source.GetTagReadonly(part).GetInt();
			if (amount <= 0)
			{
				SetStatus(ctx, "No " + ContaminantName(index) + " in the source", RenderHandler.ColorPalette.LightRed);
				return false;
			}

			int converted = Math.Min(Math.Min(GetVolumePerPass(machine), targetFree), amount);
			if (converted <= 0)
			{
				SetStatus(ctx, "Target is full", RenderHandler.ColorPalette.LightRed);
				return false;
			}

			int remaining = amount - converted;
			source.ModifyTag(part, state => state.Enable().SetInt(remaining));

			string productId = SelectedProductId(ctx);
			WaterHelper.AddLiquid(target, productId, converted);

			WaterFeatureHelper.OnLiquidChanged(source, false);
			SpriteHelper.UpdateWaterContainerSprite(source);
			WaterHelper.UpdateWaterBottleSound(source);
			WaterHelper.UpdateWaterBottleSound(target);
			PowerHelper.DrawPowerSource(battery, power);
			MachineHelper.OnMachineActioned(machine, ctx.Modules);
			AudioHelper.UseWaterFilter();
			SetStatus(ctx, "Converted " + ((float)converted / 1000f).ToString("0.#") + " mL of " + SelectedProductName(ctx), RenderHandler.ColorPalette.LightGreen);
			return true;
		}

		public static void CreateTooltip(RichTextBuilder builder, GameItem item)
		{
			if (item == null || !string.Equals(item.identifier, ConverterConfig.ItemId))
			{
				return;
			}
			builder.AddLine(ConverterConfig.Name);
			builder.AddLine("Power per use: " + GetPowerCost(item));
			builder.AddLine("Throughput: " + ((float)GetVolumePerPass(item) / 1000f).ToString("0.#") + " mL per pass");
			if (Contexts.TryGetValue(item, out Context ctx))
			{
				int index = SelectedIndex(ctx);
				builder.AddLine("Selected: " + ContaminantName(index));
				builder.AddLine("Product: " + SelectedProductName(ctx));
			}
			builder.AddLine("Select one contaminant, then press Convert.");
		}
	}
}
