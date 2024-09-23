using System;
using UnityEngine;

public class TileView : MonoBehaviour
{
    public Vector2Int pos = new Vector2Int(-1, -1);
    public TileData tileData;
    
    void Start()
    {

    }

    public void Initialize(TileData tileData)
    {
        pos = tileData.pos;
        GetComponent<DebugText>()?.SetText(pos.ToString());
    }
}
