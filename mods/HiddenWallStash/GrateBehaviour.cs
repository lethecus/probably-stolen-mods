using UnityEngine;
using UnityEngine.EventSystems;

public class GrateBehaviour : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
	private static readonly Color NormalColor = Color.white;
	private static readonly Color HoverColor = new Color(1.22f, 1.22f, 1.22f, 1f);

	private SpriteRenderer spriteRenderer;

	private void Awake()
	{
		spriteRenderer = GetComponent<SpriteRenderer>();
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
		if (spriteRenderer != null)
		{
			spriteRenderer.color = HoverColor;
		}
	}

	public void OnPointerExit(PointerEventData eventData)
	{
		if (spriteRenderer != null)
		{
			spriteRenderer.color = NormalColor;
		}
	}
}
