using UnityEngine;

public class GuiElement : MonoBehaviour
{
    [SerializeField] Vector2 hiddenPosition;
    [SerializeField] Vector2 activePosition;
    RectTransform rectTransform;
    [SerializeField] bool isShow; // Removed readonly

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        SetHiddenPosition(rectTransform.anchoredPosition);
        SetActivePosition(Vector2.zero);
        ShowElement(false);
    }

    public virtual void EnableElement(bool enable)
    {
        
    }
    
    public virtual void ShowElement(bool show)
    {
        if (show == isShow)
        {
            return;
        }
        isShow = show; // Update isShow
        rectTransform.anchoredPosition = show ? activePosition : hiddenPosition;
    }

    public virtual void SetHiddenPosition(Vector2 inHiddenPosition)
    {
        hiddenPosition = inHiddenPosition;
    }

    public void SetActivePosition(Vector2 inActivePosition)
    {
        activePosition = inActivePosition;
    }
}
