Shader "Cards/FrameMaskURP"
{
	Properties
	{
		_StencilRef ("Stencil Ref", Float) = 1
	}
	SubShader
	{
		Tags { "Queue"="Transparent-10" "RenderType"="Transparent" "RenderPipeline"="UniversalRenderPipeline" "IgnoreProjector"="True" "CanUseSpriteAtlas"="True" }

		Pass
		{
			Name "SpriteForward2D"
			Tags { "LightMode"="Universal2D" }
			Blend SrcAlpha OneMinusSrcAlpha
			Cull Off
			ZWrite Off
			ZTest LEqual
			Stencil { Ref [_StencilRef] Comp Always Pass Replace Fail Keep ZFail Keep }

			HLSLPROGRAM
			#pragma vertex Vert
			#pragma fragment Frag
			#pragma target 2.0
			#pragma multi_compile_instancing
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

			CBUFFER_START(UnityPerMaterial)
			CBUFFER_END

			struct Attributes { float3 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
			struct Varyings  { float4 positionHCS : SV_POSITION; UNITY_VERTEX_OUTPUT_STEREO };

			Varyings Vert(Attributes IN)
			{
				Varyings OUT;
				UNITY_SETUP_INSTANCE_ID(IN);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
				OUT.positionHCS = TransformObjectToHClip(IN.positionOS);
				return OUT;
			}

			half4 Frag(Varyings IN) : SV_Target
			{
				return half4(0,0,0,0); // invisible mask, writes stencil via state
			}
			ENDHLSL
		}

		// Optional forward pass for non-2D renderer fallback
		Pass
		{
			Name "SpriteForward"
			Tags { "LightMode"="UniversalForward" }
			Blend SrcAlpha OneMinusSrcAlpha
			Cull Off
			ZWrite Off
			ZTest LEqual
			Stencil { Ref [_StencilRef] Comp Always Pass Replace Fail Keep ZFail Keep }

			HLSLPROGRAM
			#pragma vertex Vert
			#pragma fragment Frag
			#pragma target 2.0
			#pragma multi_compile_instancing
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

			CBUFFER_START(UnityPerMaterial)
			CBUFFER_END

			struct Attributes { float3 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
			struct Varyings  { float4 positionHCS : SV_POSITION; UNITY_VERTEX_OUTPUT_STEREO };

			Varyings Vert(Attributes IN)
			{
				Varyings OUT;
				UNITY_SETUP_INSTANCE_ID(IN);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
				OUT.positionHCS = TransformObjectToHClip(IN.positionOS);
				return OUT;
			}

			half4 Frag(Varyings IN) : SV_Target
			{
				return half4(0,0,0,0);
			}
			ENDHLSL
		}
	}
}