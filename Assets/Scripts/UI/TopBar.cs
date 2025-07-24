using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TopBar : MonoBehaviour
{
    public TextMeshProUGUI text;
    public Image background;

    public void Show(bool show)
    {
        gameObject.SetActive(show);
    }

    public void SetView(Color backgroundColor, string message)
    {
        background.color = backgroundColor;
        text.text = $"<mspace=32>{message}</mspace>";
    }
}
