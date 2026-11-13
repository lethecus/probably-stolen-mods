using System;
using System.Collections.Generic;
using UnityEngine;

namespace AutoSorterPlus
{
	internal sealed class ContainerTarget
	{
		internal GameItem Item;
		internal GameInventory Inv;
		internal string Slot;
		internal string SlotLabel;

		internal string Key => Rules.Key(Item, Slot);

		internal string Title
		{
			get
			{
				string name = Item != null ? Item.name : "?";
				return string.IsNullOrEmpty(SlotLabel) ? name : name + " · " + SlotLabel;
			}
		}
	}

	internal static class World
	{
		private static readonly List<ContainerTarget> TargetsCache = new List<ContainerTarget>();
		private static float _lastScan = -1000f;

		// Inventories that belong to the player's working areas; never targets.
		private static readonly HashSet<object> Protected = new HashSet<object>();

		internal static List<ContainerTarget> Targets(bool force)
		{
			if (!force && Time.realtimeSinceStartup - _lastScan < 3f)
			{
				return TargetsCache;
			}
			_lastScan = Time.realtimeSinceStartup;
			TargetsCache.Clear();
			RebuildProtected();

			HashSet<object> seen = new HashSet<object>();
			List<GameItem> items = AllItems();
			for (int i = 0; i < items.Count; i++)
			{
				GameItem item = items[i];
				if (item == null || !Owned(item) || IsBag(item) || !seen.Add(item.uniqueId))
				{
					continue;
				}
				try
				{
					AddTargets(item);
				}
				catch (Exception exception)
				{
					Core.LogWarning("Target scan: " + exception.Message);
				}
			}
			return TargetsCache;
		}

		private static void AddTargets(GameItem item)
		{
			MachineryPanelRow row = MachineryRow(item);
			if (row != null)
			{
				AddMachineTargets(item, row);
				return;
			}
			if (!IsStorageItem(item))
			{
				return;
			}
			if (!IsInventoryItem(item, out GameInventory inventory) || inventory == null || Protected.Contains(Ptr(inventory)))
			{
				return;
			}
			TargetsCache.Add(new ContainerTarget { Item = item, Inv = inventory });
		}

		private static void AddMachineTargets(GameItem item, MachineryPanelRow row)
		{
			if (IsFridge(item))
			{
				try
				{
					GameInventory chill = MachineFridge.GetChillZone(item);
					if (chill != null && !Protected.Contains(Ptr(chill)))
					{
						TargetsCache.Add(new ContainerTarget { Item = item, Inv = chill, Slot = "chill", SlotLabel = Core.Loc("asp.slot.fridge.chill") });
					}
				}
				catch
				{
				}
				try
				{
					GameInventory freeze = MachineFridge.GetFreezeZone(item);
					if (freeze != null && !Protected.Contains(Ptr(freeze)))
					{
						TargetsCache.Add(new ContainerTarget { Item = item, Inv = freeze, Slot = "freeze", SlotLabel = Core.Loc("asp.slot.fridge.freeze") });
					}
				}
				catch
				{
				}
				return;
			}

			IReadOnlyList<MachineryPanel> panels = row.Panels;
			for (int i = 0; i < panels.Count; i++)
			{
				MachineryPanel panel = panels[i];
				if (panel == null || panel.isHidden)
				{
					continue;
				}
				string role = panel.role;
				if (role == "battery" || role == "module" || role == "note" || role == "power")
				{
					continue;
				}
				MachineryTabs tabs = panel.content as MachineryTabs;
				if (tabs != null && tabs.count > 0)
				{
					for (int page = 0; page < tabs.count; page++)
					{
						string caption = tabs.GetCaption(page);
						if (string.IsNullOrEmpty(caption))
						{
							caption = (page + 1).ToString();
						}
						List<GraphNodeStorage> storages = new List<GraphNodeStorage>();
						try
						{
							MachineryContainerUtils.Collect(tabs.GetPage(page), storages);
						}
						catch
						{
						}
						for (int s = 0; s < storages.Count; s++)
						{
							if (storages[s] is GameInventory inventory && !Protected.Contains(Ptr(inventory)))
							{
								string slot = (string.IsNullOrEmpty(role) ? "tab" : role) + ".tab" + page + (s > 0 ? "_" + s : "");
								TargetsCache.Add(new ContainerTarget { Item = item, Inv = inventory, Slot = slot, SlotLabel = caption });
							}
						}
					}
					continue;
				}
				List<GraphNodeStorage> list = new List<GraphNodeStorage>();
				try
				{
					panel.CollectStorage(list);
				}
				catch
				{
				}
				string label = null;
				try
				{
					label = panel.label != null ? panel.label.GetText() : null;
				}
				catch
				{
				}
				for (int s = 0; s < list.Count; s++)
				{
					if (list[s] is GameInventory inventory && !Protected.Contains(Ptr(inventory)))
					{
						string slot = string.IsNullOrEmpty(role) ? "slot" + i : role;
						if (HasSlot(item, slot))
						{
							slot = slot + "_" + s;
						}
						TargetsCache.Add(new ContainerTarget { Item = item, Inv = inventory, Slot = slot, SlotLabel = label ?? role });
					}
				}
			}
		}

