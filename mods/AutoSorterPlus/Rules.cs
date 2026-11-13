using System;
using System.Collections.Generic;
using System.Text;

namespace AutoSorterPlus
{
	internal sealed class RuleEntry
	{
		internal bool Enabled;
		internal string Include = "";
		internal string Exclude = "";
		internal bool AllowStolen;
		internal bool StolenPriority;
		internal bool AllowContraband;
		internal int Priority = 0;

		private RuleNode _includeNode;
		private RuleNode _excludeNode;
		private string _includeSource;
		private string _excludeSource;
		private string _error;

		internal string Error => _error;

		internal void Compile()
		{
			_error = null;
			if (!ReferenceEquals(_includeSource, Include))
			{
				_includeSource = Include;
				if (!RuleExpr.TryParse(Include, out _includeNode, out string includeError))
				{
					_error = Loc("asp.error.parse", includeError);
				}
			}
			if (!ReferenceEquals(_excludeSource, Exclude))
			{
				_excludeSource = Exclude;
				if (!RuleExpr.TryParse(Exclude, out _excludeNode, out string excludeError))
				{
					_error = Loc("asp.error.parse", excludeError);
				}
			}
		}

		internal bool Matches(ItemFacts facts)
		{
			if (!Enabled || !Evaluate(facts))
			{
				return false;
			}
			if (facts.Stolen && !AllowStolen)
			{
				return false;
			}
			if (facts.Contraband && !AllowContraband)
			{
				return false;
			}
			return true;
		}

		// Include/exclude expression only; the Enabled switch and the stolen/contraband
		// policies are applied by Matches, so the editor can preview a disabled rule.
		internal bool Evaluate(ItemFacts facts)
		{
			if (_error != null)
			{
				return false;
			}
			if (_excludeNode != null && _excludeNode.Eval(facts))
			{
				return false;
			}
			if (_includeNode != null && !_includeNode.Eval(facts))
			{
				return false;
			}
			return true;
		}

		internal RuleEntry Clone()
		{
			return new RuleEntry
			{
				Enabled = Enabled,
				Include = Include,
				Exclude = Exclude,
				AllowStolen = AllowStolen,
				StolenPriority = StolenPriority,
				AllowContraband = AllowContraband,
				Priority = Priority
			};
		}

		private static string Loc(string key, params object[] args)
		{
			return Core.Loc(key, args);
		}
	}

	// Per-save rule storage carried in the mod data, so rules travel with the run.
	internal static class Rules
	{
		internal const string Prefix = "rule.";
		private const string GlobalKey = "global";

		private static readonly Dictionary<string, RuleEntry> Entries = new Dictionary<string, RuleEntry>(StringComparer.Ordinal);
		private static object _store;
		private static int _slot = int.MinValue;
		private static bool _globalEnabled = true;

		internal static bool GlobalEnabled
		{
			get => _globalEnabled;
			set
			{
				_globalEnabled = value;
				Persist(GlobalKey, value ? "1" : "0");
			}
		}

		internal static string Key(GameItem item, string slot)
		{
			if (item == null)
			{
				return null;
			}
			string key = (string.IsNullOrEmpty(item.identifier) ? "container" : item.identifier) + "#" + item.uniqueId;
			if (!string.IsNullOrEmpty(slot))
			{
				key += "@" + slot;
			}
			return key;
		}

		internal static void ResetForSave()
		{
			_store = null;
			_slot = int.MinValue;
			Entries.Clear();
			_globalEnabled = true;
			Load();
		}

		internal static void Load()
		{
			try
			{
				PlayerStore store = PlayerStore.HasInstance ? PlayerStore.Instance : null;
				if (store == null)
				{
					return;
				}
				if (ReferenceEquals(store, _store) && store.saveSlotId == _slot)
				{
					return;
				}
				_store = store;
				_slot = store.saveSlotId;
				Entries.Clear();
				_globalEnabled = true;
				string global = Core.GetModData(GlobalKey);
				if (global != null)
				{
					_globalEnabled = global != "0";
				}
				List<string> keys = Core.ModDataKeys();
				int loaded = 0;
				foreach (string key in keys)
				{
					if (!key.StartsWith(Prefix, StringComparison.Ordinal))
					{
						continue;
					}
					RuleEntry entry = Decode(Core.GetModData(key));
					if (entry != null)
					{
						Entries[key.Substring(Prefix.Length)] = entry;
						loaded++;
					}
				}
				Core.LogMsg("Loaded " + loaded + " rule(s) for save slot " + store.saveSlotId + ".");
			}
			catch (Exception exception)
			{
				Core.LogWarning("Could not load rules: " + exception.Message);
			}
		}

