Shader "Hidden/Outlines/Edge Detection/Outline"
{
    Properties
    {
        // Line appearance.
        _BackgroundColor ("Background Color", Color) = (0, 0, 0, 0)
        _OutlineColor ("Outline Color", Color) = (0, 0, 0, 1)
        _FillColor ("Fill Color", Color) = (0, 0, 0, 1)
        [Toggle(OVERRIDE_SHADOW)] _OverrideShadow ("Override Outline Color In Shadow", Float) = 0
        _OutlineColorShadow ("Outline Color Shadow", Color) = (1, 1, 1, 1)
        _OutlineWidth ("Outline Width", Range(0, 10)) = 1

        // Edge detection.
        [KeywordEnum(Cross, Sobel)] _Operator("Edge Detection Operator", Float) = 0

        // Edge Detection (depth).
        _DepthSensitivity ("Depth Sensitivity", Range(0, 50)) = 0
        _DepthDistanceModulation ("Depth Non-Linearity Factor", Range(0, 3)) = 1
        _GrazingAngleMaskPower ("Grazing Angle Mask Power", Range(0, 1)) = 1
        _GrazingAngleMaskHardness("Grazing Angle Mask Hardness", Range(0,1)) = 1

        // Edge Detection (normals).
        _NormalSensitivity ("Normals Sensitivity", Range(0, 50)) = 0

        // Edge Detection (luminance).
        _LuminanceSensitivity ("Luminance Sensitivity", Range(0, 50)) = 0

        _SrcBlend ("_SrcBlend", Int) = 0
        _DstBlend ("_DstBlend", Int) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType"="Opaque"
        }

        ZWrite Off
        Cull Off

        HLSLINCLUDE
        #pragma multi_compile_local _ DEPTH
        #pragma multi_compile_local _ NORMALS
        #pragma multi_compile_local _ LUMINANCE
        #pragma multi_compile_local _ SECTIONS

        #pragma multi_compile_local _ OVERRIDE_SHADOW
        #pragma multi_compile_local OPERATOR_CROSS OPERATOR_SOBEL

        #pragma multi_compile_local _ DEBUG_DEPTH DEBUG_NORMALS DEBUG_LUMINANCE DEBUG_SECTIONS DEBUG_SECTIONS_RAW DEBUG_LINES
        ENDHLSL

        Pass // 0: EDGE DETECTION OUTLINE
        {
            Name "EDGE DETECTION OUTLINE"

            Blend [_SrcBlend] [_DstBlend]

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            #if UNITY_VERSION < 202300
            CBUFFER_START(UnityPerMaterial)
            float4 _BlitTexture_TexelSize;
            CBUFFER_END
            #endif
            
            #if defined(DEPTH) || defined(OVERRIDE_SHADOW) || defined(DEBUG_DEPTH)
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #endif

            #if defined(DEPTH) || defined(NORMALS) || defined(DEBUG_NORMALS)
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"
            #endif

            #if defined(LUMINANCE) || defined(DEBUG_LUMINANCE)
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            #endif

            #if defined(SECTIONS) || defined(DEBUG_SECTIONS_RAW) || defined(DEBUG_SECTIONS)
            #include "Packages/dev.ameye.linework/Runtime/EdgeDetection/Shaders/DeclareSectioningTexture.hlsl"
            #endif

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            float4 _BackgroundColor, _OutlineColor, _FillColor, _OutlineColorShadow;
            float _OverrideOutlineColorShadow;
            float _OutlineWidth;
            float _DepthSensitivity, _DepthDistanceModulation, _GrazingAngleMaskPower, _GrazingAngleMaskHardness;
            float _NormalSensitivity;
            float _LuminanceSensitivity;

            #pragma vertex Vert
            #pragma fragment frag

            float RobertsCross(float3 samples[4])
            {
                const float3 difference_1 = samples[1] - samples[2];
                const float3 difference_2 = samples[0] - samples[3];
                return sqrt(dot(difference_1, difference_1) + dot(difference_2, difference_2));
            }

            float RobertsCross(float samples[4])
            {
                const float difference_1 = samples[1] - samples[2];
                const float difference_2 = samples[0] - samples[3];
                return sqrt(difference_1 * difference_1 + difference_2 * difference_2);
            }

            float Sobel(float3 samples[9])
            {
                const float3 difference_1 = samples[0] - samples[2] + 2 * samples[3] - 2 * samples[5] + samples[6] - samples[8];
                const float3 difference_2 = samples[0] - samples[6] + 2 * samples[1] - 2 * samples[7] + samples[2] - samples[8];
                return sqrt(dot(difference_1, difference_1) + dot(difference_2, difference_2));
            }

            float Sobel(float samples[9])
            {
                const float difference_1 = samples[0] - samples[2] + 2 * samples[3] - 2 * samples[5] + samples[6] - samples[8];
                const float difference_2 = samples[0] - samples[6] + 2 * samples[1] - 2 * samples[7] + samples[2] - samples[8];
                return sqrt(difference_1 * difference_1 + difference_2 * difference_2);
            }

            #if defined(NORMALS)
            float3 SampleSceneNormalsRemapped(float2 uv)
            {
                return SampleSceneNormals(uv) * 0.5 + 0.5;
            }
            #endif

            #if defined(DEBUG_LUMINANCE) || defined(LUMINANCE)
            float SampleSceneLuminance(float2 uv)
            {
                float3 color = SampleSceneColor(uv);
                return color.r * 0.3 + color.g * 0.59 + color.b * 0.11;
            }
            #endif

            half3 HSVToRGB(half3 In)
            {
                half4 K = half4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
                half3 P = abs(frac(In.xxx + K.xyz) * 6.0 - K.www);
                return In.z * lerp(K.xxx, saturate(P - K.xxx), In.y);
            }

            half4 frag(Varyings IN) : SV_TARGET
            {
                // Get screen-space UV coordinate.
                float2 uv = IN.texcoord;

                ///
                /// DISCONTINUITY SOURCES
                ///

                #if defined(DEPTH) || defined(OVERRIDE_SHADOW) || defined(DEBUG_DEPTH)
                float depth = SampleSceneDepth(uv); // Sample scene depth.
                #if !UNITY_REVERSED_Z // Transform depth from [0, 1] to [-1, 1] on OpenGL.
                depth = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, depth); // Alternatively: depth = 1.0 - depth
                #endif
                float3 positionWS = ComputeWorldSpacePosition(uv, depth, UNITY_MATRIX_I_VP); // Calculate world position from depth.
                #endif

                #if defined(DEBUG_NORMALS)
                float3 normals = SampleSceneNormals(uv); // Sample scene normals.
                #endif

                #if defined(DEBUG_LUMINANCE)
                float luminance = SampleSceneLuminance(uv); // Sample scene luminance.
                #endif

                #if defined(SECTIONS) || defined(DEBUG_SECTIONS_RAW) || defined(DEBUG_SECTIONS)
                bool mask = false;
                bool fill = false;
                float section = SampleSceneSection(uv).r; // Sample scene section.
                if(section == 1.0) fill = true;
                if(section == 0.0) mask = true;
                #endif

                ///
                /// EDGE DETECTION
                ///

                float edgeDepth = 0;
                float edgeNormal = 0;
                float edgeLuminance = 0;
                float edgeSection = 0;

                float2 texel_size = float2(1.0 / _ScreenParams.x, 1.0 / _ScreenParams.y);
                
                #if defined(OPERATOR_CROSS)
                const float half_width_f = floor(_OutlineWidth * 0.5);
                const float half_width_c = ceil(_OutlineWidth * 0.5);

                // Generate samples.
                float2 uvs[4];
                uvs[0] = uv + texel_size * float2(half_width_f, half_width_c) * float2(-1, 1);  // top left
                uvs[1] = uv + texel_size * float2(half_width_c, half_width_c) * float2(1, 1);   // top right
                uvs[2] = uv + texel_size * float2(half_width_f, half_width_f) * float2(-1, -1); // bottom left
                uvs[3] = uv + texel_size * float2(half_width_c, half_width_f) * float2(1, -1);  // bottom right
                
                float3 normal_samples[4];
                float depth_samples[4], section_samples[4], luminance_samples[4];

                for (int i = 0; i < 4; i++) {
                #if defined(DEPTH)
                    depth_samples[i] = SampleSceneDepth(uvs[i]);
                #endif

                #if defined(NORMALS)
                    normal_samples[i] = SampleSceneNormalsRemapped(uvs[i]);
                #endif

                #if defined(LUMINANCE)
                    luminance_samples[i] = SampleSceneLuminance(uvs[i]);
                #endif

                #if defined(SECTIONS)
                    section_samples[i] = SampleSceneSection(uvs[i]).r;
                    if(section_samples[i] == 1) fill = true;
                    if(section_samples[i] == 0) mask = true;
                #endif
                }

                #if defined(DEPTH)
                edgeDepth = mask ? 0 : RobertsCross(depth_samples);
                #endif

                #if defined(NORMALS)
                edgeNormal = mask ? 0 : RobertsCross(normal_samples);
                #endif

                #if defined(LUMINANCE)
                edgeLuminance = mask ? 0 : RobertsCross(luminance_samples);
                #endif

                #if defined(SECTIONS)
                edgeSection = mask ? 0 : RobertsCross(section_samples);
                #endif

                #elif defined(OPERATOR_SOBEL)
                float scale = floor(_OutlineWidth);

                float2 uvs[9];
                uvs[0] = uv + texel_size * scale * float2(-1, 1); // top left
                uvs[1] = uv + texel_size * scale * float2(0, 1);  // top center
                uvs[2] = uv + texel_size * scale * float2(1, 1);  // top right
                uvs[3] = uv + texel_size * scale * float2(-1, 0); // middle left
                uvs[4] = uv + texel_size * scale * float2(0, 0);  // middle center
                uvs[5] = uv + texel_size * scale * float2(1, 0);  // middle right
                uvs[6] = uv + texel_size * scale * float2(-1, -1); // bottom left
                uvs[7] = uv + texel_size * scale * float2(0, -1);  // bottom center
                uvs[8] = uv + texel_size * scale * float2(1, -1);  // bottom right

                float3 normal_samples[9];
                float depth_samples[9], section_samples[9], luminance_samples[9];

                for (int i = 0; i < 9; i++) {
                #if defined(DEPTH)
                    depth_samples[i] = SampleSceneDepth(uvs[i]);
                #endif

                #if defined(NORMALS)
                    normal_samples[i] = SampleSceneNormalsRemapped(uvs[i]);
                #endif

                #if defined(LUMINANCE)
                    luminance_samples[i] = SampleSceneLuminance(uvs[i]);
                #endif

                #if defined(SECTIONS)
                    section_samples[i] = SampleSceneSection(uvs[i]).r;
                    if(section_samples[i] == 1) fill = true;
                    if(section_samples[i] == 0) mask = true;
                #endif
                }
                
                #if defined(DEPTH)
                edgeDepth = mask ? 0 : Sobel(depth_samples);
                #endif

                #if defined(NORMALS)
                edgeNormal = mask ? 0 : Sobel(normal_samples);
                #endif

                #if defined(LUMINANCE)
                edgeLuminance = mask ? 0 : Sobel(luminance_samples);
                #endif

                #if defined(SECTIONS)
                edgeSection = mask ? 0 : Sobel(section_samples);
                #endif

                #endif

                ///
                /// DISCONTINUITIY THRESHOLDING
                ///

                #if defined(DEPTH)
                float depth_threshold = 1 / _DepthSensitivity;

                // 1. The depth buffer is non-linear so two objects 1m apart close to camera will have much larger depth difference than two
                //    objects 1m apart far away from the camera. For this, we multiply the threshold by the depth buffer so that nearby objects
                //    will have to have a larger discontinuity in order to be detected as an 'edge'.
                depth_threshold = max(depth_threshold * 0.01, depth_threshold * _DepthDistanceModulation * SampleSceneDepth(uv));

                // 2. At small grazing angles, the depth difference will grow larger and so faces can be wrongly detected. For this, the depth threshold
                //    can be modulated by the grazing angle, given by the dot product between the normal vector and the view direction. If the normal vector
                //    and the view direction are almost perpendicular, the depth threshold should be increased.
                float3 viewWS = normalize(_WorldSpaceCameraPos.xyz - positionWS);
                float fresnel = pow(1.0 - dot(normalize(SampleSceneNormals(uv)), normalize(viewWS)), 1.0);
                float grazingAngleMask = saturate((fresnel + _GrazingAngleMaskPower - 1) / _GrazingAngleMaskPower); // a mask between 0 and 1
                depth_threshold = depth_threshold * (1 + _GrazingAngleMaskHardness * grazingAngleMask);
                
                edgeDepth = edgeDepth > depth_threshold ? 1 : 0;
                #endif

                #if defined(NORMALS)
                float normalThreshold = 1 / _NormalSensitivity;
                edgeNormal = edgeNormal > normalThreshold ? 1 : 0;
                #endif

                #if defined(LUMINANCE)
                float luminanceThreshold = 1 / _LuminanceSensitivity;
                edgeLuminance = edgeLuminance > luminanceThreshold ? 1 : 0;
                #endif

                #if defined(SECTIONS)
                edgeSection = edgeSection > 0 ? 1 : 0;
                #endif

                ///
                /// COMPOSITION
                ///

                float edge = max(edgeDepth, max(edgeNormal, max(edgeLuminance, edgeSection))); // Combine edges.

                #if defined(DEBUG_DEPTH) // Debug depth.
                return lerp(half4(depth, depth, depth, 1), half4(1,1,1,1), edgeDepth);
                #endif

                #if defined(DEBUG_NORMALS) // Debug normals.
                return lerp(half4(normals * 0.5 + 0.5, 1), half4(0,0,0,1), edgeNormal);
                #endif

                #if defined(DEBUG_LUMINANCE) // Debug luminance.
                return lerp(half4(luminance, luminance, luminance, 1), half4(1,0,0,1), edgeLuminance);
                #endif

                #if defined(DEBUG_SECTIONS_RAW) // Debug section map (raw values).
                if(mask) return half4(0,1,0,1);
                if(fill) return half4(0,0,1,1);
                return lerp(half4(section,0,0,1), half4(1,1,1,1), edgeSection);
                #endif

                #if defined(DEBUG_SECTIONS) // Debug section map (perceptual).
                if(mask) return half4(1,1,1,1);
                if(fill) return half4(0,0,0,1);
                
                half3 section_perceptual;
                if (section == 0.0) {
                    section_perceptual = half3(0.0, 0.0, 0.0);
                } else if (section == 1.0) {
                    section_perceptual = half3(1.0, 1.0, 1.0);
                } else {
                    section_perceptual = HSVToRGB(half3(section * 360.0, 0.5, 1.0));
                }
                return lerp(float4(section_perceptual.x, section_perceptual.y, section_perceptual.z, 1.0), half4(0,0,0,1), edgeSection);
                #endif

                #if defined(DEBUG_LINES) // Debug lines.
                return lerp(half4(1,1,1,1), half4(0,0,0,1), edge);
                #endif

                #if defined(SECTIONS) && (defined(DEBUG_LINES) || defined(DEBUG_SECTIONS))
                if (fill) return _FillColor;
                if (mask) return 0;
                #endif

                // Sample shadow map.
                float4 lineColor = half4(_OutlineColor.xyz, _OutlineColor.a);
                #if defined(OVERRIDE_SHADOW)
                float shadow = 1 - SampleShadowmap(
                    TransformWorldToShadowCoord(positionWS),
                    TEXTURE2D_ARGS(_MainLightShadowmapTexture, sampler_MainLightShadowmapTexture),
                    GetMainLightShadowSamplingData(),
                    GetMainLightShadowStrength(),
                    false);
                lineColor = lerp(_OutlineColor, _OutlineColorShadow, shadow);
                #endif

                // Return line.
                return lerp(_BackgroundColor, lineColor, edge);
            }
            ENDHLSL
        }
    }
}