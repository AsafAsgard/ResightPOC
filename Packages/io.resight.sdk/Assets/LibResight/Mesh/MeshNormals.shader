Shader "Resight/MeshNormals"
{
    Properties
    {
        _Param1 ("Param1", Range(0.1,2.0)) = 1
    }

    SubShader
    {
        Pass
        {
            Tags { "LightMode" = "ForwardBase" }
            LOD 150
            ZTest LEqual        
            ZWrite On

            HLSLPROGRAM

			//#pragma target 3.0

            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "Lighting.cginc"

            float _Param1;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 normal : TEXCOORD0;
            };

            struct fragment_output
            {
                half4 color : SV_Target;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.normal = UnityObjectToWorldNormal(v.normal);
                return o;
            }

            fragment_output frag(v2f i)
            {
				float3 lightDir = _WorldSpaceLightPos0.xyz;
				float3 lightColor = _LightColor0.rgb;
                float3 albedo = i.normal * 0.5 + 0.5;
				float3 diffuse = pow(albedo, _Param1); //* lightColor * max(0, dot(lightDir, i.normal));
                fragment_output o;
                o.color.rgb = diffuse;
                o.color.a = 1;
                return o;
            }

            ENDHLSL
        }
    }

    FallBack "VertexLit"
}