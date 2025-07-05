Shader "Unlit/DynamicNoiseBackground"
{
    Properties
    {
        _Color1("Bottom Color", Color) = (0, 0, 0, 1)
        _Color2("Top Color", Color) = (0.2, 0.2, 0.2, 1)
        _NoiseTex("Noise Texture", 2D) = "white" {}
        _NoiseScale("Noise Scale", Float) = 1.0
        _FlowSpeed("Flow Speed", Float) = 0.1
        _NoiseIntensity("Noise Intensity", Range(0, 1)) = 0.1
    }
    SubShader
    {
        Tags
        {
            "Queue"="Background"
            "IgnoreProjector"="True"
            "RenderType"="Opaque"
        }
        LOD 100
        
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _NoiseTex;
            float4 _Color1;
            float4 _Color2;
            float _NoiseScale;
            float _FlowSpeed;
            float _NoiseIntensity;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float baseGradient = i.uv.y;

                // Scroll the noise texture over time
                float2 noiseUV = i.uv * _NoiseScale;
                noiseUV.x += _Time.y * _FlowSpeed;

                // Sample from the generated noise texture
                float noiseValue = tex2D(_NoiseTex, noiseUV).r;

                // Combine base gradient with noise
                float grad = clamp(baseGradient + noiseValue * _NoiseIntensity, 0.0, 1.0);

                return lerp(_Color1, _Color2, grad);
            }
            ENDCG
        }
    }
    FallBack Off
}
