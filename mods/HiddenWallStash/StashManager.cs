using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class StashManager
{
	public const string ItemId = "hidden_wall_stash";

	/// <summary>
	/// Identifier the item actually carries. It deliberately does NOT contain "hidden": AutoStorage ignores any
	/// item whose id contains one of its block words ("hidden" among them), so a stash called hidden_wall_stash
	/// can never be listed no matter how it is tagged or attached. The factory stays registered under the old id
	/// as well, so a save that still contains the old identifier resolves.
	/// </summary>
	public const string ItemIdentifier = "wall_grate_stash";
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

	/// <summary>
	/// Invisible parent for the stash item. AutoStorage's container scan ignores any item that is not
	/// inside an inventory (its IsAttached check reads item.parentInventory.childItems), so a virtual item
	/// reconstructed from mod data is skipped no matter how well FindAllItem exposes it. This private grid
	/// gives the stash a parent without putting it anywhere the player can see, sell, inspect or save.
	/// </summary>
	private static GameGridInventory hostInventory;

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
						ApplyConfiguredSize();
						AttachHost(Stash);
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
		Stash = DirectoryMaster.Item(ItemIdentifier);
		if (!IsUsable(Stash))
		{
			Log.Error("Could not create the hidden wall stash item. Is the item factory registered?");
			Stash = null;
			return;
		}
		Stash.GetUniqueID();
		List<GameGridInventory> fresh = GridsOf(Stash);
		if (fresh.Count > 0 && fresh[0].inventoryShape != null)
		{
			StoreSize(fresh[0].inventoryShape.width, fresh[0].inventoryShape.height);
		}
		AttachHost(Stash);
		EncodeNow();
	}

	/// <summary>
	/// Gives the stash a parent inventory so container scans accept it, and marks it owned for the same
	/// reason. Called after every create/decode. The host is never rendered and is not reachable from any
	/// EmporiumEntry window, so it cannot be opened, inspected or written into the save by the game.
	/// </summary>
	private static void AttachHost(GameItem item)
	{
		try
		{
			if (item == null)
			{
				return;
			}
			if (!item.IsTag("IS_OWNED_TAG"))
			{
				item.EnableTag("IS_OWNED_TAG", false);
			}
			// A stash decoded from an older save still carries "hidden_wall_stash", which AutoStorage blocks by
			// name. Rename it here so the save re-encodes with the usable identifier.
			if (!string.Equals(item.identifier, ItemIdentifier, StringComparison.Ordinal))
			{
				item.identifier = ItemIdentifier;
			}
			// The factory sets this for new stashes, but an existing stash is decoded from mod data and keeps
			// the tags it was saved with - so a stash saved before this change never gets it. AutoStorage's
			// IsMachineItem is Tags.Has(it,"MACHINE"), and that is what routes the stash down its tabbed
			// machinery path, so set it here as well.
			// A decoded stash can come back with an empty display name, and AutoStorage labels its container rows
			// with GetDisplayName(), which then falls back to the identifier ("wall_grate_stash").

			if (string.IsNullOrEmpty(item.name))
			{
				item.name = "Hidden Wall Stash";
			}
			if (!item.IsTag("MACHINE"))
			{
				item.EnableTag("MACHINE", false);
			}
			if (hostInventory == null)
			{
				hostInventory = new GameGridInventory(25, 25);
				hostInventory.identifier = "hidden_wall_stash_host";
			}
			if (item.parentInventory == hostInventory || item.parentInventory != null)
			{
				return;
			}
			SlotMarker slot = hostInventory.TryInventorySlot(item, item.unitCount, (GridShape)null, (TagSystem)null);
			if (slot != null && slot.targetItem == null)
			{
				slot.AcceptUnchecked();
			}
			if (item.parentInventory == null)
			{
				// The slot path can refuse silently (shape validation), so fall back to the inventory's own
				// accept, which is what a normal pickup uses.
				hostInventory.UncheckedAccept(item);
			}
			GameInventory parent = item.parentInventory;
			string window = "none";
			try
			{
				if (item.contentWindow != null && item.contentWindow.childElement != null)
				{
					// AutoStorage's machinery path requires the window's child element to be a MachineryPanelRow
					// (MachineryRow(item) returns null otherwise), which is the last thing that can silently
					// break after the tags are right.
					window = item.contentWindow.childElement.GetType().Name;
				}
			}
			catch (Exception)
			{
			}
			Log.Log("Stash host: parent=" + ((parent == null) ? "none" : (parent.identifier ?? "?"))
				+ ", children=" + ((parent == null) ? -1 : parent.childItems.Count)
				+ ", shape=" + ((item.modifiedShape == null) ? "null" : (item.modifiedShape.width + "x" + item.modifiedShape.height))
				+ ", machineTag=" + item.IsTag("MACHINE")
				+ ", ownedTag=" + item.IsTag("IS_OWNED_TAG")
				+ ", windowRow=" + window);
			if (parent == null)
			{
				Log.Warning("Stash host inventory did not take the item; AutoStorage will not list the stash.");
			}
		}
		catch (Exception ex)
		{
			Log.Warning("Could not attach the stash host inventory: " + ex.Message);
		}
	}

	/// <summary>Size the stash was last built at, so a decode matches the save instead of the setting.</summary>
	internal static bool TryReadStoredSize(out int width, out int height)
	{
		width = 0;
		height = 0;
		try
		{
			string stored = ModHelper.GetModData(ModId, StashItemFactory.SizeKey, null);
			if (string.IsNullOrEmpty(stored))
			{
				return false;
			}
			string[] parts = stored.Split('x');
			if (parts.Length != 2 ||
			    !int.TryParse(parts[0], out width) || !int.TryParse(parts[1], out height))
			{
				return false;
			}
			if (width < HiddenWallStash.HiddenWallStashMod.MinSide || width > HiddenWallStash.HiddenWallStashMod.MaxSide ||
			    height < HiddenWallStash.HiddenWallStashMod.MinSide || height > HiddenWallStash.HiddenWallStashMod.MaxSide)
			{
				return false;
			}
			return true;
		}
		catch
		{
			return false;
		}
	}

	internal static void StoreSize(int width, int height)
	{
		try
		{
			ModHelper.SetModData(ModId, StashItemFactory.SizeKey, width + "x" + height);
		}
		catch (Exception exception)
		{
			Log.Warning("Could not store the stash size: " + exception.Message);
		}
	}

	internal static bool IsDecoding
	{
		get { return decoding; }
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
		// Safety net: if the size changed while the game was running, apply it before the window is shown.
		ApplyConfiguredSize();
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
		return GridsOf(Stash);
	}

	/// <summary>Tab grids of any stash item, fresh or decoded.</summary>
	private static List<GameGridInventory> GridsOf(GameItem item)
	{
		List<GameGridInventory> grids = new List<GameGridInventory>();
		PixelWindow window = item != null ? item.contentWindow : null;
		if (window == null)
		{
			return grids;
		}

		// Model path first: the panel/tab objects exist as soon as the item is decoded, while their render
		// nodes only appear once the window has been shown. Walking the render tree here is why resizing
		// silently did nothing after a load.
		if (window.child is MachineryPanelRow row && row.Panels != null)
		{
			for (int p = 0; p < row.Panels.Count; p++)
			{
				MachineryPanel panel = row.Panels[p];
				if (panel == null || !(panel.content is MachineryTabs tabs))
				{
					continue;
				}
				for (int i = 0; i < tabs.count; i++)
				{
					MachineryIntake intake = tabs.GetPage(i) as MachineryIntake;
					if (intake != null && intake.content is GameGridInventory grid)
					{
						grids.Add(grid);
					}
				}
			}
		}
		if (grids.Count > 0)
		{
			return grids;
		}

		// Fallback for a window that is already on screen.
		if (window.handler == null)
		{
			return grids;
		}
		TreeNodeRender[] nodes = window.handler.GetComponentsInChildren<TreeNodeRender>(true);
		if (nodes == null)
		{
			return grids;
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
					grids.Add(grid);
				}
			}
		}
		return grids;
	}

	/// <summary>
	/// Applies the configured stash size to an existing stash. The stash is not placed anywhere in the
	/// world - it lives in mod data only - so the clean way is to build a replacement at the new size and
	/// move the contents across, rather than resizing grids inside a window laid out for the old size.
	/// Growing is always safe; shrinking only happens when every tab is empty, because items would otherwise
	/// have nowhere to go.
	/// </summary>
	public static void ApplyConfiguredSize()
	{
		try
		{
			if (!IsUsable(Stash))
			{
				return;
			}

			int wantWidth = HiddenWallStash.HiddenWallStashMod.StashWidth;
			int wantHeight = HiddenWallStash.HiddenWallStashMod.StashHeight;

			List<GameGridInventory> grids = GridsOf(Stash);
			if (grids.Count == 0)
			{
				Log.Warning("Could not find the stash tab grids; size change skipped this load.");
				return;
			}
			GridShape shape = grids[0].inventoryShape;
			if (shape == null)
			{
				return;
			}
			int currentWidth = shape.width;
			int currentHeight = shape.height;
			if (currentWidth == wantWidth && currentHeight == wantHeight)
			{
				StoreSize(currentWidth, currentHeight);
				return;
			}

			GameItem replacement = DirectoryMaster.Item(ItemIdentifier);
			if (!IsUsable(replacement))
			{
				Log.Warning("Could not build a resized hidden wall stash.");
				return;
			}
			replacement.GetUniqueID();
			List<GameGridInventory> targets = GridsOf(replacement);
			if (targets.Count == 0)
			{
				Log.Warning("The rebuilt hidden wall stash had no tab grids.");
				return;
			}

			List<KeyValuePair<GameGridInventory, GameItem>> moved = new List<KeyValuePair<GameGridInventory, GameItem>>();
			int keptInTab = 0;
			int spilled = 0;
			int expected = 0;
			for (int e = 0; e < grids.Count; e++)
			{
				expected += grids[e].childItems.Count;
			}
			Dictionary<string, int> spillCounts = new Dictionary<string, int>();
			for (int i = 0; i < grids.Count; i++)
			{
				foreach (GameItem child in grids[i].childItems.ToList())
				{
					bool placed = false;
					// Prefer the item's own tab so a resize keeps the organisation, and only spill into the
					// other tabs when it genuinely does not fit any more.
					if (i < targets.Count && GraphUtils.TryAcceptAll(targets[i], child) == 0)
					{
						placed = true;
						keptInTab++;
						moved.Add(new KeyValuePair<GameGridInventory, GameItem>(grids[i], child));
					}
					if (!placed)
					{
						for (int t = 0; t < targets.Count; t++)
						{
							if (t == i)
							{
								continue;
							}
							// TryAcceptAll returns the amount that did NOT fit, so 0 means it went in.
							if (GraphUtils.TryAcceptAll(targets[t], child) == 0)
							{
								placed = true;
								spilled++;
								string pair = (i + 1) + "->" + (t + 1);
								spillCounts[pair] = spillCounts.TryGetValue(pair, out int seenCount) ? seenCount + 1 : 1;
								moved.Add(new KeyValuePair<GameGridInventory, GameItem>(grids[i], child));
								break;
							}
						}
					}
					if (!placed)
					{
						// Nothing is lost: put back everything already moved and keep the old stash.
						for (int m = moved.Count - 1; m >= 0; m--)
						{
							try
							{
								GraphUtils.TryAcceptAll(moved[m].Key, moved[m].Value);
							}
							catch
							{
							}
						}
						Log.Warning("Could not fit " + child.identifier + " into " + wantWidth + "x" + wantHeight +
							"; kept " + currentWidth + "x" + currentHeight + " and restored the items.");
						return;
					}
				}
			}

			Stash = replacement;
			StoreSize(wantWidth, wantHeight);
			EncodeNow();
			string spillSummary = string.Empty;
			foreach (KeyValuePair<string, int> pair in spillCounts)
			{
				spillSummary += " " + pair.Key + "x" + pair.Value;
			}
			int movedTotal = keptInTab + spilled;
			Log.Log("Resized the hidden wall stash to " + wantWidth + "x" + wantHeight + " (was " +
				currentWidth + "x" + currentHeight + "): " + keptInTab + " stayed in their own tab, " + spilled +
				" moved, " + movedTotal + " of " + expected + " item(s) placed" +
				(spilled > 0 ? "; spill" + spillSummary : "") + ".");
			if (movedTotal != expected)
			{
				Log.Error("Stash resize placed " + movedTotal + " item(s) but " + expected + " were present.");
			}
		}
		catch (Exception exception)
		{
			Log.Error("Failed to resize the hidden wall stash: " + exception);
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
			if (Stash.contentWindow != null)
			{
				Stash.contentWindow.Validate();
			}
			foreach (GameGridInventory grid in EnumerateStashGrids())
			{
				if (grid == null || !GraphUtils.CanAccept(grid, item))
				{
					continue;
				}
				grid.Validate();
				// TryAcceptAll returns how many units were NOT accepted, so 0 means success.
				int remaining = GraphUtils.TryAcceptAll(grid, item);
				if (remaining == 0)
				{
					MarkDirty();
					Log.Log("Dropped " + item.identifier + " into the hidden wall stash.");
					return true;
				}
			}
			Log.Log("No room in the hidden wall stash for " + item.identifier + ".");
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
