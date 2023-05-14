Shader "Resight/MeshColorsAndShadows"
{
    Properties
    {
        _ShadowColor ("Shadow Color", Color) = (0.16, 0.15, 0.13, 0.75)
		_TintColor ("Tint Color", Color) = (1,1,1,1)
    }

    SubShader
    {                      
        Pass
        {
            Tags 
            {
                "LightMode" = "ForwardBase"
                "PassFlags" = "OnlyDirectional"
                "ForceNoShadowCasting" = "True"
                "RenderType"="Opaque"
                "Queue"="Geometry+1"
            }

            Blend SrcAlpha OneMinusSrcAlpha            
            ZTest LEqual
            LOD 150
            ZWrite On

            // Sets the depth offset for this geometry so that the GPU draws this geometry closer to the camera
            // You would typically do this to avoid z-fighting
            // Offset -1, -1

            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            #pragma multi_compile_fwdbase nolightmap nodirlightmap nodynlightmap novertexlight
            #include "AutoLight.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float4 color : COLOR;
                LIGHTING_COORDS(0,1)
            };

            struct fragment_output
            {
                half4 color : SV_Target;
            };


			fixed4 _TintColor;
            fixed4 _ShadowColor;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                TRANSFER_VERTEX_TO_FRAGMENT(o);
                return o;
            }

            fragment_output frag(v2f i)
            {
                fragment_output o;
                o.color = i.color * _TintColor;

                float shadowFactor = _ShadowColor.a * (1 - SHADOW_ATTENUATION(i));
                o.color.rgb = lerp(o.color.rgb, _ShadowColor.rgb, shadowFactor);
                return o;
            }

            ENDHLSL
        }
    }

    FallBack "VertexLit"
}