using System;
using Linework.EdgeDetection;
using Linework.Editor.Utils;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;

namespace Linework.Editor.EdgeDetection
{
    [CustomEditor(typeof(EdgeDetectionSettings))]
    public class EdgeDetectionSettingsEditor : UnityEditor.Editor
    {
        private SerializedProperty injectionPoint;
        private SerializedProperty showInSceneView;
        private SerializedProperty debugView;

        // Discontinuity.
        private SerializedProperty discontinuityInput;
        private SerializedProperty depthSensitivity;
        private SerializedProperty depthDistanceModulation;
        private SerializedProperty grazingAngleMaskPower;
        private SerializedProperty grazingAngleMaskHardness;
        private SerializedProperty normalSensitivity;
        private SerializedProperty luminanceSensitivity;
        private SerializedProperty sectionRenderingLayer;
        private SerializedProperty objectId;
        private SerializedProperty sectionMapInput;
        private SerializedProperty sectionTexture;
        private SerializedProperty sectionTextureUvSet;
        private SerializedProperty vertexColorChannel;
        private SerializedProperty sectionTextureChannel;

        // Outline.
        private SerializedProperty kernel;
        private SerializedProperty outlineWidth;
        private SerializedProperty backgroundColor;
        private SerializedProperty outlineColor;
        private SerializedProperty overrideColorInShadow;
        private SerializedProperty outlineColorShadow;
        private SerializedProperty fillColor;
        private SerializedProperty blendMode;

        private SerializedProperty showDiscontinuitySection, showOutlineSection;

