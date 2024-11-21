using TMPro;
using UnityEngine;

public class GuiMoveWaiting : MonoBehaviour
{

    public TextMeshProUGUI header;
    public string waitingForServer = "Waiting for other player...";
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        header.text = waitingForServer;
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
