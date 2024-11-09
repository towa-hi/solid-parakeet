using Linework.Editor.Utils;
using Linework.SoftOutline;
using UnityEditor;

namespace Linework.Editor.SoftOutline
{
    [CustomEditor(typeof(Outline))]
    public class OutlineEditor : UnityEditor.Editor
    {
        private SerializedProperty renderingLayer;
        private SerializedProperty occlusion;
        private SerializedProperty closedLoop;
        private SerializedProperty alphaCutout, alphaCutoutTexture, alphaCutoutThreshold;
        private SerializedProperty color;
        private SerializedProperty disableColor;

        private void OnEnable()
        {
            renderingLayer = serializedObject.FindProperty(nameof(Outline.RenderingLayer));
            occlusion = serializedObject.FindProperty(nameof(Outline.occlusion));
            closedLoop = serializedObject.FindProperty(nameof(Outline.closedLoop));
            alphaCutout = serializedObject.FindProperty(nameof(Outline.alphaCutout));
            alphaCutoutTexture = serializedObject.FindProperty(nameof(Outline.alphaCutoutTexture));
            alphaCutoutThreshold = serializedObject.FindProperty(nameof(Outline.alphaCutoutThreshold));
            color = serializedObject.FindProperty(nameof(Outline.color));
            disableColor = serializedObject.FindProperty("disableColor");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("Render", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(renderingLayer, EditorUtils.CommonStyles.OutlineLayer);
            EditorGUILayout.PropertyField(occlusion, EditorUtils.CommonStyles.OutlineOcclusion);
            if((Occlusion) occlusion.intValue == Occlusion.WhenNotOccluded)
            {
                EditorGUILayout.PropertyField(closedLoop, EditorUtils.CommonStyles.ClosedLoop);
            }
            EditorGUILayout.PropertyField(alphaCutout, EditorUtils.CommonStyles.AlphaCutout);
            if (alphaCutout.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(alphaCutoutTexture, EditorUtils.CommonStyles.AlphaCutoutTexture); 
                EditorGUILayout.PropertyField(alphaCutoutThreshold, EditorUtils.CommonStyles.AlphaCutoutThreshold);
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.Space();
            
            if ((SoftOutlineOcclusion) occlusion.intValue != SoftOutlineOcclusion.AsMask)
            {
                EditorGUILayout.LabelField("Outline", EditorStyles.boldLabel);
                if (!disableColor.boolValue)
                {
                    EditorGUILayout.PropertyField(color, EditorUtils.CommonStyles.OutlineColor);
                }
            }
            else
            {
                EditorGUILayout.HelpBox("The mask mode is used to mask out the other outlines where they are not needed.", MessageType.Info);
            }
            EditorGUILayout.Space();
            serializedObject.ApplyModifiedProperties();
        }
    }
}