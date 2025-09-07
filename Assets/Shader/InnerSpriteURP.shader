Shader "Cards/InnerSpriteURP"
{
	Properties
	{
		[MainTexture] _MainTex ("Sprite Texture", 2D) = "white" {}
		[MainColor] _Color ("Tint", Color) = (1,1,1,1)
		_StencilRef ("Stencil Ref", Float) = 1
		_ZOffset ("Local Z Offset", Float) = 0
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
			ZTest LEqual
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
			CBUFFER_END

			struct Attributes { float3 positionOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
			struct Varyings  { float4 positionHCS : SV_POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };

			Varyings Vert(Attributes IN)
			{
				Varyings OUT;
				UNITY_SETUP_INSTANCE_ID(IN);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
				float3 posOS = IN.positionOS;
				posOS.z += _ZOffset;
				OUT.positionHCS = TransformObjectToHClip(posOS);
				OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
				return OUT;
			}

			half4 Frag(Varyings IN) : SV_Target
			{
				return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv) * _Color;
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
			ZTest LEqual
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
			CBUFFER_END

			struct Attributes { float3 positionOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
			struct Varyings  { float4 positionHCS : SV_POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };

			Varyings Vert(Attributes IN)
			{
				Varyings OUT;
				UNITY_SETUP_INSTANCE_ID(IN);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
				float3 posOS = IN.positionOS;
				posOS.z += _ZOffset;
				OUT.positionHCS = TransformObjectToHClip(posOS);
				OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
				return OUT;
			}

			half4 Frag(Varyings IN) : SV_Target
			{
				return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv) * _Color;
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
			ZTest LEqual
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
			CBUFFER_END

			struct Attributes { float3 positionOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
			struct Varyings  { float4 positionHCS : SV_POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };

			Varyings Vert(Attributes IN)
			{
				Varyings OUT;
				UNITY_SETUP_INSTANCE_ID(IN);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
				float3 posOS = IN.positionOS;
				posOS.z += _ZOffset;
				OUT.positionHCS = TransformObjectToHClip(posOS);
				OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
				return OUT;
			}

			half4 Frag(Varyings IN) : SV_Target
			{
				return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv) * _Color;
			}
			ENDHLSL
		}
	}
}