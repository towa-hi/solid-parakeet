using Linework.Common.Attributes;
using UnityEngine;

namespace Linework.WideOutline
{
    public class Outline : ScriptableObject
    {
        [SerializeField, HideInInspector] public Material material;
        [SerializeField, HideInInspector] private bool isActive = true;
        [SerializeField, HideInInspector] private bool customDepthEnabled = true;

#if UNITY_6000_0_OR_NEWER
        public RenderingLayerMask RenderingLayer = RenderingLayerMask.defaultRenderingLayerMask;
#else
        [RenderingLayerMask]
        public uint RenderingLayer = 1;
#endif
        public WideOutlineOcclusion occlusion = WideOutlineOcclusion.Always;
        public bool closedLoop;
        public bool alphaCutout;
        public Texture2D alphaCutoutTexture;
        [Range(0.0f, 1.0f)] public float alphaCutoutThreshold = 0.5f;
        
        [ColorUsage(true, true)] public Color color = Color.green;
        
        private void OnEnable()
        {
            EnsureMaterialInitialized();
        }

        private void EnsureMaterialInitialized()
        {
            if (material == null)
            {
                var shader = Shader.Find(ShaderPath.Silhouette);
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

        public void SetAdvancedOcclusionEnabled(bool enable)
        {
            customDepthEnabled = enable;
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