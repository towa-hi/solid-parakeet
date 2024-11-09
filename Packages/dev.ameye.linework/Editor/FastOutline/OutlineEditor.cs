using System;
using Linework.Editor.Utils;
using Linework.FastOutline;
using UnityEditor;
using UnityEngine;

namespace Linework.Editor.FastOutline
{
    [CustomEditor(typeof(Outline))]
    public class OutlineEditor : UnityEditor.Editor
    {
        private SerializedProperty renderingLayer;
        private SerializedProperty occlusion;
        private SerializedProperty maskingStrategy;
        private SerializedProperty blendMode;
        private SerializedProperty color;
        private SerializedProperty enableOcclusion;
        private SerializedProperty occludedColor;
        private SerializedProperty extrusionMethod;
        private SerializedProperty scaling;
        private SerializedProperty width;
        private SerializedProperty minWidth;

        private void OnEnable()
        {
            renderingLayer = serializedObject.FindProperty(nameof(Outline.RenderingLayer));
            occlusion = serializedObject.FindProperty(nameof(Outline.occlusion));
            maskingStrategy = serializedObject.FindProperty(nameof(Outline.maskingStrategy));
            blendMode = serializedObject.FindProperty(nameof(Outline.blendMode));
            color = serializedObject.FindProperty(nameof(Outline.color));
            enableOcclusion = serializedObject.FindProperty(nameof(Outline.enableOcclusion));
            occludedColor = serializedObject.FindProperty(nameof(Outline.occludedColor));
            extrusionMethod = serializedObject.FindProperty(nameof(Outline.extrusionMethod));
            scaling = serializedObject.FindProperty(nameof(Outline.scaling));
            width = serializedObject.FindProperty(nameof(Outline.width));
            minWidth = serializedObject.FindProperty(nameof(Outline.minWidth));
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("Render", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(renderingLayer, EditorUtils.CommonStyles.OutlineLayer);
            EditorGUILayout.PropertyField(occlusion, EditorUtils.CommonStyles.OutlineOcclusion);
            EditorGUILayout.PropertyField(blendMode, EditorUtils.CommonStyles.OutlineBlendMode);
            if ((Occlusion) occlusion.intValue == Occlusion.WhenNotOccluded)
            {
                EditorGUILayout.PropertyField(maskingStrategy, EditorUtils.CommonStyles.MaskingStrategy);
            }
   
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Outline", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(color, EditorUtils.CommonStyles.OutlineColor);
            if ((Occlusion) occlusion.intValue == Occlusion.Always)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(enableOcclusion, EditorUtils.CommonStyles.OutlineOccludedColor);
                if (enableOcclusion.boolValue) EditorGUILayout.PropertyField(occludedColor, GUIContent.none);
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.PropertyField(extrusionMethod, EditorUtils.CommonStyles.ExtrusionMethod);
            EditorGUILayout.PropertyField(scaling, EditorUtils.CommonStyles.Scaling);
            switch ((Scaling) scaling.intValue)
            {
                case Scaling.ConstantScreenSize:
                    EditorGUILayout.PropertyField(width, EditorUtils.CommonStyles.OutlineWidth);
                    break;
                case Scaling.ScaleWithDistance:
                    EditorGUILayout.PropertyField(width, EditorUtils.CommonStyles.OutlineWidth);
                    EditorGUILayout.PropertyField(minWidth, EditorUtils.CommonStyles.MinWidth);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            EditorGUILayout.Space();

            serializedObject.ApplyModifiedProperties();
        }
    }
}