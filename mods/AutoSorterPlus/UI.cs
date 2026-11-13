using System;
using System.Collections.Generic;
using UnityEngine;

namespace AutoSorterPlus
{
	internal static class UI
	{
		private const string PanelPrefix = "AutoSorterPlus.panel.";
		private const string OverviewId = "AutoSorterPlus.overview";
		private static readonly List<string> Windows = new List<string>();
		private static readonly Dictionary<string, string> DraftInclude = new Dictionary<string, string>(StringComparer.Ordinal);
		private static readonly Dictionary<string, string> DraftExclude = new Dictionary<string, string>(StringComparer.Ordinal);

		internal static bool IsCapturing => false;

		internal static void CloseAll()
		{
			for (int i = 0; i < Windows.Count; i++)
			{
				try
				{
					CustomUIManager.Instance.CloseWindow(Windows[i]);
				}
				catch
				{
				}
			}
			Windows.Clear();
		}

		private static string WindowId(ContainerTarget target)
		{
			string key = target.Key ?? "?";
			return PanelPrefix + key.Replace('#', '_').Replace('@', '_').Replace('.', '_');
		}

		private static void Track(string id)
		{
			if (!Windows.Contains(id))
			{
				Windows.Add(id);
			}
		}

		internal static void TryOpenFromClick()
		{
			try
			{
				if (ItemSelectHandler.current != null && ItemSelectHandler.current.IsEquipped)
				{
					return;
				}
				if (ItemPinHelper.BlocksClicks)
				{
					return;
				}
				if (ItemMouseDragHandler.current != null && ItemMouseDragHandler.current.IsDraggingItem)
				{
					return;
				}
				GameItem item = RenderHandler.RaycastElement<GameItem>(Input.mousePosition, null);
				if (item == null || GeneralHelper.IsDisplayOnly(item))
				{
					return;
				}
				Open(item);
			}
			catch (Exception exception)
			{
				Core.LogWarning("Open settings: " + exception.Message);
			}
		}

		internal static void Open(GameItem item)
		{
			if (item == null)
			{
				return;
			}
			List<ContainerTarget> targets = new List<ContainerTarget>();
			List<ContainerTarget> all = World.Targets(false);
			for (int i = 0; i < all.Count; i++)
			{
				if (ReferenceEquals(all[i].Item, item))
				{
					targets.Add(all[i]);
				}
			}
			if (targets.Count == 0)
			{
				bool owned = World.Owned(item);
				bool bag = World.IsBag(item);
				bool machine = World.IsMachine(item);
				bool resolved = World.ResolveInventory(item) != null;
				Core.LogWarning(Core.Loc("asp.log.notContainer") + " [" + item.identifier + " owned=" + owned + " bag=" + bag + " machine=" + machine + " inv=" + resolved + " total=" + all.Count + "]");
				return;
			}
			OpenPanel(item, targets, 0);
		}

