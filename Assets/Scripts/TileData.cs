using UnityEngine;

[System.Serializable]
public class TileData
{
    public Vector2Int pos;
    public bool isPassable = true;
    public TileSetup tileSetup = TileSetup.NONE;
    
    public void Initialize(Vector2Int newPos)
    {
        pos = newPos;
    }
}
