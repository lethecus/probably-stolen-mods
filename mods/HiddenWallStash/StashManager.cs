using System;
using System.Collections.Generic;
using UnityEngine;

public static class StashManager
{
	public const string ItemId = "hidden_wall_stash";
	public const string ModId = HiddenWallStash.HiddenWallStashMod.ModId;

	private const string StashKey = "stash";
	private const string FeaturesKey = "stash_features";
	private const float FlushDelay = 0.35f;
	private const float SignatureInterval = 1f;

	public static GameItem Stash { get; private set; }

	private static bool dirty;
	private static float dirtyAt;
	private static int lastSignature;
	private static float nextSignatureCheck;
	private static bool decoding;
	private static GameObject updaterObject;

	private static ModLog Log => HiddenWallStash.HiddenWallStashMod.Log;

	public static bool IsWindowVisible()
	{
		PixelWindow window = Stash?.contentWindow;
		return window != null && window.handler != null && window.IsVisible();
	}

	private static bool IsUsable(GameItem item)
	{
		return item != null && item.contentWindow != null && item.contentWindow.handler != null;
	}

	public static void ResetForLoad()
	{
		if (IsUsable(Stash) && IsWindowVisible())
		{
			try
			{
				Stash.CloseContentWindow();
			}
			catch (Exception)
			{
			}
		}
		Stash = null;
		dirty = false;
		lastSignature = 0;
		nextSignatureCheck = 0f;
	}

	public static void StartNewGame()
	{
		ResetForLoad();
		ModHelper.RemoveModData(ModId, StashKey);
		ModHelper.RemoveModData(ModId, FeaturesKey);
		CreateFresh();
	}

	public static void EnsureStash()
	{
		if (IsUsable(Stash))
		{
			return;
		}
		Stash = null;
		if (PlayerStore.Instance == null)
		{
			return;
		}

		string json = ModHelper.GetModData(ModId, StashKey, null);
		if (!string.IsNullOrEmpty(json))
		{
			try
			{
				SaveState state = JsonUtility.FromJson<SaveState>(json);
				if (state?.saveItems != null && state.saveItems.Count > 0)
				{
					decoding = true;
					GameItem decoded;
					try
					{
						decoded = SaveManager.DecodeNodes(state.saveItems);
					}
					finally
					{
						decoding = false;
					}
					if (IsUsable(decoded))
					{
						Stash = decoded;
						Stash.GetUniqueID();
						RestoreFeatures();
						lastSignature = ComputeSignature();
						Log.Log("Decoded hidden wall stash with " + state.saveItems.Count + " saved nodes.");
						return;
					}
					Log.Warning("Decoded hidden wall stash was not usable; creating a fresh one.");
				}
			}
			catch (Exception exception)
			{
				Log.Error("Failed to decode hidden wall stash: " + exception);
			}
		}

		CreateFresh();
	}

	private static void CreateFresh()
	{
		Stash = DirectoryMaster.Item(ItemId);
		if (!IsUsable(Stash))
		{
			Log.Error("Could not create the hidden wall stash item. Is the item factory registered?");
			Stash = null;
			return;
		}
		Stash.GetUniqueID();
		EncodeNow();
	}

	public static void MarkDirty()
	{
		if (decoding)
		{
			return;
		}
		dirty = true;
		dirtyAt = Time.unscaledTime;
	}

	public static void OnStashInventoryChanged(GameItem item, GameInventory inventory, SlotMarker slot)
	{
		MarkDirty();
	}

	public static void Tick()
	{
		if (Stash == null)
		{
			return;
		}
		if (dirty && Time.unscaledTime - dirtyAt >= FlushDelay)
		{
			EncodeNow();
			return;
		}
		if (!IsWindowVisible())
		{
			nextSignatureCheck = 0f;
			return;
		}
		if (Time.unscaledTime < nextSignatureCheck)
		{
			return;
		}
		nextSignatureCheck = Time.unscaledTime + SignatureInterval;
		if (ComputeSignature() != lastSignature)
		{
			EncodeNow();
		}
	}

	public static void FlushNow()
	{
		if (Stash != null && IsUsable(Stash))
		{
			EncodeNow();
		}
	}

	public static void HideWindow()
	{
		if (IsUsable(Stash) && IsWindowVisible())
		{
			try
			{
				Stash.CloseContentWindow();
			}
			catch (Exception)
			{
			}
		}
	}

	public static void ToggleWindow()
	{
		if (!IsUsable(Stash))
		{
			EnsureStash();
		}
		if (!IsUsable(Stash))
		{
			return;
		}
		if (IsWindowVisible())
		{
			Stash.CloseContentWindow();
			PlayClick();
			EncodeNow();
			return;
		}
		Stash.contentWindow.Show(Input.mousePosition);
		Stash.RestoreContentWindowPosition();
		PlayClick();
	}

	private static void PlayClick()
	{
		try
		{
			AudioManager.Instance?.TryPlayOne("interface_click");
		}
		catch (Exception)
		{
		}
	}

