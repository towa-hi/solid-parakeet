Shader "Cards/InnerSpriteURP"
{
	Properties
	{
		[MainTexture] _MainTex ("Sprite Texture", 2D) = "white" {}
		[MainColor] _Color ("Tint", Color) = (1,1,1,1)
		_StencilRef ("Stencil Ref", Float) = 1
		_ZOffset ("Local Z Offset", Float) = 0
		_EdgeFeather ("Edge Feather", Range(0,0.2)) = 0.05
		_CoverageGain ("Coverage Gain", Range(0,5)) = 1
		_ParallaxStrength ("Parallax Strength", Range(0,2)) = 0.25
	}
	SubShader
	{
		Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" "CanUseSpriteAtlas"="True" }

		

		Pass
		{
			Name "SpriteForward2D"
			Tags { "LightMode"="Universal2D" }
			Blend SrcAlpha OneMinusSrcAlpha
			Cull Off
			ZWrite Off
			ZTest Always
			Stencil { Ref [_StencilRef] Comp Equal Pass Keep Fail Keep ZFail Keep }

			HLSLPROGRAM
			#pragma vertex Vert
			#pragma fragment Frag
			#pragma target 2.0
			#pragma multi_compile_instancing
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

			TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
			CBUFFER_START(UnityPerMaterial)
				float4 _Color;
				float4 _MainTex_ST;
				float _ZOffset;
				float _EdgeFeather;
				float _ParallaxStrength;
			CBUFFER_END

			struct Attributes { float3 positionOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
			struct Varyings  { float4 positionHCS : SV_POSITION; float2 uv : TEXCOORD0; float3 posOS : TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };

			Varyings Vert(Attributes IN)
			{
				Varyings OUT;
				UNITY_SETUP_INSTANCE_ID(IN);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
				float3 posOS = IN.positionOS;
				OUT.positionHCS = TransformObjectToHClip(posOS);
				OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
				OUT.posOS = IN.positionOS;
				return OUT;
			}

			half4 Frag(Varyings IN) : SV_Target
			{
				// View direction in object space
				float3 camOS = mul(GetWorldToObjectMatrix(), float4(_WorldSpaceCameraPos, 1.0)).xyz;
				float3 viewDirOS = normalize(IN.posOS - camOS);
				float2 parallax = (viewDirOS.xy / max(abs(viewDirOS.z), 1e-4)) * _ParallaxStrength * _ZOffset;
				float2 uv = IN.uv + parallax;
				half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv) * _Color;
				// screen-consistent edge feather using derivatives
				float2 duv = fwidth(IN.uv);
				float2 edgeDist = min(IN.uv, 1.0 - IN.uv);
				float edge = saturate((min(edgeDist.x, edgeDist.y) - _EdgeFeather) / max(max(duv.x, duv.y), 1e-5));
				tex.a *= edge;
				return tex;
			}
			ENDHLSL
		}

		

		Pass
		{
			Name "SpriteForward"
			Tags { "LightMode"="UniversalForward" }
			Blend SrcAlpha OneMinusSrcAlpha
			Cull Off
			ZWrite Off
			ZTest Always
			Stencil { Ref [_StencilRef] Comp Equal Pass Keep Fail Keep ZFail Keep }

			HLSLPROGRAM
			#pragma vertex Vert
			#pragma fragment Frag
			#pragma target 2.0
			#pragma multi_compile_instancing
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

			TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
			CBUFFER_START(UnityPerMaterial)
				float4 _Color;
				float4 _MainTex_ST;
				float _ZOffset;
				float _EdgeFeather;
				float _ParallaxStrength;
			CBUFFER_END

			struct Attributes { float3 positionOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
			struct Varyings  { float4 positionHCS : SV_POSITION; float2 uv : TEXCOORD0; float3 posOS : TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };

			Varyings Vert(Attributes IN)
			{
				Varyings OUT;
				UNITY_SETUP_INSTANCE_ID(IN);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
				float3 posOS = IN.positionOS;
				OUT.positionHCS = TransformObjectToHClip(posOS);
				OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
				OUT.posOS = IN.positionOS;
				return OUT;
			}

			half4 Frag(Varyings IN) : SV_Target
			{
				float3 camOS = mul(GetWorldToObjectMatrix(), float4(_WorldSpaceCameraPos, 1.0)).xyz;
				float3 viewDirOS = normalize(IN.posOS - camOS);
				float2 parallax = (viewDirOS.xy / max(abs(viewDirOS.z), 1e-4)) * _ParallaxStrength * _ZOffset;
				float2 uv = IN.uv + parallax;
				half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv) * _Color;
				float2 duv = fwidth(IN.uv);
				float2 edgeDist = min(IN.uv, 1.0 - IN.uv);
				float edge = saturate((min(edgeDist.x, edgeDist.y) - _EdgeFeather) / max(max(duv.x, duv.y), 1e-5));
				tex.a *= edge;
				return tex;
			}
			ENDHLSL
		}

		// Fallback for passes where neither Universal2D nor UniversalForward is used (e.g., some WebGL paths)
		

		Pass
		{
			Name "SpriteUnlitFallback"
			Tags { "LightMode"="SRPDefaultUnlit" }
			Blend SrcAlpha OneMinusSrcAlpha
			Cull Off
			ZWrite Off
			ZTest Always
			Stencil { Ref [_StencilRef] Comp Equal Pass Keep Fail Keep ZFail Keep }

			HLSLPROGRAM
			#pragma vertex Vert
			#pragma fragment Frag
			#pragma target 2.0
			#pragma multi_compile_instancing
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

			TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
			CBUFFER_START(UnityPerMaterial)
				float4 _Color;
				float4 _MainTex_ST;
				float _ZOffset;
				float _EdgeFeather;
				float _ParallaxStrength;
			CBUFFER_END

			struct Attributes { float3 positionOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
			struct Varyings  { float4 positionHCS : SV_POSITION; float2 uv : TEXCOORD0; float3 posOS : TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };

			Varyings Vert(Attributes IN)
			{
				Varyings OUT;
				UNITY_SETUP_INSTANCE_ID(IN);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
				float3 posOS = IN.positionOS;
				OUT.positionHCS = TransformObjectToHClip(posOS);
				OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
				OUT.posOS = IN.positionOS;
				return OUT;
			}

			half4 Frag(Varyings IN) : SV_Target
			{
				float3 camOS = mul(GetWorldToObjectMatrix(), float4(_WorldSpaceCameraPos, 1.0)).xyz;
				float3 viewDirOS = normalize(IN.posOS - camOS);
				float2 parallax = (viewDirOS.xy / max(abs(viewDirOS.z), 1e-4)) * _ParallaxStrength * _ZOffset;
				float2 uv = IN.uv + parallax;
				half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv) * _Color;
				float2 duv = fwidth(IN.uv);
				float2 edgeDist = min(IN.uv, 1.0 - IN.uv);
				float edge = saturate((min(edgeDist.x, edgeDist.y) - _EdgeFeather) / max(max(duv.x, duv.y), 1e-5));
				tex.a *= edge;
				return tex;
			}
			ENDHLSL
		}
	}
}