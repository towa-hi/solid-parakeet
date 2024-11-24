using TMPro;
using UnityEngine;

public class GuiEndScreen : MonoBehaviour
{
    public TextMeshProUGUI winnerText;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void Initialize(int winnerPlayer)
    {
        if (winnerPlayer == 0)
        {
            Debug.Log("The game isn't over yet!");
        }
        else if (winnerPlayer == 1)
        {
            Debug.Log("Red player won!");
        }
        else if (winnerPlayer == 2)
        {
            Debug.Log("Blue player won!");
        }
        else if (winnerPlayer == 3)
        {
            Debug.Log("Both players lost!");
        }
    }
}
