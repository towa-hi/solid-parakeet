using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(BoardDef))]
public class BoardDefInspector : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        BoardDef boardDef = (BoardDef)target;
        if (GUILayout.Button("Initialize"))
        {
            boardDef.EditorInitialize();
            EditorUtility.SetDirty(boardDef);
        }

        if (GUILayout.Button("Init Max Pawns"))
        {
            boardDef.EditorInitializeMaxPawns();
            EditorUtility.SetDirty(boardDef);
        }
    }
}
