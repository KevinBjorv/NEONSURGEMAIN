Shader "Custom/CircleSpriteShader"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Base Color", Color) = (1,1,1,1)
        _EmissionColor ("Emission Color", Color) = (0,0,0,0)
        _GradientStrength ("Gradient Strength", Range(0,1)) = 1.0
        _EmissionStrength ("Emission Strength", Range(0,5)) = 1.0
    }
    SubShader
    {
        Tags 
        { 
            "Queue"="Transparent" 
            "IgnoreProjector"="True" 
            "RenderType"="Transparent" 
            "PreviewType"="Plane" 
            "CanUseSpriteAtlas"="True"
        }
        Lighting Off
        ZWrite Off
        Cull Off
        Fog { Mode Off }
        
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord  : TEXCOORD0;
                float2 uv       : TEXCOORD1;
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _EmissionColor;
            float _GradientStrength;
            float _EmissionStrength;
            float4 _MainTex_ST;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = TRANSFORM_TEX(IN.texcoord, _MainTex);
                OUT.uv = IN.texcoord;
                OUT.color = IN.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                // Sample base sprite texture
                fixed4 texcol = tex2D(_MainTex, IN.texcoord) * IN.color;

                // Compute radial gradient based on UV distance from center
                float2 center = float2(0.5, 0.5);
                float dist = distance(IN.uv, center);

                // Use gradient strength to control fade
                float gradient = saturate(1.0 - dist * _GradientStrength);

                // Apply base color modulated by gradient
                fixed4 baseColor = texcol * gradient;

                // Add emission effect scaled by gradient
                fixed4 emission = _EmissionColor * _EmissionStrength * gradient;

                // Combine base color and emission
                fixed4 finalColor = baseColor + emission;

                // Maintain original alpha from texture
                finalColor.a = texcol.a;

                return finalColor;
            }
            ENDCG
        }
    }
}
