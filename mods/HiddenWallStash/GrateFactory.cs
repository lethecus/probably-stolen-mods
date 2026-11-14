using UnityEngine;

public static class GrateFactory
{
	public const float WorldX = 4.32f;
	public const float WorldY = 0.38f;
	public const float WorldZ = 0.01f;
	public const float WidthUnits = 0.52f;
	public const float HeightUnits = 0.34f;

	private const string TexturePath = "Textures/grate.png";

	private static GameObject grate;
	private static Sprite grateSprite;

	public static GameObject Grate => grate;

	public static void Ensure()
	{
		if (grate != null)
		{
			return;
		}
		EmporiumEntry entry = EmporiumEntry.Instance;
		if (entry == null || entry.storePhysical == null)
		{
			return;
		}
		Sprite sprite = LoadSprite();
		if (sprite == null)
		{
			return;
		}

		Transform parent = entry.storePhysical.transform;
		grate = new GameObject("HiddenWallStashGrate");
		grate.transform.SetParent(parent, false);
		grate.transform.localPosition = new Vector3(WorldX, WorldY, WorldZ);
		grate.transform.localRotation = Quaternion.identity;
		grate.transform.localScale = Vector3.one;
		grate.layer = parent.gameObject.layer;

		SpriteRenderer renderer = grate.AddComponent<SpriteRenderer>();
		renderer.sprite = sprite;
		renderer.sortingLayerName = "Default";
		renderer.sortingOrder = 8;

		BoxCollider2D collider = grate.AddComponent<BoxCollider2D>();
		collider.size = new Vector2(WidthUnits, HeightUnits);
		collider.offset = Vector2.zero;
		collider.isTrigger = true;

		// Use the game's own outline component so the grate highlights like every other
		// interactable when the mouse is over it.
		EPOOutline.Outlinable outline = grate.AddComponent<EPOOutline.Outlinable>();
		outline.TryAddTarget(new EPOOutline.OutlineTarget(renderer, 0));
		outline.OutlineParameters.BlurShift = 0f;
		outline.OutlineParameters.DilateShift = 1f;
		outline.enabled = false;

		grate.AddComponent<GrateBehaviour>();
		HiddenWallStash.HiddenWallStashMod.Log.Log("Created the hidden wall grate.");
	}

	private static Sprite LoadSprite()
	{
		if (grateSprite != null)
		{
			return grateSprite;
		}
		byte[] bytes = ModHelper.ReadModFileBytes(HiddenWallStash.HiddenWallStashMod.ModId, TexturePath);
		if (bytes == null || bytes.Length == 0)
		{
			HiddenWallStash.HiddenWallStashMod.Log.Error("Could not read " + TexturePath + " from the mod folder.");
			return null;
		}
		Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
		texture.name = "HiddenWallStashGrate";
		texture.filterMode = FilterMode.Point;
		texture.wrapMode = TextureWrapMode.Clamp;
		if (!ImageConversion.LoadImage(texture, bytes))
		{
			HiddenWallStash.HiddenWallStashMod.Log.Error("Could not decode " + TexturePath + " as a PNG image.");
			UnityEngine.Object.Destroy(texture);
			return null;
		}
		grateSprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
		grateSprite.name = "HiddenWallStashGrate";
		return grateSprite;
	}
}
