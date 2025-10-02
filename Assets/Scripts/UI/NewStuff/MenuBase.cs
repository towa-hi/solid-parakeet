using System;
using System.Threading.Tasks;
using UnityEngine;
using Contract;


[RequireComponent(typeof(CanvasGroup))]
public abstract class MenuBase : MonoBehaviour
{
    public Area area;
    protected CanvasGroup canvasGroup;
    protected MenuController menuController;
    public Action OnTransitionStart;
    public Action OnTransitionEnd;
    public Action OnClosed;
    public Action OnOpened;
    public float openDuration;
    public float closeDuration;
    bool _isClosing;
    bool _isOpening;
    
    protected void Awake()
    {
        if (!canvasGroup)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }
        Display(false);
    }

    public virtual void SetMenuController(MenuController controller)
    {
        menuController = controller;
    }

    public virtual void Display(bool display)
    {
        gameObject.SetActive(display);
    }

    public abstract void Refresh();

    public void SetInteractable(bool interactable)
    {
        if (canvasGroup == null) return;
        Debug.Log($"SetInteractable: {name} {interactable}");
        canvasGroup.interactable = interactable;
        canvasGroup.blocksRaycasts = interactable;
    }

    public void Close()
    {
        if (_isClosing) return;
		LogTaskExceptions(CloseAsync());
    }

    public virtual async Task CloseAsync()
    {
        if (_isClosing) return;
        _isClosing = true;
        OnTransitionStart?.Invoke();

        if (closeDuration > 0f && canvasGroup != null)
        {
            float startAlpha = canvasGroup.alpha;
            float elapsed = 0f;
            while (elapsed < closeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / closeDuration);
                float eased = EaseInCubic(t);
                canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, eased);
                await Task.Yield();
            }
            canvasGroup.alpha = 0f;
        }
        else
        {
            // Ensure at least one frame passes to simulate an async transition.
            await Task.Yield();
        }

        OnTransitionEnd?.Invoke();
        Display(false);
        _isClosing = false;
        OnClosed?.Invoke();
    }

    public void Open()
    {
        if (_isOpening) return;
		LogTaskExceptions(OpenAsync());
    }

    public virtual async Task OpenAsync()
    {
        if (_isOpening) return;
        _isOpening = true;
        // Debug.Log("MenuBase: invoking OnTransitionStart");
        OnTransitionStart?.Invoke();

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
        }
        Display(true);

        if (openDuration > 0f && canvasGroup != null)
        {
            float startAlpha = canvasGroup.alpha;
            float elapsed = 0f;
            while (elapsed < openDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / openDuration);
                float eased = EaseOutCubic(t);
                canvasGroup.alpha = Mathf.Lerp(startAlpha, 1f, eased);
                await Task.Yield();
            }
            canvasGroup.alpha = 1f;
        }
        else
        {
            await Task.Yield();
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
            }
        }
        // Debug.Log("MenuBase: invoking OnTransitionEnd");
        OnTransitionEnd?.Invoke();
        _isOpening = false;
        OnOpened?.Invoke();
    }

    static float EaseInCubic(float t)
    {
        return t * t * t;
    }

    static float EaseOutCubic(float t)
    {
        t = 1f - t;
        return 1f - t * t * t;
    }

	// Ensure exceptions from fire-and-forget Tasks are surfaced to Unity's Console
	static void LogTaskExceptions(Task task)
	{
		if (task == null) return;
		if (task.IsCompleted)
		{
			if (task.IsFaulted && task.Exception != null)
			{
				Debug.LogException(task.Exception);
			}
		}
		else
		{
			_ = task.ContinueWith(t =>
			{
				if (t.IsFaulted && t.Exception != null)
				{
					Debug.LogException(t.Exception);
				}
			}, TaskContinuationOptions.OnlyOnFaulted);
		}
	}
}




