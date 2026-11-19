using UnityEngine;

namespace CassetteRandomize
{
	// Adds a small arrow on the left edge of the player window that collapses or
	// restores the storage panel, so the deck can take the full window.
	public class StorageDrawer
	{
		private const string ClosedTag = "CASSETTE_STORAGE_CLOSED_TAG";
		private const string Atlas = "Window/elements_inner";
		private const int HeaderHeight = 21;

		private readonly GameItem machine;
		private readonly PixelWindow window;
		private readonly MachineryPanelRow row;
		private readonly MachineryPanel panel;
		private readonly MachineryButton arrow;

		private bool IsOpen => !machine.IsTag(ClosedTag);

		public StorageDrawer(GameItem machine, MachineryPanelRow row, MachineryPanel panel)
		{
			this.machine = machine;
			this.row = row;
			this.panel = panel;
			window = machine.contentWindow;
			arrow = MachineryButton.FromAtlas(Atlas, "previous", Toggle);
			arrow.handler.SetParent(window.handler);
			arrow.handler.rectTransform.SetAsLastSibling();
			panel.SetHidden(!IsOpen);
			Refresh();
		}

		private void Toggle()
		{
			bool open = !IsOpen;
			machine.ModifyTag(ClosedTag, tag => tag.SetEnabled(!open), false);
			int previousWidth = window.widthPixels;
			panel.SetHidden(!open);
			Refresh();
			Vector2 position = window.rectTransform.anchoredPosition;
			position.x -= window.widthPixels - previousWidth;
			window.rectTransform.anchoredPosition = position;
			window.FixBounds();
		}

		public void Refresh()
		{
			row.Validate();
			window.Validate();
			bool open = IsOpen;
			string face = open ? "next" : "previous";
			arrow.SetFaceSprites(
				RenderHandler.LoadFromAtlas(Atlas, face),
				RenderHandler.TryLoadFromAtlas(Atlas, face + "_hl"),
				RenderHandler.TryLoadFromAtlas(Atlas, face + "_c"),
				RenderHandler.TryLoadFromAtlas(Atlas, face + "_d"));
			arrow.SetDescription(open ? Loc.Get("ui.storage.hide") : Loc.Get("ui.storage.show"));
			arrow.Validate();
			int top = HeaderHeight + Mathf.Max(0, (window.heightPixels - HeaderHeight - arrow.heightPixels) / 2);
			arrow.handler.rectTransform.anchoredPosition = new Vector2(-arrow.widthPixels + 1, -top);
		}
	}
}
