Shader "Custom/PixellatedDissolveWind_ToggleFull"
{
    Properties
    {
        [Header(Base Properties)]
        _BaseMap ("Base Texture", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1,1,1,1)
        
        [Header(Pixellation Settings)]
        [Toggle] _EnablePixellation ("Enable Pixellation", Float) = 0.0
        _PixelSize ("Pixel Size", Range(0.01, 0.5)) = 0.1
        
        [Header(Wind Dissolve Animation)]
        _DissolveAmount ("Dissolve Amount", Range(0,1)) = 0.0
        _DisplaceAmount ("Wind Strength", Range(0, 5)) = 1.0
        _WindDirection ("Wind Direction", Vector) = (-1, 0.5, 0.3, 0)
        _AnimationSpeed ("Animation Speed", Range(0.1, 5)) = 1.0
    }
    
    SubShader
    {
        Tags 
        { 
            "RenderType"="Opaque" 
            "Queue"="Geometry"
            "RenderPipeline"="UniversalPipeline"
        }
        
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            
            // Render state per shader opaco standard
            Blend One Zero
            ZWrite On
            ZTest LEqual
            Cull Back
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float alpha : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // Properties
            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float _EnablePixellation;
                float _PixelSize;
                float _DissolveAmount;
                float _DisplaceAmount;
                float4 _WindDirection;
                float _AnimationSpeed;
            CBUFFER_END

            // Hash function semplice
            float hash31(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.yzx + 19.19);
                return frac((p.x + p.y) * p.z);
            }

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                // Posizione originale in world space
                float3 worldPos = TransformObjectToWorld(IN.positionOS.xyz);
                float3 finalPos = worldPos;
                float alpha = 1.0;

                // SOLO se pixellation è abilitata, modifica la geometria
                if (_EnablePixellation > 0.5)
                {
                    // Snap alla griglia pixel
                    float3 pixelPos = floor(worldPos / _PixelSize) * _PixelSize;
                    
                    // Se dissolve è attivo, calcola movimento
                    if (_DissolveAmount > 0.01)
                    {
                        float seed = hash31(pixelPos);
                        
                        // Il pixel si dissolve quando dissolveAmount supera il suo seed
                        if (_DissolveAmount > seed)
                        {
                            // Progresso locale del dissolve per questo pixel
                            float progress = (_DissolveAmount - seed) / (1.0 - seed);
                            progress = saturate(progress);
                            
                            // Movimento del vento
                            float3 windDir = normalize(_WindDirection.xyz);
                            float windInfluence = progress * progress; // Accelerazione
                            
                            // Aggiunge un po' di randomness al movimento
                            float3 randomOffset = (hash31(pixelPos + float3(1,2,3)) - 0.5) * 2.0;
                            windDir += randomOffset * 0.2;
                            
                            // Applica movimento
                            finalPos = pixelPos + windDir * _DisplaceAmount * windInfluence;
                            
                            // Fade out basato su distanza
                            float fadeDistance = 2.0;
                            float dist = length(windDir * _DisplaceAmount * windInfluence);
                            alpha = saturate(1.0 - dist / fadeDistance);
                        }
                        else
                        {
                            // Pixel non ancora dissolto
                            finalPos = pixelPos;
                        }
                    }
                    else
                    {
                        // Dissolve non attivo, ma pixellation sì - snap alla griglia
                        finalPos = pixelPos;
                    }
                }
                // Altrimenti finalPos rimane = worldPos (posizione originale)

                OUT.positionHCS = TransformWorldToHClip(finalPos);
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                OUT.alpha = alpha;
                
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                half4 texColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);
                half4 color = texColor * _BaseColor;
                
                // Se pixellation non è attiva, restituisci il colore base
                if (_EnablePixellation < 0.5)
                {
                    return color;
                }
                
                // Se pixellation è attiva e il pixel si sta dissolvendo, applica alpha
                if (_DissolveAmount > 0.01)
                {
                    // Clip pixel completamente trasparenti per performance
                    if (IN.alpha < 0.01)
                        discard;
                        
                    color.rgb *= IN.alpha; // Darkening invece di alpha blending
                }
                
                return color;
            }
            ENDHLSL
        }
    }
    
    // Shader per quando i pixel diventano trasparenti
    SubShader
    {
        Tags 
        { 
            "RenderType"="Transparent" 
            "Queue"="Transparent"
            "RenderPipeline"="UniversalPipeline"
        }
        
        Pass
        {
            Name "ForwardTransparent"
            Tags { "LightMode"="UniversalForward" }
            
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off
            
            HLSLPROGRAM
            // Stesso codice del pass opaco ma con alpha blending
            #pragma vertex vert
            #pragma fragment frag_transparent
            #pragma multi_compile_instancing
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // Stesse strutture e funzioni del pass precedente
            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float alpha : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float _EnablePixellation;
                float _PixelSize;
                float _DissolveAmount;
                float _DisplaceAmount;
                float4 _WindDirection;
                float _AnimationSpeed;
            CBUFFER_END

            float hash31(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.yzx + 19.19);
                return frac((p.x + p.y) * p.z);
            }

            // Stesso vertex shader
            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                float3 worldPos = TransformObjectToWorld(IN.positionOS.xyz);
                float3 finalPos = worldPos;
                float alpha = 1.0;

                if (_EnablePixellation > 0.5)
                {
                    float3 pixelPos = floor(worldPos / _PixelSize) * _PixelSize;
                    
                    if (_DissolveAmount > 0.01)
                    {
                        float seed = hash31(pixelPos);
                        
                        if (_DissolveAmount > seed)
                        {
                            float progress = (_DissolveAmount - seed) / (1.0 - seed);
                            progress = saturate(progress);
                            
                            float3 windDir = normalize(_WindDirection.xyz);
                            float windInfluence = progress * progress;
                            
                            float3 randomOffset = (hash31(pixelPos + float3(1,2,3)) - 0.5) * 2.0;
                            windDir += randomOffset * 0.2;
                            
                            finalPos = pixelPos + windDir * _DisplaceAmount * windInfluence;
                            
                            float fadeDistance = 2.0;
                            float dist = length(windDir * _DisplaceAmount * windInfluence);
                            alpha = saturate(1.0 - dist / fadeDistance);
                        }
                        else
                        {
                            finalPos = pixelPos;
                        }
                    }
                    else
                    {
                        finalPos = pixelPos;
                    }
                }

                OUT.positionHCS = TransformWorldToHClip(finalPos);
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                OUT.alpha = alpha;
                
                return OUT;
            }

            half4 frag_transparent (Varyings IN) : SV_Target
            {
                half4 texColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);
                half4 color = texColor * _BaseColor;
                
                if (_EnablePixellation < 0.5)
                {
                    return color;
                }
                
                color.a *= IN.alpha;
                
                return color;
            }
            ENDHLSL
        }
    }
    
    Fallback "Universal Render Pipeline/Unlit"
}