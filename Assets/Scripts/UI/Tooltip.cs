using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class Tooltip : MonoBehaviour
{
    public static Tooltip Instance { get; private set; }

    public TextMeshProUGUI header;
    public TextMeshProUGUI body;
    public Vector2 offset;
    public float edgePadding = 8f;
	public float fadeDuration = 0.15f;

    RectTransform _rectTransform;
    Canvas _rootCanvas;
    bool _locked;
	CanvasGroup _canvasGroup;
	float _targetAlpha;
	bool _isFading;
	bool _deactivateOnFadeComplete;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

		_rectTransform = transform as RectTransform;
		_rootCanvas = GetComponentInParent<Canvas>();
		_canvasGroup = GetComponent<CanvasGroup>();
		if (_canvasGroup == null)
			_canvasGroup = gameObject.AddComponent<CanvasGroup>();
		_canvasGroup.interactable = false;
		_canvasGroup.blocksRaycasts = false;
		_canvasGroup.alpha = 0f;
		_targetAlpha = 0f;
        if (_rectTransform != null)
        {
            // Default to bottom-middle of the cursor
            _rectTransform.pivot = new Vector2(0.5f, 0f);
        }
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void SetText(string headerText, string bodyText)
    {
        header.text = headerText;
        body.text = bodyText;
        header.gameObject.SetActive(headerText != "");
        body.gameObject.SetActive(bodyText != "");

        if (_rectTransform == null)
            _rectTransform = transform as RectTransform;
        // Ensure layout updates so size is accurate before positioning
        LayoutRebuilder.ForceRebuildLayoutImmediate(_rectTransform);
    }

	public void SetVisible(bool visible)
	{
		if (visible)
		{
			if (!gameObject.activeSelf)
				gameObject.SetActive(true);
			_deactivateOnFadeComplete = false;
			StartFade(1f);
		}
		else
		{
			_deactivateOnFadeComplete = true;
			StartFade(0f);
		}
	}

    public void SetTextAndVisible(string headerText, string bodyText, bool visible)
    {
        SetText(headerText, bodyText);
        SetVisible(visible);
    }

	public void ShowAtPointerResetFade(string headerText, string bodyText)
	{
		SetText(headerText, bodyText);
		if (!gameObject.activeSelf)
			gameObject.SetActive(true);
		_deactivateOnFadeComplete = false;
		if (_canvasGroup != null)
			_canvasGroup.alpha = 0f;
		UpdatePositionNow();
		StartFade(1f);
	}

    // Update is called once per frame
	void Update()
    {
		if (gameObject.activeSelf)
		{
			UpdateFade();
		}
		if (!gameObject.activeSelf) return;
        if (_locked || _isFading) return;

        Vector2 mousePosition = Globals.InputActions != null
            ? Globals.InputActions.Game.PointerPosition.ReadValue<Vector2>()
            : (Vector2)Input.mousePosition;

        ApplyPositionFromScreenPoint(mousePosition);
    }

    public void UpdatePositionNow()
    {
        Vector2 mousePosition = Globals.InputActions != null
            ? Globals.InputActions.Game.PointerPosition.ReadValue<Vector2>()
            : (Vector2)Input.mousePosition;
        ApplyPositionFromScreenPoint(mousePosition);
    }

    public void SetLocked(bool locked)
    {
        _locked = locked;
    }

    Vector2 GetAutoPivot(Vector2 anchorScreenPos)
    {
        if (_rectTransform == null)
            _rectTransform = transform as RectTransform;

        float scaleFactor = _rootCanvas != null ? _rootCanvas.scaleFactor : 1f;
        Vector2 size = _rectTransform.rect.size * scaleFactor;

        // Horizontal: default center (0.5). If centered placement clips sides, snap to left/right.
        float pivotX = 0.5f;
        if (anchorScreenPos.x - (size.x * 0.5f) - edgePadding < 0f)
        {
            pivotX = 0f; // place to the right of the cursor
        }
        else if (anchorScreenPos.x + (size.x * 0.5f) + edgePadding > Screen.width)
        {
            pivotX = 1f; // place to the left of the cursor
        }

        // Vertical: default bottom (0). If placing above would clip top, use top (1) to place below.
        float pivotY = 0f;
        if (anchorScreenPos.y + size.y + edgePadding > Screen.height)
        {
            pivotY = 1f;
        }

        return new Vector2(pivotX, pivotY);
    }

    void ApplyPositionFromScreenPoint(Vector2 screenPoint)
    {
        Vector2 autoPivot = GetAutoPivot(screenPoint);
        if (_rectTransform.pivot != autoPivot)
            _rectTransform.pivot = autoPivot;

        // Only use offset for the original bottom-right style; otherwise no offset.
        Vector2 appliedOffset = (autoPivot.x == 0f && autoPivot.y == 1f) ? offset : Vector2.zero;
        transform.position = screenPoint + appliedOffset;
    }

	void StartFade(float toAlpha)
	{
		_targetAlpha = Mathf.Clamp01(toAlpha);
		_isFading = true;
	}

	void UpdateFade()
	{
		if (!_isFading || _canvasGroup == null)
			return;

		if (fadeDuration <= 0f)
		{
			_canvasGroup.alpha = _targetAlpha;
		}
		else
		{
			float step = Time.unscaledDeltaTime / fadeDuration;
			_canvasGroup.alpha = Mathf.MoveTowards(_canvasGroup.alpha, _targetAlpha, step);
		}

		if (Mathf.Approximately(_canvasGroup.alpha, _targetAlpha))
		{
			_isFading = false;
			if (_deactivateOnFadeComplete && _targetAlpha <= 0f)
			{
				gameObject.SetActive(false);
			}
		}
	}
}
