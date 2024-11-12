using UnityEngine;
using UnityEngine.UI;

public class RadialMenuEntry : MonoBehaviour
{
    public Image image;
    public PawnDef pawnDef;
    
    public void Initialize(PawnDef inPawnDef)
    {
        
        pawnDef = inPawnDef;
        if (pawnDef == null)
        {
            Debug.Log("initialized empty");
            return;
        }
        else
        {
            Debug.Log($"initialized {pawnDef.pawnName}");
            image.sprite = pawnDef.icon;
        }
        
    }
}
