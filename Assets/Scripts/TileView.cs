using System;
using UnityEngine;

public class TileView : MonoBehaviour
{
    public Vector2Int pos = new Vector2Int(-1, -1);

    void Start()
    {
        Initialize(pos);
    }

    public void Initialize(Vector2Int newPos)
    {
        if (newPos.x < 0 || newPos.y < 0)
        {
            Debug.LogError("Position must be greater than or equal to (0, 0). Provided value: " + pos);
            return;  // Exit the method if the position is invalid
        }
        
        pos = newPos;
        GetComponent<DebugText>()?.SetText(pos.ToString());
    }
}
