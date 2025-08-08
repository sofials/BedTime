Shader "Custom/PixellatedDissolveWind_ToggleFull"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (1,1,1,1)
        _PixelSize ("Pixel Size", Float) = 0.1
        _DisplaceAmount ("Displace Amount", Float) = 1.0
        _DissolveAmount ("Dissolve Amount", Range(0,1)) = 0.0
        _EnableDisplacement ("Enable Displacement", Float) = 0.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
            };

            float4 _BaseColor;
            float _PixelSize;
            float _DisplaceAmount;
            float _DissolveAmount;
            float _EnableDisplacement;

            float hash33(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.yzx + 19.19);
                return frac((p.x + p.y) * p.z);
            }

            Varyings vert (Attributes IN)
            {
                Varyings OUT;

                float3 worldPos = TransformObjectToWorld(IN.positionOS.xyz);
                float3 finalPos;

                if (_EnableDisplacement < 0.5)
                {
                    // Posizione originale, nessuna pixelazione/spostamento
                    finalPos = worldPos;
                }
                else
                {
                    // Pixel dissolve attivo
                    float3 blockOrigin = floor(worldPos / _PixelSize) * _PixelSize;

                    float seed = hash33(blockOrigin);
                    float dissolveStart = seed * 3.0;
                    float localTime = saturate(_Time.y - dissolveStart);
                    float ease = smoothstep(0.0, 1.0, localTime);
                    float dissolveProgress = saturate(_DissolveAmount);

                    if (localTime > 0.0 && dissolveProgress > seed)
                    {
                        float3 windDir = normalize(float3(-1.0, 0.5, 0.3));
                        float waveY = sin(_Time.y * 3.0 + blockOrigin.y * 5.0);
                        float waveZ = cos(_Time.y * 2.5 + blockOrigin.z * 4.0);
                        float3 turbulence = float3(0.0, waveY, waveZ) * 0.2;
                        float3 motion = (windDir + turbulence) * _DisplaceAmount * ease;
                        float3 displacedPos = blockOrigin + motion;

                        // Interpola tra originale e pixel spostato (smooth dissolve)
                        finalPos = lerp(worldPos, displacedPos, ease);
                    }
                    else
                    {
                        finalPos = blockOrigin;
                    }
                }

                OUT.positionHCS = TransformWorldToHClip(finalPos);
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                return _BaseColor;
            }
            ENDHLSL
        }
    }
}
