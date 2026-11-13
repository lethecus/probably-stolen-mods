using System;
using System.Collections.Generic;

namespace AutoSorterPlus
{
	internal sealed class SortResult
	{
		internal int Moved;
		internal int NoMatch;
		internal int Failed;
		internal int Skipped;
		internal int Targets;
		internal double Milliseconds;
	}

	internal sealed class TargetPlan
	{
		internal ContainerTarget Target;
		internal RuleEntry Rule;
	}

	internal static class Engine
	{
		internal static List<TargetPlan> BuildPlans()
		{
			List<ContainerTarget> targets = World.Targets(true);
			Core.LogMsg(Core.Loc("asp.log.scan", targets.Count));
			List<TargetPlan> plans = new List<TargetPlan>();
			for (int i = 0; i < targets.Count; i++)
			{
				ContainerTarget target = targets[i];
				if (target?.Item == null || target.Inv == null)
				{
					continue;
				}
				RuleEntry rule = Rules.Get(target.Key);
				if (!rule.Enabled)
				{
					continue;
				}
				rule.Compile();
				if (rule.Error != null)
				{
					Core.LogWarning(target.Title + ": " + rule.Error);
					continue;
				}
				plans.Add(new TargetPlan { Target = target, Rule = rule });
			}
			plans.Sort((a, b) => b.Rule.Priority.CompareTo(a.Rule.Priority));
			return plans;
		}

		internal static SortResult Sort()
		{
			SortResult result = new SortResult();
			long start = Environment.TickCount;
			try
			{
				Rules.Load();
				if (!Rules.GlobalEnabled)
				{
					Core.LogWarning(Core.Loc("asp.log.disabled"));
					return result;
				}
				List<TargetPlan> plans = BuildPlans();
				result.Targets = plans.Count;
				if (plans.Count == 0)
				{
					Core.LogWarning(Core.Loc("asp.log.noTargets"));
					return result;
				}
				List<GameItem> sources = World.Sources();
				for (int i = 0; i < sources.Count; i++)
				{
					GameItem item = sources[i];
					if (!IsSortable(item))
					{
						result.Skipped++;
						continue;
					}
					ItemFacts facts = ItemFacts.From(item);
					TargetPlan chosen = Pick(plans, facts, item);
					if (chosen == null)
					{
						result.NoMatch++;
						continue;
					}
					if (World.Move(item, chosen.Target.Inv))
					{
						result.Moved++;
					}
					else
					{
						result.Failed++;
					}
				}
				for (int i = 0; i < plans.Count; i++)
				{
					try
					{
						plans[i].Target.Inv.Validate();
					}
					catch
					{
					}
				}
				LogSourceMods(sources);
			}
			catch (Exception exception)
			{
				Core.LogError("Sort failed: " + exception);
			}
			result.Milliseconds = Environment.TickCount - start;
			Core.LogMsg(Core.Loc("asp.log.done", result.Moved, result.NoMatch, result.Failed, result.Skipped, result.Targets, result.Milliseconds.ToString("0")));
			return result;
		}

		private static void LogSourceMods(List<GameItem> sources)
		{
			try
			{
				Dictionary<string, int> counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
				for (int i = 0; i < sources.Count; i++)
				{
					if (!IsSortable(sources[i]))
					{
						continue;
					}
					string mod = ModResolver.Resolve(sources[i]) ?? "(base)";
					counts.TryGetValue(mod, out int value);
					counts[mod] = value + 1;
				}
				List<KeyValuePair<string, int>> rows = new List<KeyValuePair<string, int>>(counts);
				rows.Sort((a, b) => b.Value.CompareTo(a.Value));
				System.Text.StringBuilder builder = new System.Text.StringBuilder();
				for (int i = 0; i < rows.Count && i < 8; i++)
				{
					if (builder.Length > 0)
					{
						builder.Append(", ");
					}
					builder.Append(rows[i].Key).Append('×').Append(rows[i].Value);
				}
				if (rows.Count > 8)
				{
					builder.Append(", +").Append(rows.Count - 8);
				}
				Core.LogMsg("Source mods: " + builder);
			}
			catch (Exception exception)
			{
				Core.LogWarning("Could not summarise source mods: " + exception.Message);
			}
		}