	private static void EncodeNow()
	{
		if (Stash == null || PlayerStore.Instance == null || !IsUsable(Stash))
		{
			return;
		}
		try
		{
			if (PlayerStore.Instance.savedItemFeatureList == null)
			{
				PlayerStore.Instance.savedItemFeatureList = new List<ItemFeature>();
			}
			List<ItemFeature> featureSink = PlayerStore.Instance.savedItemFeatureList;
			int before = featureSink.Count;
			List<SaveItemNode> nodes = null;
			List<ItemFeature> features = null;
			try
			{
				nodes = SaveManager.EncodeNodes(Stash);
			}
			finally
			{
				int added = featureSink.Count - before;
				if (added > 0)
				{
					features = featureSink.GetRange(before, added);
					featureSink.RemoveRange(before, added);
				}
			}

			SaveState state = new SaveState();
			state.saveItems = nodes;
			ModHelper.SetModData(ModId, StashKey, JsonUtility.ToJson(state));
			WriteFeatures(features);
			dirty = false;
			lastSignature = ComputeSignature();
		}
		catch (Exception exception)
		{
			dirty = false;
			Log.Error("Failed to encode hidden wall stash: " + exception);
		}
	}

	private static void WriteFeatures(List<ItemFeature> features)
	{
		if (features == null || features.Count == 0)
		{
			ModHelper.RemoveModData(ModId, FeaturesKey);
			return;
		}
		byte[] bytes = ES3.Serialize(features);
		ModHelper.SetModData(ModId, FeaturesKey, Convert.ToBase64String(bytes));
	}

	private static void RestoreFeatures()
	{
		string encoded = ModHelper.GetModData(ModId, FeaturesKey, null);
		if (string.IsNullOrEmpty(encoded) || Stash == null)
		{
			return;
		}
		try
		{
			byte[] bytes = Convert.FromBase64String(encoded);
			List<ItemFeature> features = ES3.Deserialize<List<ItemFeature>>(bytes);
			if (features == null)
			{
				return;
			}
			Dictionary<int, GameItem> byId = new Dictionary<int, GameItem>();
			byId[Stash.GetUniqueID()] = Stash;
			foreach (GameItem item in GraphUtils.FindAllChildrenType<GameItem>(Stash))
			{
				if (!byId.ContainsKey(item.uniqueId))
				{
					byId.Add(item.uniqueId, item);
				}
			}
			foreach (GameItem item in byId.Values)
			{
				item.RemoveAllFeature();
			}
			int restored = 0;
			foreach (ItemFeature feature in features)
			{
				if (feature == null)
				{
					continue;
				}
				if (byId.TryGetValue(feature.parentItemUniqueId, out GameItem owner))
				{
					owner.itemFeatures.Add(feature);
					restored++;
				}
			}
			Log.Log("Restored " + restored + " item features in the hidden wall stash.");
		}
		catch (Exception exception)
		{
			Log.Error("Failed to restore hidden wall stash features: " + exception);
		}
	}

	private static int ComputeSignature()
	{
		if (Stash == null)
		{
			return 0;
		}
		int hash = 17;
		unchecked
		{
			hash = hash * 31 + Stash.uniqueId;
			foreach (GameItem item in GraphUtils.FindAllChildrenType<GameItem>(Stash))
			{
				hash = hash * 31 + item.uniqueId;
				hash = hash * 31 + item.unitCount;
				hash = hash * 31 + item.itemFeatures.Count;
			}
		}
		return hash;
	}

	// The tab inventories live inside the stash's content window, so walk the window's
	// render tree to find them. Works for both freshly created and decoded stashes.
	private static IEnumerable<GameGridInventory> EnumerateStashGrids()
	{
		PixelWindow window = Stash != null ? Stash.contentWindow : null;
		if (window == null || window.handler == null)
		{
			yield break;
		}
		TreeNodeRender[] nodes = window.handler.GetComponentsInChildren<TreeNodeRender>(true);
		if (nodes == null)
		{
			yield break;
		}
		foreach (TreeNodeRender node in nodes)
		{
			if (node == null || !(node.element is MachineryTabs tabs))
			{
				continue;
			}
			for (int i = 0; i < tabs.count; i++)
			{
				MachineryIntake intake = tabs.GetPage(i) as MachineryIntake;
				if (intake != null && intake.content is GameGridInventory grid)
				{
					yield return grid;
				}
			}
		}
	}

	// Drops an item straight into the stash, used when the mouse is released over the grate.
	public static bool TryDropIntoStash(GameItem item)
	{
		if (item == null)
		{
			return false;
		}
		if (!IsUsable(Stash))
		{
			EnsureStash();
		}
		if (!IsUsable(Stash))
		{
			return false;
		}
		try
		{
			int found = 0;
			if (Stash.contentWindow != null)
			{
				Stash.contentWindow.Validate();
			}
			foreach (GameGridInventory grid in EnumerateStashGrids())
			{
				found++;
				grid.Validate();
				bool canAccept = GraphUtils.CanAccept(grid, item);
				Log.Log("Stash drop check: grid '" + (grid.identifier ?? "?") + "' canAccept=" + canAccept);
				if (!canAccept)
				{
					continue;
				}
				if (GraphUtils.TryAcceptAll(grid, item) > 0)
				{
					MarkDirty();
					Log.Log("Dropped " + item.identifier + " into the hidden wall stash.");
					return true;
				}
			}
			Log.Log("No room in the hidden wall stash for " + item.identifier + " (grids found: " + found + ", window=" + (Stash.contentWindow != null) + ").");
		}
		catch (Exception exception)
		{
			Log.Error("Failed to drop an item into the hidden wall stash: " + exception);
		}
		return false;
	}

	public static void EnsureUpdater()
	{
		if (updaterObject != null)
		{
			return;
		}
		updaterObject = new GameObject("HiddenWallStashUpdater");
		UnityEngine.Object.DontDestroyOnLoad(updaterObject);
		updaterObject.AddComponent<StashUpdater>();
	}
}
