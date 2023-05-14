Shader "Resight/MeshColors"
{
    SubShader
    {
        Pass
        {
            LOD 150
            ZTest LEqual        
            ZWrite On

            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"


            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float4 color : COLOR;
            };

            struct fragment_output
            {
                half4 color : SV_Target;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                return o;
            }

            fragment_output frag(v2f i)
            {
                fragment_output o;
                o.color = i.color;
                return o;
            }

            ENDHLSL
        }
    }

    FallBack "VertexLit"
}