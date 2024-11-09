using Linework.Editor.Utils;
using Linework.FastOutline;
using UnityEditor;
using UnityEditor.Rendering;

namespace Linework.Editor.FastOutline
{
    [CustomEditor(typeof(FastOutlineSettings))]
    public class FastOutlineSettingsEditor : UnityEditor.Editor
    {
        private SerializedProperty injectionPoint;
        private SerializedProperty showInSceneView;
        private SerializedProperty debugStage;
        
        private SerializedProperty outlines;
        private EditorList<Outline> outlineList;

        private void OnEnable()
        {
            injectionPoint = serializedObject.FindProperty("injectionPoint");
            showInSceneView = serializedObject.FindProperty("showInSceneView");
            debugStage = serializedObject.FindProperty("debugStage");
            
            outlines = serializedObject.FindProperty("outlines");
            outlineList = new EditorList<Outline>(this, outlines, ForceSave, "Add Outline", "No outlines added.");
        }

        private void OnDisable()
        {
            outlineList.OnDisable();
        }

        public override void OnInspectorGUI()
        {
            if (outlines == null) OnEnable();

            serializedObject.Update();
            
            EditorGUILayout.LabelField("Fast Outline", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(injectionPoint, EditorUtils.CommonStyles.InjectionPoint);
            EditorGUILayout.PropertyField(showInSceneView, EditorUtils.CommonStyles.ShowInSceneView);
            EditorGUILayout.PropertyField(debugStage, EditorUtils.CommonStyles.DebugStage);
            EditorGUILayout.Space();
            CoreEditorUtils.DrawSplitter();
            serializedObject.ApplyModifiedProperties();
            
            EditorGUILayout.LabelField(EditorUtils.CommonStyles.Outlines, EditorStyles.boldLabel);
            outlineList.Draw();
        }

        private void ForceSave()
        {
            ((FastOutlineSettings) target).Changed();
            EditorUtility.SetDirty(target);
        }
    }
}