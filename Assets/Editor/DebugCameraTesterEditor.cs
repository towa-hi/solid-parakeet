using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(DebugCameraTester))]
public class DebugCameraTesterEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        
        DebugCameraTester debugCameraTester = (DebugCameraTester)target;
        
        if (GUILayout.Button("Update Test Pawn"))
        {
            debugCameraTester.SendMessage("UpdateTestPawn");
        }
        
        if (GUILayout.Button("Toggle Test Pawn Selection"))
        {
            debugCameraTester.testPawnIsSelected = !debugCameraTester.testPawnIsSelected;
            debugCameraTester.SendMessage("UpdateTestPawnAnimationState");
        }

        if (GUILayout.Button("set Hurt"))
        {
            debugCameraTester.SendMessage("UpdateTestPawnAnimationStateHurt");
        }
    }
}