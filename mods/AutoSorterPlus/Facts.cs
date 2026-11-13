using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;

namespace AutoSorterPlus
{
	// Everything a rule can test, resolved once per item per sort pass.
	internal sealed class ItemFacts
	{
		internal string Id;
		internal string Name;
		internal string ModId;
		internal string ModName;
		internal HashSet<string> Types;
		internal HashSet<string> Labels;
		internal bool Stolen;
		internal bool Contraband;
		internal bool Owned;

		internal static ItemFacts From(GameItem item)
		{
			ItemFacts facts = new ItemFacts
			{
				Id = item.identifier ?? "",
				Name = item.name ?? "",
				Types = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
				Labels = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
			};
			try
			{
				List<ItemFeature> features = item.itemFeatures;
				if (features != null && features.Count > 0 && features.Count < 64)
				{
					foreach (ItemFeature feature in features)
					{
						if (feature == null)
						{
							continue;
						}
						if (!string.IsNullOrEmpty(feature.category))
						{
							facts.Labels.Add(feature.category);
						}
						ItemCondition condition = feature.GetCurrentItemCondition();
						if (condition == null)
						{
							continue;
						}
						if (!string.IsNullOrEmpty(condition.identifier))
						{
							facts.Labels.Add(condition.identifier);
						}
						if (!string.IsNullOrEmpty(condition.category))
						{
							facts.Labels.Add(condition.category);
						}
						string display = condition.display;
						if (!string.IsNullOrEmpty(display))
						{
							facts.Labels.Add(System.Text.RegularExpressions.Regex.Replace(display, "<[^>]*>", "").Trim());
						}
					}
				}
			}
			catch
			{
			}
			try
			{
				TagSystem state = item.modifiedState ?? item.state;
				Dictionary<string, TagState> dict = state?.dict;
				if (dict != null && dict.Count > 0 && dict.Count < 4096)
				{
					foreach (KeyValuePair<string, TagState> pair in dict)
					{
						if (pair.Value != null && pair.Value.IsEnabled())
						{
							facts.Types.Add(pair.Key);
						}
					}
				}
			}
			catch
			{
			}
			try
			{
				List<string> itemTypes = item.GetGameItemType();
				if (itemTypes != null)
				{
					foreach (string type in itemTypes)
					{
						if (!string.IsNullOrEmpty(type))
						{
							facts.Types.Add(type);
						}
					}
				}
			}
			catch
			{
			}
			facts.ModId = ModResolver.Resolve(item);
			facts.ModName = ModResolver.DisplayName(facts.ModId);
			try
			{
				facts.Stolen = StolenHelper.IsStolenItem(item);
			}
			catch
			{
			}
			try
			{
				facts.Contraband = ContrabandHelper.IsContraband(item);
			}
			catch
			{
			}
			try
			{
				facts.Owned = GeneralHelper.IsItemEffectivelyOwned(item);
			}
			catch
			{
			}
			return facts;
		}
	}

	// Which mod an item comes from: the MOD_ID_TAG written by ModHelper.SetSource,
	// otherwise the assembly that registered the item's factory. Null means base game.
	internal static class ModResolver
	{
		private static readonly Dictionary<string, string> ByIdentifier = new Dictionary<string, string>(StringComparer.Ordinal);

		internal static string Resolve(GameItem item)
		{
			if (item == null)
			{
				return null;
			}
			try
			{
				if (item.IsTag("MOD_ID_TAG"))
				{
					string tagged = item.GetTagReadonly("MOD_ID_TAG").GetString();
					if (!string.IsNullOrEmpty(tagged))
					{
						return tagged;
					}
				}
			}
			catch
			{
			}
			string id = item.identifier;
			if (string.IsNullOrEmpty(id))
			{
				return null;
			}
			if (ByIdentifier.TryGetValue(id, out string cached))
			{
				return cached;
			}
			string resolved = null;
			try
			{
				Func<GameItem> factory = DirectoryMaster.GetFactory<GameItem>(id);
				if (factory != null)
				{
					LoadedMod mod = ModSourceHelper.FindModByDelegate(factory);
					if (mod != null && mod.Manifest != null)
					{
						resolved = mod.Manifest.ID;
					}
				}
			}
			catch
			{
			}
			// Only cache a positive result: a factory can be registered after the first
			// lookup, and a cached null would hide the mod for the rest of the session.
			if (resolved != null)
			{
				ByIdentifier[id] = resolved;
			}
			return resolved;
		}

		internal static string DisplayName(string modId)
		{
			if (string.IsNullOrEmpty(modId))
			{
				return null;
			}
			try
			{
				string name = ModSourceHelper.GetModDisplayName(modId);
				if (!string.IsNullOrEmpty(name))
				{
					return name;
				}
			}
			catch
			{
			}
			return modId;
		}
	}

	// Case-insensitive glob matching (* and ?), compiled and cached.
	internal static class Glob
	{
		private static readonly Dictionary<string, Regex> Cache = new Dictionary<string, Regex>(StringComparer.Ordinal);

		internal static bool IsMatch(string pattern, string value)
		{
			if (string.IsNullOrEmpty(pattern))
			{
				return string.IsNullOrEmpty(value);
			}
			if (value == null)
			{
				value = "";
			}
			Regex regex;
			lock (Cache)
			{
				if (!Cache.TryGetValue(pattern, out regex))
				{
					regex = new Regex("^" + ToRegex(pattern) + "$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
					Cache[pattern] = regex;
				}
			}
			return regex.IsMatch(value);
		}

		private static string ToRegex(string pattern)
		{
			System.Text.StringBuilder builder = new System.Text.StringBuilder();
			foreach (char c in pattern)
			{
				switch (c)
				{
				case '*':
					builder.Append(".*");
					break;
				case '?':
					builder.Append('.');
					break;
				default:
					builder.Append(Regex.Escape(c.ToString()));
					break;
				}
			}
			return builder.ToString();
		}
	}
}
