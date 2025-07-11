Shader "Unlit/RevealSketch"
{
    Properties
    {
        _Color1 ("Color 1", Color) = (1,1,0,1)      // colore chiaro (es. giallo)
        _Color2 ("Color 2", Color) = (1,0.8,0,1)    // colore scuro (es. arancio/giallo scuro)
        _SketchTex ("Sketch Texture", 2D) = "white" {}
        _RevealProgress ("Reveal Progress", Range(0,1)) = 0
        _SketchIntensity ("Sketch Intensity", Range(0,1)) = 0.5
        _ToonThreshold ("Toon Threshold", Range(0,1)) = 0.5
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        LOD 100

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            float4 _Color1;
            float4 _Color2;
            sampler2D _SketchTex;
            float _RevealProgress;
            float _SketchIntensity;
            float _ToonThreshold;

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionHCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                // Reveal progress
                if (i.uv.x > _RevealProgress)
                    discard;

                // Semplice toon step basato su uv.y per variare colore (puoi cambiare in base a necessità)
                float toonStep = step(_ToonThreshold, i.uv.y);
                float3 baseColor = lerp(_Color1.rgb, _Color2.rgb, toonStep);

                // Sketch pattern (linee scure)
                half4 sketchCol = tex2D(_SketchTex, i.uv);

                // Mix tra baseColor e sketch pattern (moltiplichiamo per sketchCol.r per intensità linee)
                float3 finalColor = lerp(baseColor, baseColor * sketchCol.r, _SketchIntensity);

                // Alpha piena (1) per ora
                float alpha = 1.0;

                return half4(finalColor, alpha);
            }
            ENDHLSL
        }
    }
}
