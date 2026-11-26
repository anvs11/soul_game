Shader "Game/SewerDarkness"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Lantern ("Lantern", Vector) = (0,0,0,0)
    }
    SubShader
    {
        Tags { "Queue"="Transparent+100" "RenderType"="Transparent" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"
            float4 _Lantern;
            struct MeshVertex { float4 position : POSITION; };
            struct RasterVertex { float4 position : SV_POSITION; float2 world : TEXCOORD0; };
            RasterVertex Vert(MeshVertex v)
            {
                RasterVertex o;
                o.position = UnityObjectToClipPos(v.position);
                o.world = mul(unity_ObjectToWorld, v.position).xy;
                return o;
            }
            fixed4 Frag(RasterVertex v) : SV_Target
            {
                float d = length(v.world - _Lantern.xy);
                return fixed4(0.015, 0.027, 0.023, lerp(.02, .88, smoothstep(2.6, 6.2, d)));
            }
            ENDCG
        }
    }
}