		private static void OpenPanel(GameItem item, List<ContainerTarget> targets, int index)
		{
			if (targets == null || targets.Count == 0)
			{
				return;
			}
			index = Mathf.Clamp(index, 0, targets.Count - 1);
			ContainerTarget target = targets[index];
			// The overview would otherwise sit on top and swallow clicks meant for the editor.
			CloseWindow(OverviewId);
			string id = WindowId(target);
			CloseWindow(id);
			Track(id);

			RuleEntry rule = Rules.Get(target.Key);
			const int perRow = 5;
			int slotRows = targets.Count > 1 ? (targets.Count + perRow - 1) / perRow : 0;
			float height = 430f + Mathf.Max(0, slotRows - 1) * 28f;
			CustomUIBuilder builder = CustomUIManager.Window(id, Core.Loc("asp.panel.title"));
			builder.SetSize(600f, height).SetDraggable(true).SetCloseOnEscape(true);
			builder.BeginColumn(6f);
			builder.AddLabel(target.Title, "title");
			builder.AddLabel(Core.Loc("asp.panel.key", target.Key), "key");

			if (targets.Count > 1)
			{
				builder.AddLabel(Core.Loc("asp.panel.slots"), "slotsLabel");
				for (int start = 0; start < targets.Count; start += perRow)
				{
					builder.BeginRow(4f);
					int end = Mathf.Min(start + perRow, targets.Count);
					for (int i = start; i < end; i++)
					{
						int slotIndex = i;
						ContainerTarget slot = targets[i];
						string label = string.IsNullOrEmpty(slot.SlotLabel) ? (slot.Slot ?? "-") : slot.SlotLabel;
						builder.AddButton(label, () => OpenPanel(item, targets, slotIndex), "slot" + i);
					}
					builder.End();
				}
			}

			builder.AddToggle(Core.Loc("asp.panel.enabled"), rule.Enabled, null, "enabled");
			builder.AddLabel(Core.Loc("asp.panel.include"), "includeLabel");
			string include = DraftInclude.TryGetValue(target.Key ?? "", out string draftInclude) ? draftInclude : rule.Include;
			string exclude = DraftExclude.TryGetValue(target.Key ?? "", out string draftExclude) ? draftExclude : rule.Exclude;
			builder.AddInput(Core.Loc("asp.panel.includeHint"), include, null, "include", text => DraftInclude[target.Key ?? ""] = text);
			builder.AddLabel(Core.Loc("asp.panel.exclude"), "excludeLabel");
			builder.AddInput(Core.Loc("asp.panel.excludeHint"), exclude, null, "exclude", text => DraftExclude[target.Key ?? ""] = text);
			builder.AddLabel(Core.Loc("asp.panel.help"), "help");
			builder.BeginRow(6f);
			builder.AddToggle(Core.Loc("asp.panel.allowStolen"), rule.AllowStolen, null, "allowStolen");
			builder.AddToggle(Core.Loc("asp.panel.stolenPriority"), rule.StolenPriority, null, "stolenPriority");
			builder.AddToggle(Core.Loc("asp.panel.allowContraband"), rule.AllowContraband, null, "allowContraband");
			builder.End();
			builder.BeginRow(6f);
			builder.AddLabel(Core.Loc("asp.panel.priority"), "priorityLabel");
			builder.AddInput("0", rule.Priority.ToString(), null, "priority");
			builder.End();
			builder.BeginRow(6f);
			builder.AddButton(Core.Loc("asp.panel.test"), () => TestMatch(id, target), "test");
			builder.AddButton(Core.Loc("asp.panel.scan"), () => Scan(target), "scan");
			builder.AddLabel("", "count");
			builder.End();
			builder.BeginRow(6f);
			builder.AddButton(Core.Loc("asp.panel.save"), () => Save(id, target), "save");
			builder.AddButton(Core.Loc("asp.panel.clear"), () => Clear(id, target), "clear");
			builder.AddButton(Core.Loc("asp.panel.close"), () => CloseWindow(id), "close");
			builder.End();
			builder.AddLabel(Core.Loc("asp.panel.saveNotice"), "saveNotice");
			builder.End();

			GameItem itemRef = item;
		}

		private static void Save(string id, ContainerTarget target)
		{
			RuleEntry entry = ReadDraft(id, target);
			if (entry.Error != null)
			{
				SetText(id, "count", entry.Error);
				return;
			}
			Rules.Save(target.Key, entry);
			Core.LogMsg(Core.Loc(entry.Enabled ? "asp.log.saved" : "asp.log.savedDisabled", target.Title));
			SetText(id, "count", Core.Loc(entry.Enabled ? "asp.panel.saved" : "asp.panel.savedDisabled"));
		}

		private static void Clear(string id, ContainerTarget target)
		{
			Rules.Remove(target.Key);
			DraftInclude[target.Key ?? ""] = "";
			DraftExclude[target.Key ?? ""] = "";
			OpenPanel(target.Item, TargetList(target.Item), IndexOf(TargetList(target.Item), target));
			SetText(WindowId(target), "count", Core.Loc("asp.panel.cleared"));
		}

