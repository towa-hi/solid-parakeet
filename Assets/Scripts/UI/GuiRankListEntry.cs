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

    public void Initialize(Rank inRank)
    {
        rank = inRank;
        buttonText.text = rank.ToString();
        numberText.text = "";
    }
    
    public void SetButtonOnClick(Action<Rank> buttonAction)
    {
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(delegate { buttonAction(rank); });
    }

    public void Refresh(int max, int used, bool inSelected, bool interactable)
    {
        remaining = max - used;
        numberText.text = remaining.ToString();
        selected = inSelected;
        Color newColor = selected ? Color.green : Color.white;
        if (selected && remaining == 0)
        {
            newColor = Color.red;
        }
        ColorBlock cb = button.colors;
        cb.normalColor = newColor;
        cb.highlightedColor = newColor;
        cb.pressedColor = newColor;
        cb.selectedColor = newColor;
        numberBackground.color = remaining == 0 ? Color.red : Color.white;
        button.colors = cb;
        button.interactable = interactable;
    }
}
