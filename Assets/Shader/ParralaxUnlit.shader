Shader "Unlit/ParralaxUnlit"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        // Parallax controls
        _Depth0 ("Depth 0", Range(0, 2)) = 0.0
        _Depth1 ("Depth 1", Range(0, 2)) = 0.2
        _Depth2 ("Depth 2", Range(0, 2)) = 0.4
        _ParallaxScale ("Parallax Scale", Float) = 1.0
        _UseFixedNormal ("Use Fixed +Z Normal (0/1)", Range(0,1)) = 0

        // Circle parameters
        _Radius0 ("Radius 0", Range(0, 1)) = 0.2
        _Radius1 ("Radius 1", Range(0, 1)) = 0.3
        _Radius2 ("Radius 2", Range(0, 1)) = 0.4

        // Colors
        _Color0 ("Color 0 (near)", Color) = (0,0,1,1)
        _Color1 ("Color 1 (mid)", Color) = (0,1,0,1)
        _Color2 ("Color 2 (far)", Color) = (1,0,0,1)
        _BgColor ("Background Color", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            #pragma multi_compile_fog

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
                float3 objPos : TEXCOORD2;
                float3 objNormal : TEXCOORD3;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _Depth0;
            float _Depth1;
            float _Depth2;
            float _ParallaxScale;
            float _UseFixedNormal;
            float _Radius0;
            float _Radius1;
            float _Radius2;
            fixed4 _Color0;
            fixed4 _Color1;
            fixed4 _Color2;
            fixed4 _BgColor;

            // Helpers (global scope in HLSL/CG)
            float sdfCircle(float2 p, float r, float2 off)
            {
                float2 q = p - off;
                return length(q) - r;
            }

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                UNITY_TRANSFER_FOG(o,o.vertex);
                o.objPos = v.vertex.xyz;
                o.objNormal = v.normal;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Parallax SDF circles demo inspired by bgolus thread
                float2 uv = i.uv - 0.5;

                // Per-fragment view direction in object space
                float3 camPosOS = mul(unity_WorldToObject, float4(_WorldSpaceCameraPos, 1.0)).xyz;
                float3 viewDirOS = normalize(camPosOS - i.objPos);

                // Plane/object-space normal (blend mesh with fixed +Z)
                float3 normalMesh = normalize(i.objNormal);
                float3 normalFixed = float3(0.0, 0.0, 1.0);
                float3 normalOS = normalize(lerp(normalMesh, normalFixed, saturate(_UseFixedNormal)));
                float facing = dot(viewDirOS, normalOS);
                // Avoid division by zero when extremely grazing or backfacing
                facing = max(facing, 1e-4);
                float3 perspective = viewDirOS / facing;

                // Depth distances (exposed)
                float detphDist  = _Depth0;
                float detphDist1 = _Depth1;
                float detphDist2 = _Depth2;

                float2 offset  = detphDist  * perspective.xy * _ParallaxScale;
                float2 offset1 = detphDist1 * perspective.xy * _ParallaxScale;
                float2 offset2 = detphDist2 * perspective.xy * _ParallaxScale;

                // Shapes
                float shape  = sdfCircle(uv, _Radius0, offset);
                float shape1 = sdfCircle(uv, _Radius1, offset1);
                float shape2 = sdfCircle(uv, _Radius2, offset2);

                // Colors and blending
                float3 c = _BgColor.rgb;
                c = lerp(_Color2.rgb, c, step(0.0, shape2));
                c = lerp(_Color1.rgb, c, step(0.0, shape1));
                c = lerp(_Color0.rgb, c, step(0.0, shape));

                fixed4 col = fixed4(c, 1.0);
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }
}
