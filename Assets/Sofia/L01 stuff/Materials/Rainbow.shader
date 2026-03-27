Shader "Unlit/RainbowTransparent"
{
    Properties
    {
        _Speed ("Rainbow Speed", Float) = 0.5
        _Scale ("Color Scale", Float) = 1.0
        _Alpha ("Transparency", Range(0, 1)) = 1.0
        _SparkleIntensity ("Sparkle Intensity", Range(0, 5)) = 1.5
        _SparkleSpeed ("Sparkle Speed", Float) = 5.0
        _SparkleDensity ("Sparkle Density", Float) = 10.0
        _SparkleSize ("Sparkle Size", Range(0.01, 0.2)) = 0.05
        _PastelSaturation ("Pastel Saturation", Range(0.1, 1.0)) = 0.4
        _PastelBrightness ("Pastel Brightness", Range(0.5, 1.0)) = 0.85
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        LOD 100

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
            };

            float _Speed;
            float _Scale;
            float _Alpha;
            float _SparkleIntensity;
            float _SparkleSpeed;
            float _SparkleDensity;
            float _SparkleSize;
            float _PastelSaturation;
            float _PastelBrightness;

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 78.233);
                return frac(p.x * p.y);
            }

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                UNITY_TRANSFER_FOG(o, o.vertex);
                return o;
            }

            float3 hsv2rgb(float3 c)
            {
                float4 K = float4(1.0, 2.0/3.0, 1.0/3.0, 3.0);
                float3 p = abs(frac(c.xxx + K.xyz) * 6.0 - K.www);
                return c.z * lerp(K.xxx, saturate(p - K.xxx), c.y);
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Arcobaleno verticale con colori pastello
                float hue = frac(i.uv.y * _Scale + _Time.y * _Speed);
                float3 rgb = hsv2rgb(float3(hue, _PastelSaturation, _PastelBrightness));

                // Sparkle pseudo-random
                float2 gridUV = i.uv * _SparkleDensity;
                float2 cell = floor(gridUV);
                float2 local = frac(gridUV) - 0.5;
                float dist = length(local);

                float sparkleSeed = hash21(cell);
                float sparklePulse = sin(_Time.y * _SparkleSpeed + sparkleSeed * 6.28);
                float sparkleActive = smoothstep(0.96, 1.0, sparklePulse);

                // Maschera rotonda
                float sparkleMask = smoothstep(_SparkleSize, 0.0, dist);

                float sparkle = sparkleActive * sparkleMask;

                // Brillantezza con tonalità pastello anche per le scintille
                float3 sparkleColor = lerp(rgb, float3(1, 1, 1), 0.7) * pow(sparkle, 3.0) * _SparkleIntensity;

                rgb += sparkleColor;

                fixed4 col = float4(rgb, _Alpha);
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }
}