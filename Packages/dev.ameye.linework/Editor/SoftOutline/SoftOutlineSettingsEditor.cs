using System;
using Linework.Editor.Utils;
using Linework.SoftOutline;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;

namespace Linework.Editor.SoftOutline
{
    [CustomEditor(typeof(SoftOutlineSettings))]
    public class SoftOutlineSettingsEditor : UnityEditor.Editor
    {
        private SerializedProperty injectionPoint;
        private SerializedProperty showInSceneView;
        private SerializedProperty clearStencil;
        private SerializedProperty debugStage;

        private SerializedProperty type;
        private SerializedProperty hardness;
        private SerializedProperty color;
        private SerializedProperty intensity;
        private SerializedProperty blendMode;
        private SerializedProperty dilationMethod;
        private SerializedProperty kernelSize;
        private SerializedProperty blurSpread;
        private SerializedProperty blurPasses;
        private SerializedProperty occlusion;
        private SerializedProperty scaleWithResolution;
        private SerializedProperty referenceResolution;
        private SerializedProperty customReferenceResolution;

        private SerializedProperty outlines;
        private EditorList<Outline> outlineList;

        private void OnEnable()
        {
            injectionPoint = serializedObject.FindProperty("injectionPoint");
            showInSceneView = serializedObject.FindProperty("showInSceneView");
            clearStencil = serializedObject.FindProperty("clearStencil");
            debugStage = serializedObject.FindProperty("debugStage");
            
            type = serializedObject.FindProperty(nameof(SoftOutlineSettings.type));
            hardness = serializedObject.FindProperty(nameof(SoftOutlineSettings.hardness));
            color = serializedObject.FindProperty(nameof(SoftOutlineSettings.color));
            intensity = serializedObject.FindProperty(nameof(SoftOutlineSettings.intensity));
            blendMode = serializedObject.FindProperty(nameof(SoftOutlineSettings.blendMode));
            dilationMethod = serializedObject.FindProperty(nameof(SoftOutlineSettings.dilationMethod));
            kernelSize = serializedObject.FindProperty(nameof(SoftOutlineSettings.kernelSize));
            blurSpread = serializedObject.FindProperty(nameof(SoftOutlineSettings.blurSpread));
            blurPasses = serializedObject.FindProperty(nameof(SoftOutlineSettings.blurPasses));
            scaleWithResolution = serializedObject.FindProperty(nameof(SoftOutlineSettings.scaleWithResolution));
            referenceResolution = serializedObject.FindProperty(nameof(SoftOutlineSettings.referenceResolution));
            customReferenceResolution = serializedObject.FindProperty(nameof(SoftOutlineSettings.customResolution));
            
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
            
            var typeChanged = false;

            EditorGUILayout.LabelField("Soft Outline", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(injectionPoint, EditorUtils.CommonStyles.InjectionPoint);
            EditorGUILayout.PropertyField(showInSceneView, EditorUtils.CommonStyles.ShowInSceneView);
#if UNITY_6000_0_OR_NEWER
            EditorGUILayout.PropertyField(clearStencil, EditorUtils.CommonStyles.ClearStencil);
            EditorGUILayout.PropertyField(debugStage, EditorUtils.CommonStyles.DebugStage);
#endif            
            EditorGUILayout.Space();
            CoreEditorUtils.DrawSplitter();
            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.LabelField(EditorUtils.CommonStyles.Outlines, EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(blendMode, EditorUtils.CommonStyles.OutlineBlendMode);
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(type, EditorUtils.CommonStyles.Type);
            typeChanged |= EditorGUI.EndChangeCheck();
            switch ((OutlineType) type.intValue)
            {
                case OutlineType.Hard:
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(hardness, EditorUtils.CommonStyles.Hardness);
                    EditorGUILayout.PropertyField(color, EditorUtils.CommonStyles.OutlineColor);
                    EditorGUI.indentLevel--;
                    break;
                case OutlineType.Soft:
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(intensity, EditorUtils.CommonStyles.Intensity);
                    EditorGUI.indentLevel--;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            EditorGUILayout.PropertyField(dilationMethod, EditorUtils.CommonStyles.DilationMethod);
            EditorGUI.indentLevel++;
            switch ((DilationMethod) dilationMethod.intValue)
            {
                case DilationMethod.Box:
                    EditorGUILayout.PropertyField(kernelSize, EditorUtils.CommonStyles.KernelSize);
                    break;
                case DilationMethod.Gaussian:
                    EditorGUILayout.PropertyField(kernelSize, EditorUtils.CommonStyles.KernelSize);
                    EditorGUILayout.PropertyField(blurSpread, EditorUtils.CommonStyles.Spread);
                    break;
                case DilationMethod.Kawase:
                    EditorGUILayout.PropertyField(blurPasses, EditorUtils.CommonStyles.Passes);
                    break;
                case DilationMethod.Dilate:
                    EditorGUILayout.PropertyField(kernelSize, EditorUtils.CommonStyles.KernelSize);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(scaleWithResolution, EditorUtils.CommonStyles.ScaleWithResolution);
            if (scaleWithResolution.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(referenceResolution,  GUIContent.none);
                if ((Resolution) referenceResolution.intValue == Resolution.Custom) EditorGUILayout.PropertyField(customReferenceResolution, GUIContent.none, GUILayout.Width(100));
                EditorGUILayout.EndHorizontal();
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndHorizontal();
            EditorGUI.indentLevel--;
            
         
            if ((OutlineType) type.intValue == OutlineType.Hard)
            {
                EditorGUILayout.Space();
                EditorGUILayout.HelpBox("When using a 'Hard' outline, the color is shared between all outlines.", MessageType.Info);
            }

            serializedObject.ApplyModifiedProperties();
            EditorGUILayout.Space();
            outlineList.Draw();
            
            if (typeChanged)
            {
                ForceSave();
            }
            
            serializedObject.ApplyModifiedProperties();
        }

        private void ForceSave()
        {
            ((SoftOutlineSettings) target).Changed();
            EditorUtility.SetDirty(target);
        }
    }
}