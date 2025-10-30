using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class Tooltip : MonoBehaviour
{
    public static Tooltip Instance { get; private set; }

    public TextMeshProUGUI header;
    public TextMeshProUGUI body;
    public Vector2 offset;
    public float edgePadding = 8f;
	public float fadeDuration = 0.15f;
    public LayerMask physicsMask = Physics.DefaultRaycastLayers;
    RectTransform _rectTransform;
    Canvas _rootCanvas;
    bool _locked;
	CanvasGroup _canvasGroup;
	float _originalAlpha = 1f;
	float _targetAlpha;
	bool _isFading;

	TooltipElement _currentElement;
	float _hoverStartTime;
	bool _shownForThisHover;
	public TooltipElement CurrentElement => _currentElement;

	GameStore _store;

	public void SetStore(GameStore store)
	{
		_store = store;
	}

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
		// Remember the original alpha (fallback to 1 if prefab starts at 0)
		_originalAlpha = _canvasGroup.alpha > 0f ? _canvasGroup.alpha : 1f;
		_canvasGroup.alpha = 0f;
		_targetAlpha = 0f;
        if (_rectTransform != null)
        {
            // Default to bottom-middle of the cursor
            _rectTransform.pivot = new Vector2(0.5f, 0f);
        }
        // Ensure hidden off-screen at start
        MoveOffscreenNow();
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
			StartFade(_originalAlpha);
		}
		else
		{
			// Instantly hide without fading, but keep active for hover detection
			_isFading = false;
			if (_canvasGroup != null)
				_canvasGroup.alpha = 0f;
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

		if (_canvasGroup != null)
			_canvasGroup.alpha = 0f;
		UpdatePositionNow();
		StartFade(_originalAlpha);
	}

    // Update is called once per frame
	void Update()
    {
        UpdateHoverState();

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

    void UpdateHoverState()
    {
        Vector2 mousePosition = Globals.InputActions != null
            ? Globals.InputActions.Game.PointerPosition.ReadValue<Vector2>()
            : (Vector2)Input.mousePosition;

        TooltipElement newElement = FindTooltipElementAt(mousePosition);

        if (newElement != _currentElement)
        {
            if (newElement != null)
            {
                _currentElement = newElement;
                _hoverStartTime = Time.unscaledTime;
                _shownForThisHover = false;
                SetLocked(false);
                SetVisible(false);
            }
            else
            {
                SetVisible(false);
                SetLocked(false);
                _currentElement = null;
                _shownForThisHover = false;
                MoveOffscreenNow();
            }
            return;
        }

        if (_currentElement != null && !_shownForThisHover)
        {
            float delay = Mathf.Max(0f, _currentElement.hoverDelay);
            if (Time.unscaledTime - _hoverStartTime >= delay)
            {
				if (TryResolveTooltipContent(_currentElement, out string dynamicHeader, out string dynamicBody))
                {
                    ShowAtPointerResetFade(dynamicHeader, dynamicBody);
                    SetLocked(true);
                    _shownForThisHover = true;
                }
            }
        }
        else if (_currentElement != null && _shownForThisHover)
        {
			bool hasContent = TryResolveTooltipContent(_currentElement, out string dynamicHeader, out string dynamicBody);
            if (!hasContent)
            {
                SetVisible(false);
                SetLocked(false);
                MoveOffscreenNow();
                _shownForThisHover = false;
            }
            else if ((header != null && header.text != dynamicHeader) || (body != null && body.text != dynamicBody))
            {
                SetText(dynamicHeader, dynamicBody);
            }
        }
        else if (_currentElement == null)
        {
            // Keep hidden off-screen while not hovering any tooltip element
            MoveOffscreenNow();
        }
    }

    TooltipElement FindTooltipElementAt(Vector2 screenPos)
    {
        TooltipElement uiElement = FindUIElementAt(screenPos);
        if (uiElement != null) return uiElement;
        return FindPhysicsElementAt(screenPos);
    }

    TooltipElement FindUIElementAt(Vector2 screenPos)
    {
        if (EventSystem.current == null) return null;
        PointerEventData ped = new PointerEventData(EventSystem.current) { position = screenPos };
        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(ped, results);
        for (int i = 0; i < results.Count; i++)
        {
            GameObject go = results[i].gameObject;
            if (go == null) continue;
            TooltipElement te = go.GetComponentInParent<TooltipElement>(true);
            if (te != null && te.isActiveAndEnabled)
                return te;
        }
        return null;
    }

    bool TryResolveTooltipContent(TooltipElement element, out string headerText, out string bodyText)
    {
        headerText = string.Empty;
        bodyText = string.Empty;
        if (element == null)
        {
            return false;
        }

		if (_store != null && element.TryGetTilePosition(out Vector2Int tilePos))
		{
			if (_store.State.TryBuildTileTooltip(tilePos, out headerText, out bodyText))
			{
				return true;
			}
		}

        if (element.TryGetTooltipContent(out headerText, out bodyText))
        {
            return !string.IsNullOrEmpty(headerText) || !string.IsNullOrEmpty(bodyText);
        }

        headerText = element.header;
        bodyText = element.body;
		return !string.IsNullOrEmpty(headerText) || !string.IsNullOrEmpty(bodyText);
	}

    TooltipElement FindPhysicsElementAt(Vector2 screenPos)
    {
        Camera cam = GetCamera();
        if (cam == null) return null;
        Ray ray = cam.ScreenPointToRay(screenPos);
        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, physicsMask, QueryTriggerInteraction.Collide))
        {
            Transform t = hit.transform;
            return FindTooltipInParents(t);
        }
        return null;
    }

    TooltipElement FindTooltipInParents(Transform t)
    {
        while (t != null)
        {
            TooltipElement te = t.GetComponent<TooltipElement>();
            if (te != null && te.isActiveAndEnabled)
                return te;
            t = t.parent;
        }
        return null;
    }

    Camera GetCamera()
    {
        Camera cam = Camera.main;
        if (cam == null) cam = Camera.current;
        if (cam == null && Camera.allCamerasCount > 0)
            cam = Camera.allCameras[0];
        return cam;
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

    void MoveOffscreenNow()
    {
        transform.position = new Vector2(-10000f, -10000f);
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
			// Never deactivate here; tooltip GameObject must remain active to drive hover detection
		}
	}
}