		private static bool HasSlot(GameItem item, string slot)
		{
			for (int i = 0; i < TargetsCache.Count; i++)
			{
				if (TargetsCache[i].Item == item && TargetsCache[i].Slot == slot)
				{
					return true;
				}
			}
			return false;
		}

		internal static List<GameItem> Sources()
		{
			List<GameItem> result = new List<GameItem>();
			HashSet<int> seen = new HashSet<int>();
			EmporiumEntry entry = EmporiumEntry.Instance;
			if (entry != null)
			{
				AddChildren(entry.invElement, result, seen);
				AddChildren(entry.frontInvinvElement, result, seen);
				AddChildren(entry.backInvinvElement, result, seen);
				AddChildren(entry.backInvinvElementCounter, result, seen);
				AddChildren(entry.hiddenElement, result, seen);
				try
				{
					List<GameItem> counters = entry.GetCountersItems();
					if (counters != null)
					{
						for (int i = 0; i < counters.Count; i++)
						{
							AddSource(counters[i], result, seen);
						}
					}
				}
				catch
				{
				}
			}
			// Contents of bags carried anywhere on the player.
			List<GameItem> bags = new List<GameItem>(result);
			for (int i = 0; i < bags.Count; i++)
			{
				if (IsBag(bags[i]))
				{
					MineBag(bags[i], result, seen, 0);
				}
			}
			return result;
		}

		private static void AddChildren(GameInventory inventory, List<GameItem> result, HashSet<int> seen)
		{
			if (inventory == null)
			{
				return;
			}
			try
			{
				List<GameItem> children = inventory.childItems;
				if (children == null || children.Count > 4000)
				{
					return;
				}
				for (int i = 0; i < children.Count; i++)
				{
					AddSource(children[i], result, seen);
				}
			}
			catch
			{
			}
		}

		private static void AddSource(GameItem item, List<GameItem> result, HashSet<int> seen)
		{
			if (item != null && seen.Add(item.uniqueId))
			{
				result.Add(item);
			}
		}

		private static void MineBag(GameItem bag, List<GameItem> result, HashSet<int> seen, int depth)
		{
			if (depth > 3 || !IsInventoryItem(bag, out GameInventory inventory) || inventory == null)
			{
				return;
			}
			List<GameItem> children = new List<GameItem>(inventory.childItems);
			for (int i = 0; i < children.Count; i++)
			{
				GameItem child = children[i];
				if (child == null || !seen.Add(child.uniqueId))
				{
					continue;
				}
				result.Add(child);
				if (IsBag(child))
				{
					MineBag(child, result, seen, depth + 1);
				}
			}
		}

