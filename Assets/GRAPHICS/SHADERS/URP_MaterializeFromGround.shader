Shader "Universal Render Pipeline/MaterializeFromGround"
{
    Properties
    {
        _BaseMap ("Base Texture", 2D) = "white" {}
        [HDR] _BaseColor ("Color", Color) = (0, 1, 1, 1)
        _Progress ("Materialization Progress", Range(0, 1)) = 0
        _GroundLevel ("Ground Y Position", Float) = 0
        _Height ("Total Height", Float) = 5
        _EdgeWidth ("Edge Width", Range(0.01, 2)) = 0.3
        [HDR] _EdgeColor ("Edge Color", Color) = (0, 2, 2, 1)
        _DissolveAmount ("Dissolve Amount", Range(0, 1)) = 0.2
        [Enum(Dissolve,0,Scanline,1,Hologram,2)] _EffectStyle ("Effect Style", Float) = 0
    }
    
    SubShader
    {
        Tags 
        { 
            "RenderType" = "Transparent" 
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }
        
        LOD 100
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };
            
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float3 viewDirWS : TEXCOORD3;
                float fogFactor : TEXCOORD4;
            };
            
            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float4 _EdgeColor;
                float _Progress;
                float _GroundLevel;
                float _Height;
                float _EdgeWidth;
                float _DissolveAmount;
                float _EffectStyle;
            CBUFFER_END
            
            // Hash function for noise
            float hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }
            
            Varyings vert(Attributes input)
            {
                Varyings output;
                
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS);
                
                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.normalWS = normalInput.normalWS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.viewDirWS = GetWorldSpaceViewDir(vertexInput.positionWS);
                output.fogFactor = ComputeFogFactor(vertexInput.positionCS.z);
                
                return output;
            }
            
            half4 frag(Varyings input) : SV_Target
            {
                // Calculate height progress
                float currentHeight = _GroundLevel + (_Height * _Progress);
                float heightDiff = input.positionWS.y - currentHeight;
                
                // Discard pixels above the materialization line
                if (heightDiff > 0)
                {
                    discard;
                }
                
                // Calculate reveal factor (0 = just appearing, 1 = fully materialized)
                float reveal = saturate(-heightDiff / _EdgeWidth);
                
                // Base texture and color
                half4 texColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half4 baseColor = texColor * _BaseColor;
                
                // Fresnel effect for rim lighting
                float3 viewDirWS = normalize(input.viewDirWS);
                float3 normalWS = normalize(input.normalWS);
                float fresnel = pow(1.0 - saturate(dot(viewDirWS, normalWS)), 3.0);
                
                half4 finalColor = baseColor;
                float alpha = 1.0;
                
                // Style 0: Dissolve
                if (_EffectStyle < 0.5)
                {
                    float noise = hash(input.positionWS.xz * 10.0 + input.positionWS.y);
                    float dissolve = smoothstep(1.0 - reveal - _DissolveAmount, 1.0 - reveal, noise);
                    
                    alpha = dissolve;
                    finalColor = lerp(_EdgeColor, baseColor, dissolve);
                    finalColor.rgb += fresnel * _BaseColor.rgb * 0.5;
                }
                // Style 1: Scanlines
                else if (_EffectStyle < 1.5)
                {
                    float scanline = sin(input.positionWS.y * 50.0 + _Time.y * 5.0) * 0.5 + 0.5;
                    float edgeGlow = 1.0 - reveal;
                    
                    finalColor.rgb = baseColor.rgb * (0.7 + scanline * 0.3);
                    finalColor.rgb += edgeGlow * _EdgeColor.rgb * 2.0;
                    finalColor.rgb += fresnel * _BaseColor.rgb * 0.3;
                }
                // Style 2: Hologram
                else
                {
                    float flicker = sin(_Time.y * 10.0 + input.positionWS.y * 20.0) * 0.1 + 0.9;
                    float grid = step(0.9, frac(input.positionWS.y * 15.0)) + step(0.9, frac(input.positionWS.x * 15.0));
                    float edgeGlow = 1.0 - reveal;
                    
                    finalColor.rgb = baseColor.rgb * flicker;
                    finalColor.rgb += grid * _BaseColor.rgb * 0.3;
                    finalColor.rgb += edgeGlow * _EdgeColor.rgb * 3.0;
                    finalColor.rgb += fresnel * _BaseColor.rgb * 0.8;
                    alpha = 0.8 + fresnel * 0.2;
                }
                
                finalColor.a *= alpha;
                finalColor.rgb = MixFog(finalColor.rgb, input.fogFactor);
                
                return finalColor;
            }
            ENDHLSL
        }
    }
    
    FallBack "Universal Render Pipeline/Lit"
}
