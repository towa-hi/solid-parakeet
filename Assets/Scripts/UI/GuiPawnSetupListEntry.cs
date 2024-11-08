using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GuiPawnSetupListEntry : MonoBehaviour
{
    public Image pawnIconImage;
    public TextMeshProUGUI pawnNameText;
    public TextMeshProUGUI remainingPawnsText;
    [SerializeField] PawnDef pawnDef;
    
    public void SetPawn(PawnDef inPawnDef, int remainingPawns)
    {
        pawnDef = inPawnDef;
        pawnNameText.text = pawnDef.pawnName;
        pawnIconImage.sprite = pawnDef.icon;
        SetRemainingPawns(remainingPawns);
    }

    public void SetRemainingPawns(int remainingPawns)
    {
        remainingPawnsText.text = remainingPawns.ToString();
    }
}
