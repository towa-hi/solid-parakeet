using System;
using System.Threading.Tasks;
using UnityEngine;

[RequireComponent(typeof(CanvasGroup))]
public abstract class MenuBase : MonoBehaviour
{
    protected CanvasGroup canvasGroup;
    public CameraAnchor cameraAnchor;
    public Action OnTransitionStart;
    public Action OnTransitionEnd;
    public Action OnClosed;
    public Action OnOpened;
    public event Action<MenuAction, object> ActionInvoked;
    public event Action<IMenuCommand> CommandInvoked;

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
    }

    public virtual void Display(bool display)
    {
        gameObject.SetActive(display);
    }

    public abstract void Refresh();

    public void SetInteractable(bool interactable)
    {
        if (canvasGroup == null) return;
        canvasGroup.interactable = interactable;
        canvasGroup.blocksRaycasts = interactable;
    }

    public void Close()
    {
        if (_isClosing) return;
        _ = CloseAsync();
    }

    public virtual async Task CloseAsync()
    {
        if (_isClosing) return;
        _isClosing = true;
        if (canvasGroup != null)
        {
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
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
        _ = OpenAsync();
    }

    public virtual async Task OpenAsync()
    {
        if (_isOpening) return;
        _isOpening = true;
        if (canvasGroup != null)
        {
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
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
        if (canvasGroup != null)
        {
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }
        _isOpening = false;
        OnOpened?.Invoke();
    }

    protected void EmitAction(MenuAction action)
    {
        ActionInvoked?.Invoke(action, null);
    }

    protected void EmitAction(MenuAction action, object payload)
    {
        ActionInvoked?.Invoke(action, payload);
    }

    protected void Emit(IMenuCommand command)
    {
        CommandInvoked?.Invoke(command);
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
}
