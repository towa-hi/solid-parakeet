using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class ButtonExtended : Button
{
	public CanvasGroup canvasGroup;
	public TextMeshProUGUI text;

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
	}
}
