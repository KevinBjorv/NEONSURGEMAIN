Shader "Custom/AdvancedDissolve"
{
    Properties
    {
        _Color("Tint Color", Color) = (1,1,1,1)
        _MainTex("Main Texture", 2D) = "white" {}
        _NoiseTex("Noise Texture", 2D) = "white" {}

        // Bullet line arrays (unchanged)
        _LineCount("Line Count", Range(0,10)) = 0
        _LineCenters("Hit Line Centers", Vector) = (0,0,0,0)
        _LineDirections("Hit Line Directions", Vector) = (1,0,0,0)
        _LineThicknesses("Hit Line Thicknesses", Vector) = (0.05,0,0,0)
        _LineProgresses("Hit Line Progresses", Vector) = (0,0,0,0)

        // Radial dissolve (unchanged)
        _UseRadial("Use Radial Dissolve (Player)", Float) = 0
        _RadialCenter("Radial Center (UV)", Vector) = (0.5,0.5,0,0)
        _RadialProgress("Radial Progress", Range(0,1)) = 0
        _RadialSpeedMultiplier("Radial Speed Multiplier", Range(0.1,5)) = 1

        // ADDED: Final noise-based dissolve
        _FinalNoiseProgress("Final Noise Progress", Range(0,1)) = 0 // 0 = no final dissolve, 1 = fully dissolved
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _NoiseTex;
            float4 _Color;

            float4 _LineCenters[10];
            float4 _LineDirections[10];
            float4 _LineThicknesses[10];
            float4 _LineProgresses[10];
            float _LineCount;

            float4 _RadialCenter;
            float _RadialProgress;
            float _UseRadial;
            float _RadialSpeedMultiplier;

            // ADDED: Final noise dissolve property
            float _FinalNoiseProgress;

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

            float4 _MainTex_ST;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float4 c = tex2D(_MainTex, i.uv) * _Color;
                float noiseVal = tex2D(_NoiseTex, i.uv).r;

                // Start fully opaque
                float alpha = c.a;

                // 1) Bullet lines
                for (int idx = 0; idx < _LineCount; idx++)
                {
                    float2 lineCenter = _LineCenters[idx].xy;
                    float2 lineDir = normalize(_LineDirections[idx].xy);
                    float thickness = _LineThicknesses[idx].x;
                    float lineProgress = _LineProgresses[idx].x;

                    float2 toPixel = i.uv - lineCenter;
                    float lineParam = dot(toPixel, lineDir);
                    float2 closest = lineCenter + lineParam * lineDir;
                    float distToLine = distance(i.uv, closest);

                    // If pixel is within lineProgress * thickness + a bit of noise, dissolve
                    float localDissolveThreshold = lineProgress * thickness;
                    if (distToLine < localDissolveThreshold + noiseVal * 0.1)
                    {
                        alpha = 0;
                        break; 
                    }
                }

                // 2) Radial dissolve
                if (_UseRadial > 0.5)
                {
                    float radialDist = distance(i.uv, _RadialCenter.xy);
                    float radialThreshold = _RadialProgress + noiseVal * 0.1;
                    if (radialDist < radialThreshold)
                    {
                        alpha = 0;
                    }
                }

                // 3) FINAL Noise-based dissolve (ADDED)
                // This is triggered if _FinalNoiseProgress > 0
                // so you can ramp from 0 to 1 over time in code.
                if (_FinalNoiseProgress > 0)
                {
                    // The larger _FinalNoiseProgress is, the bigger the area that gets dissolved.
                    // For instance, if noiseVal < _FinalNoiseProgress, we dissolve the pixel.
                    if (noiseVal < _FinalNoiseProgress)
                    {
                        alpha = 0;
                    }
                }

                if (alpha <= 0.0f) discard;

                c.a = alpha;
                return c;
            }
            ENDCG
        }
    }
    FallBack "Unlit/Transparent"
}
