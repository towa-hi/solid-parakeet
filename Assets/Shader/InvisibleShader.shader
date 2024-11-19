Shader "Custom/InvisibleOpaqueShader"
{
    Properties
    {
        // No properties are needed unless you have specific requirements.
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            // Disable color writes
            ColorMask 0

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
                // Return any color; it won't be written due to ColorMask 0
                return float4(0, 0, 0, 0);
            }
            ENDHLSL
        }
    }
    FallBack "Diffuse"
}
