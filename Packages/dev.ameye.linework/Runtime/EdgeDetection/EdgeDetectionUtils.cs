using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Linework.EdgeDetection
{
    [Serializable]
    public sealed class ShaderResources
    {
        public Shader section;
        public Shader outline;

        public ShaderResources Load()
        {
            section = Shader.Find(ShaderPath.Section);
            outline = Shader.Find(ShaderPath.Outline);
            return this;
        }
    }
    
    static class ShaderPath
    {
        public const string Outline = "Hidden/Outlines/Edge Detection/Outline";
        public const string Section = "Hidden/Outlines/Edge Detection/Section";
    }

    static class Keyword
    {
        public static readonly GlobalKeyword ScreenSpaceOcclusion = GlobalKeyword.Create("_SCREEN_SPACE_OCCLUSION");
        public static readonly GlobalKeyword SectionPass = GlobalKeyword.Create("_SECTION_PASS");
    }

    static class ShaderPassName
    {
        public const string Section = "Section (Edge Detection)";
        public const string Outline = "Outline (Edge Detection)";
    }
    
    static class ShaderPropertyId
    {
        // Line appearance.
        public static readonly int BackgroundColor = Shader.PropertyToID("_BackgroundColor");
        public static readonly int OutlineColor = Shader.PropertyToID("_OutlineColor");
        public static readonly int OutlineColorShadow = Shader.PropertyToID("_OutlineColorShadow");
        public static readonly int FillColor = Shader.PropertyToID("_FillColor");
        public static readonly int OutlineWidth = Shader.PropertyToID("_OutlineWidth");
        
        // Edge detection.
        public static readonly int DepthSensitivity = Shader.PropertyToID("_DepthSensitivity");
        public static readonly int DepthDistanceModulation = Shader.PropertyToID("_DepthDistanceModulation");
        public static readonly int GrazingAngleMaskPower = Shader.PropertyToID("_GrazingAngleMaskPower");
        public static readonly int GrazingAngleMaskHardness = Shader.PropertyToID("_GrazingAngleMaskHardness");
        public static readonly int NormalSensitivity = Shader.PropertyToID("_NormalSensitivity");
        public static readonly int LuminanceSensitivity = Shader.PropertyToID("_LuminanceSensitivity");
        public static readonly int CameraSectioningTexture = Shader.PropertyToID("_CameraSectioningTexture");
  
        // Section map.
        public static readonly int SectionTexture = Shader.PropertyToID("_SectionTexture");
    }

    static class Buffer
    {
        public const string Section = "_SectionBuffer";
    }
    
    [Flags]
    public enum DiscontinuityInput
    {
        None = 0,
        Depth = 1 << 0,
        Normals = 1 << 1,
        Luminance = 1 << 2,
        SectionMap = 1 << 3,
        All = ~0,
    }
    
    public enum DebugView
    {
        None,
        [InspectorName("Depth")]
        Depth,
        [InspectorName("Normals")]
        Normals,
        [InspectorName("Luminance")]
        Luminance,
        [InspectorName("Section Map (Perceptual)")]
        SectionMapPerceptual,
        [InspectorName("Section Map (Raw Values)")]
        SectionMapRaw,
        [InspectorName("Lines Only")]
        LinesOnly
    }
    
    static class ShaderFeature
    {
        public const string DepthDiscontinuity = "DEPTH";
        public const string NormalDiscontinuity = "NORMALS";
        public const string LuminanceDiscontinuity = "LUMINANCE";
        public const string SectionDiscontinuity = "SECTIONS";

        public const string TextureUV0 = "TEXTURE_UV_SET_UV0";
        public const string TextureUV1 = "TEXTURE_UV_SET_UV1";
        public const string TextureUV2 = "TEXTURE_UV_SET_UV2";
        public const string TextureUV3 = "TEXTURE_UV_SET_UV3";
        
        public const string VertexColorChannelR = "VERTEX_COLOR_CHANNEL_R";
        public const string VertexColorChannelG = "VERTEX_COLOR_CHANNEL_G";
        public const string VertexColorChannelB = "VERTEX_COLOR_CHANNEL_B";
        public const string VertexColorChannelA = "VERTEX_COLOR_CHANNEL_A";
        
        public const string TextureChannelR = "TEXTURE_CHANNEL_R";
        public const string TextureChannelG = "TEXTURE_CHANNEL_G";
        public const string TextureChannelB = "TEXTURE_CHANNEL_B";
        public const string TextureChannelA = "TEXTURE_CHANNEL_A";

        public const string OperatorCross = "OPERATOR_CROSS";
        public const string OperatorSobel = "OPERATOR_SOBEL";

        public const string DebugDepth = "DEBUG_DEPTH";
        public const string DebugNormals = "DEBUG_NORMALS";
        public const string DebugLuminance = "DEBUG_LUMINANCE";
        public const string DebugSectionMapPerceptual = "DEBUG_SECTIONS";
        public const string DebugSectionMapRaw = "DEBUG_SECTIONS_RAW";
        public const string DebugLines = "DEBUG_LINES";
        public const string OverrideShadow = "OVERRIDE_SHADOW";

        public const string ObjectId = "OBJECT_ID";
        public const string InputVertexColor = "INPUT_VERTEX_COLOR";
        public const string InputTexture = "INPUT_TEXTURE";
    }
    
    public enum SectionMapInput
    {
        [InspectorName("None")]
        None,
        [InspectorName("Vertex Colors")]
        VertexColors,
        [InspectorName("Section Texture")]
        SectionTexture,
        [InspectorName("Custom")]
        Custom
    }
    
    public enum Kernel
    {
        RobertsCross,
        Sobel
    }
    
    public enum UVSet
    {
        UV0,
        UV1,
        UV2,
        UV3
    }
}