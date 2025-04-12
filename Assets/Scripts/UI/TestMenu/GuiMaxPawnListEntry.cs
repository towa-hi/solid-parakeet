using Contract;
using TMPro;
using UnityEngine;

public class GuiMaxPawnListEntry : MonoBehaviour
{
    public TextMeshProUGUI rankText;
    public TextMeshProUGUI maxText;
    
    public void Initialize(MaxPawns maxPawns)
    {
        Rank rankEnum = (Rank)maxPawns.rank;
        rankText.text = rankEnum.ToString();
        maxText.text = maxPawns.max.ToString();
    }
}
