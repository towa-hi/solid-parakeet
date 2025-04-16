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
    public Rank rank;
    public int remaining;
    public bool selected;
    
    public void Refresh(Rank inRank, int inRemaining, bool clicked)
    {
        Debug.Log(inRank + " - " + clicked);
        rank = inRank;
        remaining = inRemaining;
        if (selected && clicked)
        {
            selected = false;
        }
        else
        {
            selected = clicked;
        }
        button.interactable = inRemaining > 0;
        buttonText.text = rank.ToString();
        numberText.text = inRemaining.ToString();
        Color newColor = selected ? Color.green : Color.white;
        ColorBlock cb = button.colors;
        cb.normalColor = newColor;
        cb.highlightedColor = newColor;
        cb.pressedColor = newColor;
        cb.selectedColor = newColor;
        button.colors = cb;
    }

    public void SetButtonOnClick(UnityAction<GuiRankListEntry> buttonAction)
    {
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(delegate { buttonAction(this); });
    }
}