		internal static List<GameItem> AllItems()
		{
			List<GameItem> result = new List<GameItem>();
			HashSet<int> seen = new HashSet<int>();
			try
			{
				EmporiumEntry entry = EmporiumEntry.Instance;
				if (entry != null)
				{
					List<GameItem> all = entry.GetAllItems();
					for (int i = 0; i < all.Count; i++)
					{
						AddSource(all[i], result, seen);
					}
				}
			}
			catch
			{
			}
			try
			{
				PlayerStore store = PlayerStore.Instance;
				if (store != null)
				{
					List<GameItem> all = store.FindAllItem(false);
					for (int i = 0; i < all.Count; i++)
					{
						AddSource(all[i], result, seen);
					}
				}
			}
			catch
			{
			}
			return result;
		}

		private static void RebuildProtected()
		{
			Protected.Clear();
			try
			{
				EmporiumEntry entry = EmporiumEntry.Instance;
				if (entry == null)
				{
					return;
				}
				Protect(entry.invElement as GameInventory);
				Protect(entry.frontInvinvElement as GameInventory);
				Protect(entry.backInvinvElement as GameInventory);
				Protect(entry.backInvinvElementCounter as GameInventory);
				Protect(entry.hiddenElement as GameInventory);
				Protect(entry.soldElement as GameInventory);
			}
			catch
			{
			}
		}

		private static void Protect(GameInventory inventory)
		{
			if (inventory != null)
			{
				Protected.Add(Ptr(inventory));
			}
		}

		internal static bool Owned(GameItem item)
		{
			try
			{
				return GeneralHelper.IsItemEffectivelyOwned(item);
			}
			catch
			{
				return false;
			}
		}

		internal static bool IsBag(GameItem item)
		{
			try
			{
				return item != null && (item.IsTag("BACKPACK_TAG") || item.IsTag("POUCH_TAG") || item.IsTag("BAG_TAG"));
			}
			catch
			{
				return false;
			}
		}

		internal static bool IsMachine(GameItem item)
		{
			try
			{
				return item != null && item.IsTag("MACHINE");
			}
			catch
			{
				return false;
			}
		}

		internal static bool IsContainer(GameItem item)
		{
			return IsStorageItem(item);
		}

		// Storage furniture/machines only. Liquid containers, bottles and bags also
		// own inventories but are cargo, not sorting targets.
		internal static bool IsStorageItem(GameItem item)
		{
			if (item == null)
			{
				return false;
			}
			try
			{
				if (item.IsTag("STORAGE") || item.IsTag("CAGE_TAG"))
				{
					return true;
				}
			}
			catch
			{
			}
			string id = item.identifier ?? "";
			return id.IndexOf("storage", StringComparison.OrdinalIgnoreCase) >= 0
				|| id.IndexOf("crate", StringComparison.OrdinalIgnoreCase) >= 0
				|| id.IndexOf("toolbox", StringComparison.OrdinalIgnoreCase) >= 0
				|| id.IndexOf("cabinet", StringComparison.OrdinalIgnoreCase) >= 0
				|| id.IndexOf("locker", StringComparison.OrdinalIgnoreCase) >= 0
				|| id.IndexOf("animal_cage", StringComparison.OrdinalIgnoreCase) >= 0;
		}

		internal static bool IsCage(GameItem item)
		{
			return item != null && string.Equals(item.identifier, "animal_cage", StringComparison.OrdinalIgnoreCase);
		}

		internal static bool IsInventoryItem(GameItem item, out GameInventory inventory)
		{
			inventory = ResolveInventory(item);
			return inventory != null;
		}

