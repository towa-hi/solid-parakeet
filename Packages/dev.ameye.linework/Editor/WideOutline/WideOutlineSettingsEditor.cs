using Linework.Editor.Utils;
using Linework.WideOutline;
using UnityEditor;
using UnityEditor.Rendering;

namespace Linework.Editor.WideOutline
{
    [CustomEditor(typeof(WideOutlineSettings))]
    public class WideOutlineSettingsEditor : UnityEditor.Editor
    {
        private SerializedProperty injectionPoint;
        private SerializedProperty showInSceneView;
        private SerializedProperty clearStencil;
        private SerializedProperty debugStage;
        private SerializedProperty blendMode;
        private SerializedProperty scaling;
        private SerializedProperty width;
        private SerializedProperty minWidth;
        private SerializedProperty customDepthBuffer;
        private SerializedProperty occludedColor;
        
        private SerializedProperty outlines;
        private EditorList<Outline> outlineList;

        private void OnEnable()
        {
            injectionPoint = serializedObject.FindProperty("injectionPoint");
            showInSceneView = serializedObject.FindProperty("showInSceneView");
            clearStencil = serializedObject.FindProperty("clearStencil");
            debugStage = serializedObject.FindProperty("debugStage");
            
            blendMode = serializedObject.FindProperty(nameof(WideOutlineSettings.blendMode));
            scaling = serializedObject.FindProperty(nameof(WideOutlineSettings.scaling));
            width = serializedObject.FindProperty(nameof(WideOutlineSettings.width));
            minWidth = serializedObject.FindProperty(nameof(WideOutlineSettings.minWidth));
            customDepthBuffer = serializedObject.FindProperty(nameof(WideOutlineSettings.customDepthBuffer));
            occludedColor = serializedObject.FindProperty(nameof(WideOutlineSettings.occludedColor));
            
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

            var occlusionChanged = false;
            
            EditorGUILayout.LabelField("Wide Outline", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(injectionPoint, EditorUtils.CommonStyles.InjectionPoint);
            EditorGUILayout.PropertyField(showInSceneView, EditorUtils.CommonStyles.ShowInSceneView);
            EditorGUILayout.PropertyField(clearStencil, EditorUtils.CommonStyles.ClearStencil);
            EditorGUILayout.PropertyField(debugStage, EditorUtils.CommonStyles.DebugStage);
            EditorGUILayout.Space();
            CoreEditorUtils.DrawSplitter();
            serializedObject.ApplyModifiedProperties();
            EditorGUILayout.LabelField(EditorUtils.CommonStyles.Outlines, EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(width, EditorUtils.CommonStyles.OutlineWidth);
            // EditorGUILayout.PropertyField(scaling, EditorUtils.CommonStyles.Scaling);
            // switch ((Scaling) scaling.intValue)
            // {
            //     case Scaling.ConstantScreenSize:
            //         EditorGUILayout.PropertyField(width, EditorUtils.CommonStyles.OutlineWidth);
            //         break;
            //     case Scaling.ScaleWithDistance:
            //         EditorGUILayout.PropertyField(width, EditorUtils.CommonStyles.OutlineWidth);
            //         EditorGUILayout.PropertyField(minWidth, EditorUtils.CommonStyles.MinWidth);
            //         break;
            //     default:
            //         throw new ArgumentOutOfRangeException();
            // }
            EditorGUILayout.PropertyField(blendMode, EditorUtils.CommonStyles.OutlineBlendMode);
            
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(customDepthBuffer, EditorUtils.CommonStyles.CustomDepthBuffer);
            occlusionChanged |= EditorGUI.EndChangeCheck();
            if (customDepthBuffer.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(occludedColor,EditorUtils.CommonStyles.OutlineOccludedColor);
                EditorGUI.indentLevel--;
            }
            serializedObject.ApplyModifiedProperties();
            EditorGUILayout.Space();
            outlineList.Draw();
            EditorGUILayout.Space();
            
            if (occlusionChanged)
            {
                ForceSave();
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void ForceSave()
        {
            ((WideOutlineSettings) target).Changed();
            EditorUtility.SetDirty(target);
        }
    }
}