		private static void TestMatch(string id, ContainerTarget target)
		{
			RuleEntry entry = ReadDraft(id, target);
			if (entry.Error != null)
			{
				SetText(id, "count", entry.Error);
				return;
			}
			List<GameItem> sources = World.Sources();
			int count = Engine.CountMatches(entry, sources, out Engine.MatchReport report);
			Core.LogMsg("Test rule: include='" + entry.Include + "' exclude='" + entry.Exclude + "' sources=" + sources.Count + " " + report + " error=" + (entry.Error ?? "none"));
			string text = Core.Loc("asp.panel.matches", count);
			if (report.Stolen > 0)
			{
				text += "  ·  " + Core.Loc("asp.panel.stolenBlocked", report.Stolen);
			}
			if (report.Contraband > 0)
			{
				text += "  ·  " + Core.Loc("asp.panel.contrabandBlocked", report.Contraband);
			}
			SetText(id, "count", text);
		}

		private static string ScanId(ContainerTarget target)
		{
			return "AutoSorterPlus.scan." + (target.Key ?? "?").Replace('#', '_').Replace('@', '_').Replace('.', '_');
		}

		// Lists the tags, source mods and item ids currently inside the container so
		// they can be added to the include/exclude expression with one click.
		private static void Scan(ContainerTarget target)
		{
			string id = ScanId(target);
			CloseWindow(id);
			Track(id);
			List<GameItem> items = new List<GameItem>();
			try
			{
				if (target.Inv != null && target.Inv.childItems != null)
				{
					items.AddRange(target.Inv.childItems);
				}
			}
			catch
			{
			}
			Dictionary<string, int> tags = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
			Dictionary<string, int> mods = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
			Dictionary<string, int> ids = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
			Dictionary<string, int> looseTags = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
			Dictionary<string, int> looseMods = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
			Dictionary<string, int> looseIds = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
			Dictionary<string, int> looseLabels = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
			Dictionary<string, int> labels = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
			CountItems(items, tags, mods, ids, labels);
			CountItems(World.Sources(), looseTags, looseMods, looseIds, looseLabels);

			CustomUIBuilder builder = CustomUIManager.Window(id, Core.Loc("asp.scan.title", target.Title));
			builder.SetSize(760f, 580f).SetDraggable(true).SetCloseOnEscape(true);
			builder.BeginColumn(6f);
			builder.AddLabel(Core.Loc("asp.scan.hint"), "hint");
			if (items.Count == 0)
			{
				builder.AddLabel(Core.Loc("asp.scan.empty"), "empty");
			}
			builder.BeginScroll(430f);
			builder.AddLabel(Core.Loc("asp.scan.loose"), "looseTitle");
			AddScanRows(builder, target, Core.Loc("asp.scan.mods"), looseMods, "mod:");
			AddScanRows(builder, target, Core.Loc("asp.scan.labels"), looseLabels, "label:");
			AddScanRows(builder, target, Core.Loc("asp.scan.tags"), looseTags, "tag:");
			AddScanRows(builder, target, Core.Loc("asp.scan.ids"), looseIds, "id:");
			builder.AddLabel(Core.Loc("asp.scan.container"), "containerTitle");
			AddScanRows(builder, target, Core.Loc("asp.scan.mods"), mods, "mod:");
			AddScanRows(builder, target, Core.Loc("asp.scan.labels"), labels, "label:");
			AddScanRows(builder, target, Core.Loc("asp.scan.tags"), tags, "tag:");
			AddScanRows(builder, target, Core.Loc("asp.scan.ids"), ids, "id:");
			builder.End();
			builder.AddButton(Core.Loc("asp.scan.close"), () => CloseWindow(id), "close");
			builder.End();
		}

		private static void Bump(Dictionary<string, int> counts, string key)
		{
			counts.TryGetValue(key, out int value);
			counts[key] = value + 1;
		}

		private static void CountItems(List<GameItem> items, Dictionary<string, int> tags, Dictionary<string, int> mods, Dictionary<string, int> ids, Dictionary<string, int> labels)
		{
			for (int i = 0; i < items.Count; i++)
			{
				GameItem item = items[i];
				if (item == null)
				{
					continue;
				}
				ItemFacts facts = ItemFacts.From(item);
				foreach (string type in facts.Types)
				{
					if (!string.IsNullOrEmpty(type) && type.Length <= 64 && !IsNoiseTag(type))
					{
						Bump(tags, type);
					}
				}
				if (facts.Labels != null)
				{
					foreach (string label in facts.Labels)
					{
						if (!string.IsNullOrEmpty(label) && label.Length <= 64 && !IsNoiseTag(label))
						{
							Bump(labels, label);
						}
					}
				}
				if (!string.IsNullOrEmpty(facts.ModId))
				{
					Bump(mods, facts.ModId);
				}
				if (!string.IsNullOrEmpty(facts.Id))
				{
					Bump(ids, facts.Id);
				}
			}
		}

