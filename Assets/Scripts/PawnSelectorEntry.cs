using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class PawnSelectorEntry : MonoBehaviour
{
    public PawnDef pawnDef;
    public Button button;
    public TextMeshProUGUI label;

    void OnDestroy()
    {
        button.onClick.RemoveAllListeners();
    }

    public void Initialize(PawnDef inPawnDef, UnityAction<PawnDef> inAction)
    {
        pawnDef = inPawnDef;
        label.SetText(pawnDef.pawnName);
        button.onClick.AddListener(() => inAction(pawnDef));
    }

    
}