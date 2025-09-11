using UnityEngine;

[RequireComponent(typeof(CanvasGroup))]
public abstract class ModalBase : MonoBehaviour
{
    public CanvasGroup canvasGroup;
    protected void Awake()
    {
        if (!canvasGroup)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }
    }

    public abstract void OnFocus(bool focused);
}
