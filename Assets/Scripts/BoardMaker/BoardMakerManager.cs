using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public class BoardMakerManager : MonoBehaviour
{
    public BoardGrid grid;
    public BoardDef currentlyLoadedBoardDef;
    public GuiBoardMaker gui;
    public Dictionary<Tile, Vector2Int> tiles;
    public GameObject boardMakerTilePrefab;
    public GameObject boardObject;
    
    public BoardDef CreateBoardDef(string path)
    {
        BoardDef boardDef = ScriptableObject.CreateInstance<BoardDef>();
        #if UNITY_EDITOR
            // Save the ScriptableObject as an asset in the specified path
            AssetDatabase.CreateAsset(boardDef, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Created BoardDef at {path}");
        #endif

        return boardDef;
    }

    public void LoadBoard()
    {
        if (!currentlyLoadedBoardDef)
        {
            return;
        }
        grid.SetBoard(new SBoardDef(currentlyLoadedBoardDef));
        foreach (Tile tile in currentlyLoadedBoardDef.tiles)
        {
            Vector3 worldPosition = grid.CellToWorld(tile.pos);
            GameObject tileObject = Instantiate(boardMakerTilePrefab, worldPosition, Quaternion.identity, boardObject.transform);
            
            
        }
    }
}
