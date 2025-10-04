using UnityEngine;
using TMPro;

public class GraveyardListEntry : MonoBehaviour
{
    public TextMeshProUGUI rank;
    public TextMeshProUGUI red;
    public TextMeshProUGUI blue;

    public void Set(Rank rank, int red, int blue, int max)
    {
        this.rank.text = rank.ToString();
        string redText = $"{red}/{max}";
        string blueText = $"{blue}/{max}";
        this.red.text = redText;
        this.blue.text = blueText;
    }
}


