Shader "Hidden/URP/MaterializeWaveEffect"
{
    Properties
    {
        _MainTex ("Base (RGB)", 2D) = "white" {}
    }
    
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        LOD 100
        ZTest Always
        ZWrite Off
        Cull Off
        
        Pass
        {
            Name "MaterializeWavePass"
            
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment frag
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            
            CBUFFER_START(UnityPerMaterial)
                float _Progress;
                float _WaveWidth;
                float _NearDistance;
                float _FarDistance;
                float4 _EdgeColor;
                float _EffectStyle;
            CBUFFER_END
            
            // Hash function for noise
            float hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }
            
            half4 frag(Varyings input) : SV_Target
            {
                // Sample the scene color
                half4 sceneColor = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, input.texcoord);
                
                // Sample depth
                float depth = SampleSceneDepth(input.texcoord);
                float linearDepth = LinearEyeDepth(depth, _ZBufferParams);
                
                // Normalize depth to 0-1 range
                float depthNorm = saturate((linearDepth - _NearDistance) / (_FarDistance - _NearDistance));
                
                // Calculate wave edge
                float waveEdge = _WaveWidth / (_FarDistance - _NearDistance);
                
                // Calculate reveal (inverted so near objects appear first)
                float reveal = smoothstep(_Progress, _Progress + waveEdge, depthNorm);
                
                // If pixel hasn't materialized yet, make it black
                if (reveal > 0.99)
                {
                    return half4(0, 0, 0, 1);
                }
                
                half4 finalColor = sceneColor;
                
                // Style 0: Dissolve
                if (_EffectStyle < 0.5)
                {
                    float noise = hash(input.texcoord * 1000.0 + linearDepth);
                    float dissolve = smoothstep(1.0 - reveal - 0.2, 1.0 - reveal, noise);
                    
                    finalColor = lerp(_EdgeColor, sceneColor, dissolve);
                    finalColor.a = dissolve;
                }
                // Style 1: Scanlines
                else if (_EffectStyle < 1.5)
                {
                    float scanline = sin(input.texcoord.y * 500.0 + _Time.y * 5.0) * 0.5 + 0.5;
                    float edgeGlow = smoothstep(_Progress, _Progress + waveEdge * 0.5, depthNorm);
                    
                    finalColor.rgb = sceneColor.rgb * (0.7 + scanline * 0.3);
                    finalColor.rgb += edgeGlow * _EdgeColor.rgb * 2.0;
                }
                // Style 2: Hologram
                else
                {
                    float flicker = sin(_Time.y * 10.0 + input.texcoord.y * 20.0) * 0.1 + 0.9;
                    float gridX = step(0.98, frac(input.texcoord.x * 100.0));
                    float gridY = step(0.98, frac(input.texcoord.y * 100.0));
                    float grid = gridX + gridY;
                    float edgeGlow = smoothstep(_Progress, _Progress + waveEdge * 0.3, depthNorm);
                    
                    finalColor.rgb = sceneColor.rgb * flicker;
                    finalColor.rgb += grid * _EdgeColor.rgb * 0.3;
                    finalColor.rgb += edgeGlow * _EdgeColor.rgb * 3.0;
                }
                
                return finalColor;
            }
            ENDHLSL
        }
    }
}
