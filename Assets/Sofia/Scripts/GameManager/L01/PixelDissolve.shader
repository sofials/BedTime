Shader "Custom/PixelDissolve"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (1,1,1,1)
        _MainTex ("Base Texture", 2D) = "white" {}
        _PixelSize ("Pixel Size", Float) = 64
        _PixelationStrength ("Pixelation Strength", Range(0,1)) = 1
        _DissolveProgress ("Dissolve Progress", Range(0,1)) = 0
        _DissolveTex ("Dissolve Noise", 2D) = "white" {}
        _DissolveDirection ("Dissolve Scroll Direction", Vector) = (1,0,0,0)
        _ScrollSpeed ("Scroll Speed", Float) = 1
        _FadeThreshold ("Fade Threshold", Float) = 0.1
    }
    
    SubShader
    {
        Tags { 
            "RenderType"="Transparent" 
            "Queue"="Transparent" 
            "RenderPipeline"="UniversalRenderPipeline"
        }
        LOD 200
        
        Pass
        {
            Name "PixelDissolvePass"
            Tags { "LightMode"="UniversalForward" }
            
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            
            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float3 normal : NORMAL;
            };
            
            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
                float3 worldNormal : TEXCOORD2;
                float3 worldPos : TEXCOORD3;
            };
            
            // Texture and sampler declarations
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_DissolveTex);
            SAMPLER(sampler_DissolveTex);
            
            // Property declarations
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _BaseColor;
                float _PixelSize;
                float _PixelationStrength;
                float _DissolveProgress;
                float4 _DissolveDirection;
                float _ScrollSpeed;
                float _FadeThreshold;
            CBUFFER_END
            
            v2f vert (appdata v)
            {
                v2f o;
                VertexPositionInputs vertexInput = GetVertexPositionInputs(v.vertex.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(v.normal);
                
                o.pos = vertexInput.positionCS;
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.screenPos = vertexInput.positionNDC;
                o.worldNormal = normalInput.normalWS;
                o.worldPos = vertexInput.positionWS;
                
                return o;
            }
            
            float2 Pixelate(float2 uv, float2 screenUV, float pixelSize, float strength)
            {
                // Get screen resolution
                float2 resolution = float2(_ScreenParams.x, _ScreenParams.y);
                
                // Convert to pixel coordinates
                screenUV *= resolution;
                
                // Pixelate
                float2 pixelated = floor(screenUV / pixelSize) * pixelSize;
                
                // Blend between original and pixelated
                float2 smooth = lerp(screenUV, pixelated, strength);
                
                // Convert back to UV space
                return smooth / resolution;
            }
            
            half4 frag (v2f i) : SV_Target
            {
                // Get screen space UV coordinates
                float2 screenUV = i.screenPos.xy / i.screenPos.w;
                screenUV = screenUV * 0.5 + 0.5;
                
                // Apply pixelation effect
                float2 pixelUV = Pixelate(i.uv, screenUV, _PixelSize, _PixelationStrength);
                
                // Sample dissolve noise texture
                float dissolveNoise = SAMPLE_TEXTURE2D(_DissolveTex, sampler_DissolveTex, pixelUV + _Time.y * 0.1).r;
                
                // Calculate UV coordinates for sampling
                float2 uvToSample = pixelUV;
                
                // Apply wave/current effect when dissolving
                if (dissolveNoise < _DissolveProgress)
                {
                    float dissolveAmount = (_DissolveProgress - dissolveNoise);
                    
                    // Create wave motion
                    float2 waveOffset = _DissolveDirection.xy * dissolveAmount * _ScrollSpeed;
                    
                    // Add some noise to make it more organic
                    float2 noiseOffset = float2(
                        sin(_Time.y * 2.0 + pixelUV.x * 10.0) * 0.02,
                        cos(_Time.y * 1.5 + pixelUV.y * 8.0) * 0.02
                    ) * dissolveAmount;
                    
                    uvToSample += waveOffset + noiseOffset;
                }
                
                // Sample the main texture
                half4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uvToSample) * _BaseColor;
                
                // Apply basic lighting
                Light mainLight = GetMainLight();
                float NdotL = saturate(dot(i.worldNormal, mainLight.direction));
                col.rgb *= mainLight.color * (NdotL * 0.5 + 0.5); // Half-Lambert lighting
                
                // Calculate fade based on dissolve progress
                float fadeStart = _DissolveProgress - _FadeThreshold;
                float fadeAmount = 1.0 - smoothstep(fadeStart, _DissolveProgress, dissolveNoise);
                
                // Apply fade to alpha
                col.a *= fadeAmount;
                
                // Discard pixels that are fully dissolved
                if (dissolveNoise < _DissolveProgress - _FadeThreshold)
                    discard;
                
                return col;
            }
            ENDHLSL
        }
    }
    
    FallBack "Universal Render Pipeline/Lit"
}