Shader "Hidden/ChannelMixer"
{
    Properties
    {
        _MainTex ("Base (RGB)", 2D) = "white" {}
        _R ("Red Mixing", Color) = (1,0,0,1)
        _G ("Green Mixing", Color) = (0,1,0,1)
        _B ("Blue Mixing", Color) = (0,0,1,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            ZTest Always Cull Off ZWrite Off

            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            fixed4 _R;
            fixed4 _G;
            fixed4 _B;

            fixed4 frag(v2f_img i) : COLOR
            {
                fixed4 c = tex2D(_MainTex, i.uv);
                return fixed4(
                    c.r * _R.r + c.g * _R.g + c.b * _R.b,
                    c.r * _G.r + c.g * _G.g + c.b * _G.b,
                    c.r * _B.r + c.g * _B.g + c.b * _B.b,
                    c.a);
            }
            ENDCG
        }
    }
    FallBack "Diffuse"
}