		private static TargetPlan Pick(List<TargetPlan> plans, ItemFacts facts, GameItem item)
		{
			GameInventory origin = null;
			try
			{
				origin = item.parentInventory;
			}
			catch
			{
			}
			// Stolen items go to "stolen priority" targets first.
			if (facts.Stolen)
			{
				for (int i = 0; i < plans.Count; i++)
				{
					TargetPlan plan = plans[i];
					if (plan.Rule.StolenPriority && plan.Rule.Matches(facts) && !ReferenceEquals(plan.Target.Inv, origin) && World.CanAccept(plan.Target.Inv, item))
					{
						return plan;
					}
				}
			}
			for (int i = 0; i < plans.Count; i++)
			{
				TargetPlan plan = plans[i];
				if (!plan.Rule.Matches(facts) || ReferenceEquals(plan.Target.Inv, origin))
				{
					continue;
				}
				if (World.CanAccept(plan.Target.Inv, item))
				{
					return plan;
				}
			}
			return null;
		}

		internal static bool IsSortable(GameItem item)
		{
			if (item == null)
			{
				return false;
			}
			try
			{
				if (ItemPinHelper.IsPinned(item) || DebrisHelper.IsDebris(item))
				{
					return false;
				}
			}
			catch
			{
			}
			if (World.IsBag(item) || World.IsMachine(item) || World.IsStorageItem(item))
			{
				return false;
			}
			return World.Owned(item);
		}

		internal sealed class MatchReport
		{
			internal int Matched;
			internal int Stolen;
			internal int Contraband;
			internal int Pinned;
			internal int Debris;
			internal int Bag;
			internal int Machine;
			internal int Storage;
			internal int Unowned;

			public override string ToString()
			{
				return "matches=" + Matched + " stolen=" + Stolen + " contraband=" + Contraband
					+ " pinned=" + Pinned + " debris=" + Debris + " bag=" + Bag
					+ " machine=" + Machine + " storage=" + Storage + " unowned=" + Unowned;
			}
		}

		internal static int CountMatches(RuleEntry rule, List<GameItem> sources, out MatchReport report)
		{
			report = new MatchReport();
			if (rule == null || sources == null)
			{
				return 0;
			}
			rule.Compile();
			if (rule.Error != null)
			{
				return 0;
			}
			for (int i = 0; i < sources.Count; i++)
			{
				GameItem item = sources[i];
				if (item == null)
				{
					continue;
				}
				ItemFacts facts = ItemFacts.From(item);
				if (!rule.Evaluate(facts))
				{
					continue;
				}
				if (facts.Stolen && !rule.AllowStolen)
				{
					report.Stolen++;
					continue;
				}
				if (facts.Contraband && !rule.AllowContraband)
				{
					report.Contraband++;
					continue;
				}
				try
				{
					if (ItemPinHelper.IsPinned(item))
					{
						report.Pinned++;
						continue;
					}
					if (DebrisHelper.IsDebris(item))
					{
						report.Debris++;
						continue;
					}
				}
				catch
				{
				}
				if (World.IsBag(item))
				{
					report.Bag++;
					continue;
				}
				if (World.IsMachine(item))
				{
					report.Machine++;
					continue;
				}
				if (World.IsStorageItem(item))
				{
					report.Storage++;
					continue;
				}
				if (!World.Owned(item))
				{
					report.Unowned++;
					continue;
				}
				report.Matched++;
			}
			return report.Matched;
		}

		internal static int CountMatches(RuleEntry rule, List<GameItem> sources, out int stolenBlocked, out int contrabandBlocked)
		{
			int count = CountMatches(rule, sources, out MatchReport report);
			stolenBlocked = report.Stolen;
			contrabandBlocked = report.Contraband;
			return count;
		}

		internal static int CountMatches(RuleEntry rule, List<GameItem> sources)
		{
			return CountMatches(rule, sources, out MatchReport _);
		}
	}
}