		// Bookkeeping tags that are never useful as sort criteria.
		private static bool IsNoiseTag(string tag)
		{
			if (tag.StartsWith("CURRENT_", StringComparison.OrdinalIgnoreCase)
				|| tag.StartsWith("LIQUID_CONTAINER", StringComparison.OrdinalIgnoreCase)
				|| tag.StartsWith("BONUS_PERCENTAGE", StringComparison.OrdinalIgnoreCase)
				|| tag.StartsWith("GRADUATED_CONTAINER", StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
			switch (tag.ToUpperInvariant())
			{
			case "IS_OWNED_TAG":
			case "IMPORTANT_TAG":
			case "MOD_ID_TAG":
			case "MOD_NAME_TAG":
			case "CONTAINER_TEMP_TAG":
			case "CASSETTE_CONTENT":
			case "CONTAINER_TOOLTIP_TAG":
			case "MACHINE_TEMP_POWER_TAG":
				return true;
			default:
				return false;
			}
		}

		private static void AddScanRows(CustomUIBuilder builder, ContainerTarget target, string header, Dictionary<string, int> counts, string prefix)
		{
			if (counts.Count == 0)
			{
				return;
			}
			builder.AddLabel(header, null);
			List<KeyValuePair<string, int>> rows = new List<KeyValuePair<string, int>>(counts);
			rows.Sort((a, b) =>
			{
				int byCount = b.Value.CompareTo(a.Value);
				return byCount != 0 ? byCount : string.Compare(a.Key, b.Key, StringComparison.OrdinalIgnoreCase);
			});
			int shown = 0;
			for (int i = 0; i < rows.Count && shown < 60; i++, shown++)
			{
				string term = prefix + rows[i].Key;
				int count = rows[i].Value;
				string display = term + "  ×" + count;
				if (display.Length > 44)
				{
					display = display.Substring(0, 43) + "~";
				}
				builder.BeginRow(4f);
				builder.AddButton(Core.Loc("asp.scan.include"), () => Append(target, "include", term), null);
				builder.AddButton(Core.Loc("asp.scan.exclude"), () => Append(target, "exclude", term), null);
				builder.AddLabel(display, null);
				builder.End();
			}
		}

		private static void Append(ContainerTarget target, string field, string term)
		{
			string key = target.Key ?? "";
			Dictionary<string, string> drafts = field == "include" ? DraftInclude : DraftExclude;
			drafts.TryGetValue(key, out string current);
			drafts[key] = string.IsNullOrWhiteSpace(current) ? term : current.TrimEnd() + " OR " + term;
			Core.LogMsg("Scan append " + field + " '" + term + "' -> '" + drafts[key] + "' (" + key + ")");
			List<ContainerTarget> targets = TargetList(target.Item);
			OpenPanel(target.Item, targets, IndexOf(targets, target));
		}

		private static RuleEntry ReadDraft(string id, ContainerTarget target)
		{
			RuleEntry entry = Rules.Get(target.Key);
			string key = target.Key ?? "";
			entry.Enabled = GetBool(id, "enabled", entry.Enabled);
			entry.Include = DraftInclude.TryGetValue(key, out string include) ? include : GetString(id, "include", entry.Include);
			entry.Exclude = DraftExclude.TryGetValue(key, out string exclude) ? exclude : GetString(id, "exclude", entry.Exclude);
			entry.AllowStolen = GetBool(id, "allowStolen", entry.AllowStolen);
			entry.StolenPriority = GetBool(id, "stolenPriority", entry.StolenPriority);
			entry.AllowContraband = GetBool(id, "allowContraband", entry.AllowContraband);
			string priority = GetString(id, "priority", entry.Priority.ToString());
			entry.Priority = int.TryParse(priority, out int value) ? value : entry.Priority;
			entry.Compile();
			return entry;
		}

		private static void CloseWindow(string id)
		{
			try
			{
				CustomUIManager.Instance.CloseWindow(id);
			}
			catch
			{
			}
			Windows.Remove(id);
		}

		private static string GetString(string window, string element, string fallback)
		{
			try
			{
				CustomUIElement found = CustomUIManager.Instance.GetElement(window, element);
				if (found != null)
				{
					string value = found.GetString();
					return value ?? "";
				}
			}
			catch
			{
			}
			return fallback;
		}

		private static bool GetBool(string window, string element, bool fallback)
		{
			try
			{
				CustomUIElement found = CustomUIManager.Instance.GetElement(window, element);
				if (found != null)
				{
					return found.GetBool();
				}
			}
			catch
			{
			}
			return fallback;
		}

		private static void SetText(string window, string element, string text)
		{
			try
			{
				CustomUIManager.Instance.GetElement(window, element)?.SetText(text);
			}
			catch
			{
			}
		}

		internal static void ToggleOverview()
		{
			try
			{
				if (CustomUIManager.Instance.GetWindow(OverviewId) != null)
				{
					CloseWindow(OverviewId);
					return;
				}
			}
			catch
			{
			}
			ShowOverview();
		}

		private static void ShowOverview()
		{
			CloseWindow(OverviewId);
			Track(OverviewId);
			CustomUIBuilder builder = CustomUIManager.Window(OverviewId, Core.Loc("asp.overview.title"));
			builder.SetSize(620f, 520f).SetDraggable(true).SetCloseOnEscape(true);
			builder.BeginColumn(6f);
			builder.AddLabel(Core.Loc("asp.overview.hint"), "hint");
			builder.BeginRow(6f);
			builder.AddButton(Core.Loc("asp.overview.sort"), () => Engine.Sort(), "sort");
			builder.AddButton(Core.Loc("asp.overview.close"), () => CloseWindow(OverviewId), "close");
			builder.End();
			builder.AddLabel(Rules.GlobalEnabled ? Core.Loc("asp.overview.globalOn") : Core.Loc("asp.overview.globalOff"), "global");
			builder.BeginScroll(380f);

			List<ContainerTarget> targets = World.Targets(true);
			int shown = 0;
			for (int i = 0; i < targets.Count; i++)
			{
				ContainerTarget target = targets[i];
				RuleEntry rule = Rules.Get(target.Key);
				shown++;
				string summary = rule.Enabled ? BuildSummary(rule) : Core.Loc("asp.overview.off");
				builder.AddLabel(target.Title + "  —  " + summary, "row" + i);
				ContainerTarget captured = target;
				builder.AddButton(Core.Loc("asp.overview.edit"), () => OpenPanel(captured.Item, TargetList(captured.Item), IndexOf(TargetList(captured.Item), captured)), "edit" + i);
			}
			if (shown == 0)
			{
				builder.AddLabel(Core.Loc("asp.overview.empty"), "empty");
			}
			builder.End();
			builder.End();
		}

		private static List<ContainerTarget> TargetList(GameItem item)
		{
			List<ContainerTarget> result = new List<ContainerTarget>();
			List<ContainerTarget> all = World.Targets(false);
			for (int i = 0; i < all.Count; i++)
			{
				if (ReferenceEquals(all[i].Item, item))
				{
					result.Add(all[i]);
				}
			}
			return result;
		}

		private static int IndexOf(List<ContainerTarget> targets, ContainerTarget target)
		{
			for (int i = 0; i < targets.Count; i++)
			{
				if (ReferenceEquals(targets[i], target))
				{
					return i;
				}
			}
			return 0;
		}

		private static string BuildSummary(RuleEntry rule)
		{
			if (!rule.Enabled)
			{
				return Core.Loc("asp.overview.off");
			}
			string include = string.IsNullOrWhiteSpace(rule.Include) ? "*" : rule.Include;
			string exclude = string.IsNullOrWhiteSpace(rule.Exclude) ? "" : "  NOT (" + rule.Exclude + ")";
			return Core.Loc("asp.overview.priority", rule.Priority) + "  " + include + exclude;
		}
	}
}