		internal static RuleEntry Get(string key)
		{
			if (string.IsNullOrEmpty(key))
			{
				return new RuleEntry();
			}
			Load();
			if (Entries.TryGetValue(key, out RuleEntry entry))
			{
				return entry.Clone();
			}
			return new RuleEntry();
		}

		internal static void Save(string key, RuleEntry entry)
		{
			if (string.IsNullOrEmpty(key) || entry == null)
			{
				return;
			}
			Load();
			Entries[key] = entry.Clone();
			Core.SetModData(Prefix + key, Encode(entry));
		}

		internal static void Remove(string key)
		{
			if (string.IsNullOrEmpty(key))
			{
				return;
			}
			Load();
			Entries.Remove(key);
			Core.SetModData(Prefix + key, null);
		}

		internal static List<KeyValuePair<string, RuleEntry>> All()
		{
			Load();
			return new List<KeyValuePair<string, RuleEntry>>(Entries);
		}

		internal static bool HasAny(GameItem item)
		{
			if (item == null)
			{
				return false;
			}
			string prefix = Key(item, null) + "@";
			Load();
			foreach (KeyValuePair<string, RuleEntry> pair in Entries)
			{
				if (pair.Key == Key(item, null) || pair.Key.StartsWith(prefix, StringComparison.Ordinal))
				{
					return true;
				}
			}
			return false;
		}

		private static string Encode(RuleEntry entry)
		{
			StringBuilder builder = new StringBuilder();
			builder.Append("1|").Append(entry.Enabled ? '1' : '0')
				.Append('|').Append(entry.AllowStolen ? '1' : '0')
				.Append('|').Append(entry.StolenPriority ? '1' : '0')
				.Append('|').Append(entry.AllowContraband ? '1' : '0')
				.Append('|').Append(entry.Priority)
				.Append('|').Append(Escape(entry.Include ?? ""))
				.Append('|').Append(Escape(entry.Exclude ?? ""));
			return builder.ToString();
		}

		private static RuleEntry Decode(string payload)
		{
			if (string.IsNullOrEmpty(payload))
			{
				return null;
			}
			try
			{
				string[] parts = payload.Split('|');
				if (parts.Length < 8 || parts[0] != "1")
				{
					return null;
				}
				return new RuleEntry
				{
					Enabled = parts[1] == "1",
					AllowStolen = parts[2] == "1",
					StolenPriority = parts[3] == "1",
					AllowContraband = parts[4] == "1",
					Priority = int.TryParse(parts[5], out int priority) ? priority : 0,
					Include = Unescape(parts[6]),
					Exclude = Unescape(parts[7])
				};
			}
			catch
			{
				return null;
			}
		}

		private static string Escape(string value)
		{
			return value.Replace("\\", "\\\\").Replace("|", "\\p").Replace("\n", "\\n").Replace("\r", "");
		}

		private static string Unescape(string value)
		{
			StringBuilder builder = new StringBuilder(value.Length);
			for (int i = 0; i < value.Length; i++)
			{
				char c = value[i];
				if (c == '\\' && i + 1 < value.Length)
				{
					char next = value[++i];
					builder.Append(next == 'p' ? '|' : (next == 'n' ? '\n' : next));
				}
				else
				{
					builder.Append(c);
				}
			}
			return builder.ToString();
		}

		private static void Persist(string key, string value)
		{
			try
			{
				Core.SetModData(key, value);
			}
			catch (Exception exception)
			{
				Core.LogWarning("Could not save rule data: " + exception.Message);
			}
		}
	}
}
