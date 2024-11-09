using Linework.Common.Attributes;
using UnityEngine;

namespace Linework.FastOutline
{
    public class Outline : ScriptableObject
    {
        [SerializeField, HideInInspector] public Material material;
        [SerializeField, HideInInspector] private bool isActive = true;
        
#if UNITY_6000_0_OR_NEWER
        public RenderingLayerMask RenderingLayer = RenderingLayerMask.defaultRenderingLayerMask;
#else
        [RenderingLayerMask]
        public uint RenderingLayer = 1;
#endif
        public Occlusion occlusion = Occlusion.WhenNotOccluded;
        public MaskingStrategy maskingStrategy = MaskingStrategy.Stencil;
        [ColorUsage(true, true)] public Color color = Color.green;
        public bool enableOcclusion = false;
        [ColorUsage(true, true)] public Color occludedColor = Color.red;
        public BlendingMode blendMode = BlendingMode.Alpha;
        public ExtrusionMethod extrusionMethod = ExtrusionMethod.ClipSpaceNormalVector;
        public Scaling scaling;
        [Range(0.0f, 100.0f)] public float width = 20.0f;
        [Range(0.0f, 100.0f)] public float minWidth = 0.0f;

        private void OnEnable()
        {
            EnsureMaterialInitialized();
        }
        
        private void EnsureMaterialInitialized()
        {
            if (material == null)
            {
                var shader = Shader.Find(ShaderPath.Outline);
                if (shader != null)
                {
                    material = new Material(shader)
                    {
                        hideFlags = HideFlags.HideAndDontSave
                    };
                }
            }
        }
        
        public void AssignMaterial(Material copyFrom)
        {
            EnsureMaterialInitialized();
            material.CopyPropertiesFromMaterial(copyFrom);
        }
        
        public bool IsActive()
        {
            return isActive;
        }

        public void SetActive(bool active)
        {
            isActive = active;
        }
        
        public void Cleanup()
        {
            if (material != null)
            {
                DestroyImmediate(material);
                material = null;
            }
        }
    }
}