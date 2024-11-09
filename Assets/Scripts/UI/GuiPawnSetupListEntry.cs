using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GuiPawnSetupListEntry : MonoBehaviour
{
    public Image pawnIconImage;
    public TextMeshProUGUI pawnNameText;
    public TextMeshProUGUI remainingPawnsText;
    public Image panelBackground;
    public PawnDef pawnDef;
    public int remainingPawns;
    Color originalColor = new Color(1f, 1f, 1f, 100f / 255f);
    Color exhaustedColor = Color.black;
    
    public void SetPawn(PawnDef inPawnDef, int inRemainingPawns)
    {
        pawnDef = inPawnDef;
        remainingPawns = inRemainingPawns;
        pawnNameText.text = pawnDef.pawnName;
        pawnIconImage.sprite = pawnDef.icon;
        UpdateEntry();
    }

    public void DecrementCount()
    {
        remainingPawns -= 1;
        UpdateEntry();
    }

    public void IncrementCount()
    {
        remainingPawns += 1;
        UpdateEntry();
    }

    void UpdateEntry()
    {
        remainingPawnsText.text = remainingPawns.ToString();
        panelBackground.color = remainingPawns == 0 ? exhaustedColor : originalColor;
    }
}
