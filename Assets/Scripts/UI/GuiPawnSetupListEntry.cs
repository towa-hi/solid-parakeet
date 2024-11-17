using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class GuiPawnSetupListEntry : MonoBehaviour, IPointerClickHandler
{
    public Image pawnIconImage;
    public TextMeshProUGUI pawnNameText;
    public TextMeshProUGUI remainingPawnsText;
    public Image panelBackground;
    public PawnDef pawnDef;
    public int remainingPawns;
    Color originalColor = new Color(1f, 1f, 1f, 100f / 255f);
    Color exhaustedColor = Color.black;
    Color selectedColor = Color.red;
    
    [SerializeField] bool isSelected;
    public event Action<GuiPawnSetupListEntry> OnEntryClicked;
    
    public void Initialize(PawnDef inPawnDef, int inMaxPawns, Action<GuiPawnSetupListEntry> action)
    {
        
        Debug.Log("GuiPawnSetupListEntry Initialize");
        OnEntryClicked += action;
        pawnDef = inPawnDef;
        remainingPawns = inMaxPawns;
        pawnNameText.text = inPawnDef.pawnName;
        pawnIconImage.sprite = inPawnDef.icon;
        UpdateEntry();
    }
    
    public void SetPawn(PawnDef inPawnDef, int inRemainingPawns)
    {
        pawnDef = inPawnDef;
        remainingPawns = inRemainingPawns;
        pawnNameText.text = pawnDef.pawnName;
        pawnIconImage.sprite = pawnDef.icon;
        UpdateEntry();
    }

    public void SetCount(int count)
    {
        remainingPawns = count;
        UpdateEntry();
    }
    
    void UpdateEntry()
    {
        remainingPawnsText.text = remainingPawns.ToString();
        if (isSelected)
        {
            panelBackground.color = selectedColor;
        }
        else
        {
            panelBackground.color = remainingPawns == 0 ? exhaustedColor : originalColor;
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        OnEntryClicked?.Invoke(this);
    }

    public void SelectEntry(bool inIsSelected)
    {
        isSelected = inIsSelected;
        UpdateEntry();
    }
    
}
