using System;
using Contract;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class GuiRankListEntry : MonoBehaviour
{
    public TextMeshProUGUI buttonText;
    public TextMeshProUGUI numberText;
    public Image numberBackground;
    public Rank rank;
    public int remaining;
    public bool selected;
    public Color originalColor;

    public void Initialize(Rank inRank)
    {
        rank = inRank;
        buttonText.text = rank.ToString();
        numberText.text = "";
        originalColor = numberBackground.color;
    }

    public void Refresh(int max, int used, bool inSelected, bool interactable)
    {
        remaining = max - used;
        numberText.text = remaining.ToString();
        selected = inSelected;
        Color newColor = originalColor;
        if (selected)
        {
            newColor = Color.yellow;
        }
        if (remaining == 0)
        {
            newColor = Color.gray;
        }
        numberBackground.color = newColor;
    }
}
