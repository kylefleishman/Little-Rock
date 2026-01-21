Shader "Custom/VertexColorTerrain"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata {
                float4 vertex : POSITION;
                float4 color : COLOR; // This pulls from your mesh.colors array
            };

            struct v2f {
                float4 pos : SV_POSITION;
                float4 color : COLOR;
            };

            v2f vert (appdata v) {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target {
                return i.color; // Paints the pixel the biome color
            }
            ENDCG
        }
    }
}