		// Container inventories can hang off the item in several ways: a window child,
		// a grid of slot elements, the item's graph children, or a nested graph node.
		internal static GameInventory ResolveInventory(GameItem item)
		{
			if (item == null)
			{
				return null;
			}
			try
			{
				PixelWindow window = item.contentWindow;
				if (window != null)
				{
					GameInventory fromWindow = FromWindow(window);
					if (fromWindow != null)
					{
						return fromWindow;
					}
				}
			}
			catch
			{
			}
			try
			{
				List<GraphNodeStorage> children = item.children;
				if (children != null && children.Count > 0 && children.Count <= 256)
				{
					for (int i = 0; i < children.Count; i++)
					{
						if (children[i] is GameInventory inventory)
						{
							return inventory;
						}
					}
				}
			}
			catch
			{
			}
			try
			{
				GraphNodeWindow child = item.child;
				if (child != null)
				{
					return DigInventory(child.child, 0);
				}
			}
			catch
			{
			}
			return null;
		}

		private static GameInventory FromWindow(PixelWindow window)
		{
			if (window == null)
			{
				return null;
			}
			try
			{
				if (window.childElement is GameInventory direct)
				{
					return direct;
				}
			}
			catch
			{
			}
			try
			{
				if (window.child is GameInventory childInventory)
				{
					return childInventory;
				}
			}
			catch
			{
			}
			try
			{
				if (window.childElement is GridPixelElement grid)
				{
					int width = Mathf.Min(grid.gridWidth, 64);
					int height = Mathf.Min(grid.gridHeight, 64);
					for (int y = 0; y < height; y++)
					{
						for (int x = 0; x < width; x++)
						{
							if (grid.GetElement(x, y) is GameInventory slotInventory)
							{
								return slotInventory;
							}
						}
					}
				}
			}
			catch
			{
			}
			return null;
		}

		private static GameInventory DigInventory(GraphNodeStorage node, int depth)
		{
			if (node == null || depth > 4)
			{
				return null;
			}
			if (node is GameInventory inventory)
			{
				return inventory;
			}
			try
			{
				List<GraphNodeStorage> children = node.children;
				if (children != null && children.Count > 0 && children.Count <= 256)
				{
					for (int i = 0; i < children.Count; i++)
					{
						GameInventory found = DigInventory(children[i], depth + 1);
						if (found != null)
						{
							return found;
						}
					}
				}
			}
			catch
			{
			}
			return null;
		}

		private static MachineryPanelRow MachineryRow(GameItem item)
		{
			try
			{
				if (item?.contentWindow?.childElement is MachineryPanelRow row)
				{
					return row;
				}
			}
			catch
			{
			}
			return null;
		}

		internal static bool IsFridge(GameItem item)
		{
			return item != null && string.Equals(item.identifier, "fridge", StringComparison.OrdinalIgnoreCase);
		}

		internal static bool CanAccept(GameInventory inventory, GameItem item)
		{
			try
			{
				return inventory != null && item != null && GraphUtils.CanAccept(inventory, item);
			}
			catch
			{
				return false;
			}
		}

		internal static bool Move(GameItem item, GameInventory target)
		{
			if (item == null || target == null)
			{
				return false;
			}
			GameInventory origin = null;
			try
			{
				origin = item.parentInventory;
			}
			catch
			{
			}
			if (origin == null || ReferenceEquals(origin, target))
			{
				return false;
			}
			try
			{
				if (origin.IsRemoveLocked() || target.IsInsertLocked())
				{
					return false;
				}
			}
			catch
			{
				return false;
			}
			int moved = -1;
			try
			{
				moved = GraphUtils.TryAcceptAll(target, item, -1);
			}
			catch (Exception exception)
			{
				Core.LogWarning("Move failed: " + exception.Message);
			}
			if (moved == 0)
			{
				return true;
			}
			try
			{
				if (item.parentInventory == null)
				{
					GraphUtils.TryAcceptAll(origin, item, -1);
				}
			}
			catch
			{
			}
			return false;
		}

		private static object Ptr(object obj)
		{
			return obj;
		}
	}
}
