Shader "Resight/Checkerboard"
{
    Properties
    {
        _Density ("Density", Range(2,50)) = 10
    }
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

            float _Density;

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float4 color : COLOR;
                float3 world_pos : TEXCOORD0;
            };

            struct fragment_output
            {
                half4 color : SV_Target;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.world_pos = mul(unity_ObjectToWorld, v.vertex) * _Density;
                o.color = v.color;
                return o;
            }
            
            fragment_output frag(v2f i)
            {
                fragment_output o;
                float3 c = floor(i.world_pos) * 0.5 + 0.5;
                o.color = i.color * frac(c.x + c.y + c.z) * 2;
                return o;
            }

            ENDHLSL
        }
    }

    FallBack "VertexLit"
}

