using Linework.Common.Attributes;
using System;
using UnityEditor;
using UnityEngine;

namespace Linework.EdgeDetection
{
    [CreateAssetMenu(fileName = "Edge Detection Settings", menuName = "Linework/Edge Detection Settings")]
    [Icon("Packages/dev.ameye.linework/Editor/Icons/d_EdgeDetection.png")]
    public class EdgeDetectionSettings : ScriptableObject
    {
        internal Action OnSettingsChanged;
        
        [SerializeField] private InjectionPoint injectionPoint = InjectionPoint.AfterRenderingPostProcessing;
        [SerializeField] private bool showInSceneView = true;
        [SerializeField] private DebugView debugView;
       
        // Discontinuity.
        public DiscontinuityInput discontinuityInput = DiscontinuityInput.Depth | DiscontinuityInput.Normals | DiscontinuityInput.Luminance | DiscontinuityInput.SectionMap;
        [Range(0.0f, 1.0f)] public float depthSensitivity = 1.0f;
        [Range(0.0f, 1.0f)] public float depthDistanceModulation = 0.4f;
        [Range(0.0f, 1.0f)] public float grazingAngleMaskPower = 0.2f;
        [Range(1.0f, 30.0f)] public float grazingAngleMaskHardness = 1.0f;
        [Range(0.0f, 1.0f)] public float normalSensitivity = 0.4f;
        [Range(0.0f, 1.0f)] public float luminanceSensitivity = 0.3f;
#if UNITY_6000_0_OR_NEWER
        public RenderingLayerMask SectionRenderingLayer = RenderingLayerMask.defaultRenderingLayerMask;
#else
        [RenderingLayerMask]
        public uint SectionRenderingLayer = 1;
#endif
        public bool objectId = true;
        public SectionMapInput sectionMapInput = SectionMapInput.None;
        public Texture2D sectionTexture;
        public UVSet sectionTextureUvSet;
        public Channel sectionTextureChannel;
        public Channel vertexColorChannel;

        // Outline.
        public Kernel kernel = Kernel.RobertsCross;
        [Range(0.0f, 10.0f)] public float outlineWidth = 3.0f;
        [ColorUsage(true, true)] public Color backgroundColor = Color.clear;
        [ColorUsage(true, true)] public Color outlineColor = Color.black;
        public bool overrideColorInShadow;
        [ColorUsage(true, true)] public Color outlineColorShadow = Color.white;
        [ColorUsage(true, true)] public Color fillColor = Color.black;
        public BlendingMode blendMode;
        
        public InjectionPoint InjectionPoint => injectionPoint;
        public bool ShowInSceneView => showInSceneView;
        public DebugView DebugView => debugView;
        
        public bool showDiscontinuitySection;
        public bool showOutlineSection;
        public bool showExperimentalSection;

        private void OnValidate()
        {
#if UNITY_EDITOR
            if (Application.isPlaying)
                return;
            OnSettingsChanged?.Invoke();
#endif
        }

        private void OnDestroy()
        {
            OnSettingsChanged = null;
        }
        
#if UNITY_EDITOR
        private class OnDestroyProcessor: AssetModificationProcessor
        {
            private static readonly Type Type = typeof(EdgeDetectionSettings);
            private const string FileEnding = ".asset";

            public static AssetDeleteResult OnWillDeleteAsset(string path, RemoveAssetOptions _)
            {
                if (!path.EndsWith(FileEnding))
                    return AssetDeleteResult.DidNotDelete;

                var assetType = AssetDatabase.GetMainAssetTypeAtPath(path);
                if (assetType == null || assetType != Type && !assetType.IsSubclassOf(Type)) return AssetDeleteResult.DidNotDelete;
                var asset = AssetDatabase.LoadAssetAtPath<EdgeDetectionSettings>(path);
                asset.OnDestroy();

                return AssetDeleteResult.DidNotDelete;
            }
        }
#endif
    }
}