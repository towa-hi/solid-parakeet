using System.Threading.Tasks;
using UnityEngine;

[RequireComponent(typeof(CanvasGroup))]
public abstract class ModalBase : MonoBehaviour
{
    public CanvasGroup canvasGroup;
    TaskCompletionSource<bool> closeTcs;
    protected void Awake()
    {
        if (!canvasGroup)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }
        // Ensure modal input is not affected by parent CanvasGroups
        if (canvasGroup != null)
        {
            canvasGroup.ignoreParentGroups = true;
        }
    }

    public abstract void OnFocus(bool focused);

    public void PrepareAwaitable()
    {
        closeTcs = new TaskCompletionSource<bool>();
    }

    public Task AwaitCloseAsync()
    {
        return closeTcs != null ? closeTcs.Task : Task.CompletedTask;
    }

    public void CompleteClose()
    {
        closeTcs?.TrySetResult(true);
    }
}
