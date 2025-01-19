using System.Linq;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(BoardDef))]
public class BoardDefInspector : Editor
{
    public override void OnInspectorGUI()
    {
        BoardDef boardDef = (BoardDef)target;
        if (GUILayout.Button("Initialize"))
        {
            boardDef.EditorInitialize();
            EditorUtility.SetDirty(boardDef);
        }
        
        base.OnInspectorGUI();
        

        int maxPawnSum = boardDef.maxPawns.Sum(maxPawns => maxPawns.max);
        EditorGUILayout.LabelField($"maxPawnSum: {maxPawnSum}");
        int redSetupTiles = boardDef.tiles.Count(tile => tile.setupPlayer == Player.RED);
        EditorGUILayout.LabelField($"redSetupTiles: {redSetupTiles}");
        int blueSetupTiles = boardDef.tiles.Count(tile => tile.setupPlayer == Player.BLUE);
        EditorGUILayout.LabelField($"blueSetupTiles: {blueSetupTiles}");
        
        if (GUILayout.Button("Init Max Pawns"))
        {
            boardDef.EditorInitializeMaxPawns();
            EditorUtility.SetDirty(boardDef);
        }
    }
}
