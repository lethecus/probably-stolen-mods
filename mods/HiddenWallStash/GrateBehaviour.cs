using UnityEngine;
using UnityEngine.EventSystems;
using EPOOutline;

public class GrateBehaviour : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
	// Same hover colour the game's own Interactable outlines use.
	private static readonly Color HoverOutlineColor = new Color32(225, 209, 156, byte.MaxValue);

	private Outlinable outlinable;

	private void Awake()
	{
		outlinable = GetComponent<Outlinable>();
		if (outlinable != null)
		{
			outlinable.OutlineParameters.BlurShift = 0f;
			outlinable.OutlineParameters.DilateShift = 1f;
			outlinable.OutlineParameters.Color = HoverOutlineColor;
			outlinable.enabled = false;
		}
	}

	public void OnPointerClick(PointerEventData eventData)
	{
		if (eventData != null && eventData.button != PointerEventData.InputButton.Left)
		{
			return;
		}
		StashManager.ToggleWindow();
	}

	public void OnPointerEnter(PointerEventData eventData)
	{
		if (outlinable != null)
		{
			outlinable.enabled = true;
		}
	}

	public void OnPointerExit(PointerEventData eventData)
	{
		if (outlinable != null)
		{
			outlinable.enabled = false;
		}
	}

	// True while the mouse is over the grate in screen space. Used to let items be
	// dropped straight into the stash without opening the window.
	public static bool IsPointerOverGrate()
	{
		GameObject grate = GrateFactory.Grate;
		if (grate == null)
		{
			return false;
		}
		SpriteRenderer spriteRenderer = grate.GetComponent<SpriteRenderer>();
		Camera camera = Camera.main;
		if (spriteRenderer == null || camera == null)
		{
			return false;
		}
		Vector3 screenPoint = Input.mousePosition;
		screenPoint.z = Mathf.Abs(camera.transform.position.z - spriteRenderer.transform.position.z);
		Vector3 worldPoint = camera.ScreenToWorldPoint(screenPoint);
		Bounds bounds = spriteRenderer.bounds;
		return worldPoint.x >= bounds.min.x && worldPoint.x <= bounds.max.x
			&& worldPoint.y >= bounds.min.y && worldPoint.y <= bounds.max.y;
	}
}
