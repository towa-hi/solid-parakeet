using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class GuiRankListEntry : MonoBehaviour
{
    public Button button;
    public TextMeshProUGUI buttonText;
    public TextMeshProUGUI numberText;
    public Image numberBackground;
    public Rank rank;
    public int remaining;
    public bool selected;
    
    public void Refresh(Rank inRank, int inRemaining, bool clicked)
    {
        rank = inRank;
        remaining = inRemaining;
        selected = clicked;
        buttonText.text = rank.ToString();
        numberText.text = inRemaining.ToString();
        Color newColor = selected ? Color.green : Color.white;
        ColorBlock cb = button.colors;
        cb.normalColor = newColor;
        cb.highlightedColor = newColor;
        cb.pressedColor = newColor;
        cb.selectedColor = newColor;
        numberBackground.color = remaining == 0 ? Color.red : Color.white;
        button.colors = cb;
    }

    public void SetButtonOnClick(UnityAction<GuiRankListEntry> buttonAction)
    {
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(delegate { buttonAction(this); });
    }
}
