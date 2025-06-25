using Contract;
using TMPro;
using UnityEngine;

public class GuiMaxPawnListEntry : MonoBehaviour
{
    public TextMeshProUGUI rankText;
    public TextMeshProUGUI maxText;
    
    public void Initialize(Rank rank, uint max)
    {
        rankText.text = rank.ToString();
        maxText.text = max.ToString();
    }
}