        private void OnEnable()
        {
            showDiscontinuitySection = serializedObject.FindProperty(nameof(EdgeDetectionSettings.showDiscontinuitySection));
            showOutlineSection = serializedObject.FindProperty(nameof(EdgeDetectionSettings.showOutlineSection));
      
            injectionPoint = serializedObject.FindProperty("injectionPoint");
            showInSceneView = serializedObject.FindProperty("showInSceneView");
            debugView = serializedObject.FindProperty("debugView");

            // Discontinuity.
            discontinuityInput = serializedObject.FindProperty(nameof(EdgeDetectionSettings.discontinuityInput));
            depthSensitivity = serializedObject.FindProperty(nameof(EdgeDetectionSettings.depthSensitivity));
            depthDistanceModulation = serializedObject.FindProperty(nameof(EdgeDetectionSettings.depthDistanceModulation));
            grazingAngleMaskPower = serializedObject.FindProperty(nameof(EdgeDetectionSettings.grazingAngleMaskPower));
            grazingAngleMaskHardness = serializedObject.FindProperty(nameof(EdgeDetectionSettings.grazingAngleMaskHardness));
            normalSensitivity = serializedObject.FindProperty(nameof(EdgeDetectionSettings.normalSensitivity));
            luminanceSensitivity = serializedObject.FindProperty(nameof(EdgeDetectionSettings.luminanceSensitivity));
            sectionRenderingLayer = serializedObject.FindProperty(nameof(EdgeDetectionSettings.SectionRenderingLayer));
            objectId = serializedObject.FindProperty(nameof(EdgeDetectionSettings.objectId));
            sectionMapInput = serializedObject.FindProperty(nameof(EdgeDetectionSettings.sectionMapInput));
            sectionTexture = serializedObject.FindProperty(nameof(EdgeDetectionSettings.sectionTexture));
            sectionTextureUvSet = serializedObject.FindProperty(nameof(EdgeDetectionSettings.sectionTextureUvSet));
            sectionTextureChannel = serializedObject.FindProperty(nameof(EdgeDetectionSettings.sectionTextureChannel));
            vertexColorChannel = serializedObject.FindProperty(nameof(EdgeDetectionSettings.vertexColorChannel));

            // Outline.
            kernel = serializedObject.FindProperty(nameof(EdgeDetectionSettings.kernel));
            outlineWidth = serializedObject.FindProperty(nameof(EdgeDetectionSettings.outlineWidth));
            backgroundColor = serializedObject.FindProperty(nameof(EdgeDetectionSettings.backgroundColor));
            outlineColor = serializedObject.FindProperty(nameof(EdgeDetectionSettings.outlineColor));
            overrideColorInShadow = serializedObject.FindProperty(nameof(EdgeDetectionSettings.overrideColorInShadow));
            outlineColorShadow = serializedObject.FindProperty(nameof(EdgeDetectionSettings.outlineColorShadow));
            fillColor = serializedObject.FindProperty(nameof(EdgeDetectionSettings.fillColor));
            blendMode = serializedObject.FindProperty(nameof(EdgeDetectionSettings.blendMode));
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("Edge Detection", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(injectionPoint, EditorUtils.CommonStyles.InjectionPoint);
            EditorGUILayout.PropertyField(showInSceneView, EditorUtils.CommonStyles.ShowInSceneView);
            EditorGUILayout.PropertyField(debugView, EditorUtils.CommonStyles.DebugStage);
            EditorGUILayout.Space();
            CoreEditorUtils.DrawSplitter();
            serializedObject.ApplyModifiedProperties();

            // Edge detection.
            EditorUtils.SectionGUI("Discontinuity", showDiscontinuitySection, () =>
            {
                var discontinuityInputValue = (DiscontinuityInput) discontinuityInput.intValue;
                discontinuityInputValue = (DiscontinuityInput) EditorGUILayout.EnumFlagsField(EditorUtils.CommonStyles.DiscontinuityInput, discontinuityInputValue);
                discontinuityInput.intValue = (int) discontinuityInputValue;
                EditorGUILayout.Space();

                using (new EditorGUI.DisabledScope(!discontinuityInputValue.HasFlag(DiscontinuityInput.Depth)))
                {
                    EditorGUILayout.LabelField("Depth", EditorStyles.boldLabel);
                    EditorGUILayout.PropertyField(depthSensitivity, EditorUtils.CommonStyles.Sensitivity);
                    EditorGUILayout.PropertyField(depthDistanceModulation, EditorUtils.CommonStyles.DepthDistanceModulation);
                    EditorGUILayout.PropertyField(grazingAngleMaskPower, EditorUtils.CommonStyles.GrazingAngleMaskPower);
                    EditorGUILayout.PropertyField(grazingAngleMaskHardness, EditorUtils.CommonStyles.GrazingAngleMaskHardness);
                }
                EditorGUILayout.Space();

                using (new EditorGUI.DisabledScope(!discontinuityInputValue.HasFlag(DiscontinuityInput.Normals)))
                {
                    EditorGUILayout.LabelField("Normals", EditorStyles.boldLabel);
                    EditorGUILayout.PropertyField(normalSensitivity, EditorUtils.CommonStyles.Sensitivity);
                }
                EditorGUILayout.Space();

                using (new EditorGUI.DisabledScope(!discontinuityInputValue.HasFlag(DiscontinuityInput.Luminance)))
                {
                    EditorGUILayout.LabelField("Luminance", EditorStyles.boldLabel);
                    EditorGUILayout.PropertyField(luminanceSensitivity, EditorUtils.CommonStyles.Sensitivity);
                }
                EditorGUILayout.Space();

                using (new EditorGUI.DisabledScope(!discontinuityInputValue.HasFlag(DiscontinuityInput.SectionMap)))
                {
                    EditorGUILayout.LabelField("Section Map", EditorStyles.boldLabel);
                    EditorGUILayout.PropertyField(sectionRenderingLayer, EditorUtils.CommonStyles.SectionLayer);
                    EditorGUILayout.PropertyField(sectionMapInput, EditorUtils.CommonStyles.SectionMapInput);
                    EditorGUI.indentLevel++;

                    if ((SectionMapInput) sectionMapInput.intValue == SectionMapInput.None)
                    {
                        EditorGUILayout.PropertyField(objectId, EditorUtils.CommonStyles.ObjectId);
                    }

                    if ((SectionMapInput) sectionMapInput.intValue == SectionMapInput.VertexColors)
                    {
                        EditorGUILayout.PropertyField(objectId, EditorUtils.CommonStyles.ObjectId);
                        EditorGUILayout.PropertyField(vertexColorChannel, EditorUtils.CommonStyles.VertexColorChannel);
                    }

                    if ((SectionMapInput) sectionMapInput.intValue == SectionMapInput.SectionTexture)
                    {
                        EditorGUILayout.PropertyField(objectId, EditorUtils.CommonStyles.ObjectId);
                        EditorGUILayout.PropertyField(sectionTexture, EditorUtils.CommonStyles.SectionTexture);
                        EditorGUILayout.PropertyField(sectionTextureUvSet, EditorUtils.CommonStyles.SectionTextureUVSet);
                        EditorGUILayout.PropertyField(vertexColorChannel, EditorUtils.CommonStyles.SectionTextureChannel);
                    }
                    EditorGUI.indentLevel--;

                    EditorGUILayout.Space();
                    switch ((SectionMapInput) sectionMapInput.intValue)
                    {
                        case SectionMapInput.VertexColors:
                            var vertexColorsMessage = objectId.boolValue
                                ? $"Sections: Object ID + {vertexColorChannel.enumDisplayNames[vertexColorChannel.intValue]} channel of vertex colors"
                                : $"Sections: {vertexColorChannel.enumDisplayNames[vertexColorChannel.intValue]} channel of vertex colors";
                            EditorGUILayout.HelpBox(vertexColorsMessage, MessageType.Info);
                            break;
                        case SectionMapInput.SectionTexture:
                            var sectionTextureMessage = objectId.boolValue
                                ? $"Sections: Object ID + {sectionTextureChannel.enumDisplayNames[sectionTextureChannel.intValue]} channel of section texture using {sectionTextureUvSet.enumDisplayNames[sectionTextureUvSet.intValue]}"
                                : $"Sections: {sectionTextureChannel.enumDisplayNames[sectionTextureChannel.intValue]} channel of section texture using {sectionTextureUvSet.enumDisplayNames[sectionTextureUvSet.intValue]}";
                            EditorGUILayout.HelpBox(sectionTextureMessage, MessageType.Info);
                            break;
                        case SectionMapInput.Custom:
                            const string keywordMessage = "Sections: Custom. Use the _SECTION_PASS keyword to render directly to the section map.";
                            EditorGUILayout.HelpBox(keywordMessage, MessageType.Info);
                            break;
                        case SectionMapInput.None:
                            var nonMessage = objectId.boolValue ? "Sections: Object ID" : "Sections: Nothing";
                            EditorGUILayout.HelpBox(nonMessage, MessageType.Info);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }

                EditorGUILayout.Space();
            }, serializedObject);

            // Line appearance.
            EditorUtils.SectionGUI("Outline", showOutlineSection, () =>
            {
                EditorGUILayout.PropertyField(kernel, EditorUtils.CommonStyles.Kernel);
                EditorGUILayout.PropertyField(outlineWidth, EditorUtils.CommonStyles.OutlineWidth);
                EditorGUILayout.Space();

                EditorGUILayout.LabelField("Outline", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(backgroundColor, EditorUtils.CommonStyles.BackgroundColor);
                EditorGUILayout.PropertyField(outlineColor, EditorUtils.CommonStyles.EdgeColor);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(overrideColorInShadow, EditorUtils.CommonStyles.OverrideShadow);
                if (overrideColorInShadow.boolValue) EditorGUILayout.PropertyField(outlineColorShadow, GUIContent.none);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.PropertyField(fillColor, EditorUtils.CommonStyles.OutlineFillColor);
                EditorGUILayout.PropertyField(blendMode, EditorUtils.CommonStyles.OutlineBlendMode);
                EditorGUILayout.Space();
            }, serializedObject);
            
            serializedObject.ApplyModifiedProperties();
        }
    }
}