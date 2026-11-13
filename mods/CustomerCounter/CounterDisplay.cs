using UnityEngine;

namespace CustomerCounter
{
	public class CounterDisplay : MonoBehaviour
	{
		private const float WorldX = -2.675f;
		private const float WorldY = 1.54f;
		private const float WorldZ = 0.02f;
		// above the calendar/window-frame sprites (10-12) so the casing sits in front
		private const int SortingOrder = 50;

		private static GameObject display;
		private static Texture2D texture;
		private static Sprite sprite;
		private static SpriteRenderer renderer;
		private static int lastCount = -1;

		public static void EnsureUpdater()
		{
			if (Object.FindObjectOfType<CounterDisplay>() != null)
			{
				return;
			}
			GameObject runner = new GameObject("CustomerCounterRunner");
			Object.DontDestroyOnLoad(runner);
			runner.AddComponent<CounterDisplay>();
		}

		public static void DestroyAll()
		{
			DestroyDisplay();
			CounterDisplay runner = Object.FindObjectOfType<CounterDisplay>();
			if (runner != null)
			{
				Object.Destroy(runner.gameObject);
			}
		}

		private void Update()
		{
			if (display == null)
			{
				EnsureDisplay();
			}
			if (display == null || texture == null)
			{
				return;
			}

			int count = GetRemainingCustomers();
			if (count != lastCount)
			{
				lastCount = count;
				CounterArt.Draw(texture, count);
			}
		}

		public static void EnsureDisplay()
		{
			EmporiumEntry entry = EmporiumEntry.Instance;
			if (entry == null || entry.storePhysical == null)
			{
				return;
			}
			if (display != null && display.transform.parent == entry.storePhysical.transform)
			{
				return;
			}

			DestroyDisplay();

			display = new GameObject("CustomerCounterDisplay");
			display.transform.SetParent(entry.storePhysical.transform, false);
			display.transform.localPosition = new Vector3(WorldX, WorldY, WorldZ);
			display.transform.localRotation = Quaternion.identity;
			display.transform.localScale = Vector3.one;
			display.layer = entry.storePhysical.layer;

			renderer = display.AddComponent<SpriteRenderer>();
			renderer.sortingLayerName = "Default";
			renderer.sortingOrder = SortingOrder;

			if (texture == null)
			{
				texture = CounterArt.CreateTexture();
			}
			if (sprite == null)
			{
				sprite = Sprite.Create(
					texture,
					new Rect(0f, 0f, CounterArt.Width, CounterArt.Height),
					new Vector2(0.5f, 0.5f),
					100f,
					0,
					SpriteMeshType.FullRect);
				sprite.name = "CustomerCounterPanel";
			}

			renderer.sprite = sprite;
			lastCount = -1;
		}

		public static void DestroyDisplay()
		{
			if (display != null)
			{
				Object.Destroy(display);
			}
			display = null;
			renderer = null;
			lastCount = -1;
		}

		private static int GetRemainingCustomers()
		{
			PlayerStore store = PlayerStore.Instance;
			if (store == null || store.storeClientManager == null || store.storeClientManager.clientStack == null)
			{
				return 0;
			}
			int count = store.storeClientManager.clientStack.Count;
			if (store.currentClientInstance != null)
			{
				count++;
			}
			return Mathf.Clamp(count, 0, 99);
		}
	}
}
