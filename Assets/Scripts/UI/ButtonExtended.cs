using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class ButtonExtended : Button
{
	public CanvasGroup canvasGroup;
	public TextMeshProUGUI text;
	public Image frame;

	public ButtonClickType buttonClickType;
	[SerializeField] public Color textAndFrameColor = Color.white;
	[SerializeField] public Color disabledTextColor = new Color(1f, 1f, 1f, 0.5f);

	protected override void Awake()
	{
		base.Awake();
		if (canvasGroup == null)
		{
			canvasGroup = GetComponent<CanvasGroup>();
		}
		if (text == null)
		{
			text = GetComponentInChildren<TextMeshProUGUI>();
		}
		ApplyColorsForCurrentState();
	}

	protected override void OnEnable()
	{
		base.OnEnable();
		ApplyColorsForCurrentState();
	}

	// protected override void OnValidate()
	// {
	// 	base.OnValidate();
	// 	ApplyColorsForCurrentState();
	// }

	private void ApplyColorsForCurrentState()
	{
		Color baseColor = textAndFrameColor;
		bool isDisabled = !IsInteractable();
		if (isDisabled)
		{
			if (text != null)
			{
				text.color = baseColor * disabledTextColor;
			}
			if (frame != null)
			{
				frame.color = baseColor * disabledTextColor;
			}
		}
		else
		{
			if (text != null)
			{
				text.color = baseColor;
			}
			if (frame != null && frame != targetGraphic)
			{
				frame.color = baseColor;
			}
		}
	}

	protected override void DoStateTransition(SelectionState state, bool instant)
	{
		base.DoStateTransition(state, instant);

		bool isDisabled = state == SelectionState.Disabled;
		Color baseColor = textAndFrameColor;
		if (isDisabled)
		{
			if (text != null)
			{
				text.color = baseColor * disabledTextColor;
			}
			if (frame != null)
			{
				frame.color = baseColor * disabledTextColor;
			}
		}
		else
		{
			if (text != null)
			{
				text.color = baseColor;
			}
			if (frame != null && frame != targetGraphic)
			{
				frame.color = baseColor;
			}
		}
	}

	protected override void OnCanvasGroupChanged()
	{
		base.OnCanvasGroupChanged();
		ApplyColorsForCurrentState();
	}

	public override void OnPointerEnter(PointerEventData eventData)
	{
		base.OnPointerEnter(eventData);
		if (IsInteractable())
		{
			AudioManager.PlayButtonHover();
		}
	}

	public override void OnPointerClick(PointerEventData eventData)
	{
		base.OnPointerClick(eventData);
		Debug.Log($"OnPointerClick: {buttonClickType}");
		AudioManager.PlayButtonClick(buttonClickType);
	}
}
