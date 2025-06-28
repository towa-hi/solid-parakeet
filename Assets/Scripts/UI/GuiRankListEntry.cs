using System;
using Contract;
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

    public void Initialize(Rank inRank, uint max, bool interactable)
    {
        rank = inRank;
        remaining = (int)max;
        buttonText.text = rank.ToString();
        numberText.text = max.ToString();
        selected = false;
        Color newColor = selected ? Color.green : Color.white;
        ColorBlock cb = button.colors;
        cb.normalColor = newColor;
        cb.highlightedColor = newColor;
        cb.pressedColor = newColor;
        cb.selectedColor = newColor;
        numberBackground.color = remaining == 0 ? Color.red : Color.white;
        button.colors = cb;
        button.interactable = interactable;
    }
    
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

    public void SetButtonOnClick(Action<Rank> buttonAction)
    {
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(delegate { buttonAction(rank); });
    }

    public void SetRemaining(uint max, int used)
    {
        remaining = (int)(max - used);
    }
}
