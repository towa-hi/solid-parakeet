Shader "Custom/InvisibleOpaqueShader"
{
    Properties
    {
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        Pass
        {
            // Enable blending that effectively discards color output
            Blend Zero One

            // Enable depth writing
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // Output any color; blending will prevent it from affecting the color buffer
                return float4(0, 0, 0, 0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
