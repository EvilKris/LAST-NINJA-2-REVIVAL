Shader "Custom/PhantomEffect"
{
    Properties
    {
        _MainTexture ("Main Texture", 2D) = "white" {}
        _EmissionPower ("Emission Power", Range(0, 10)) = 2
        _EmissionColor ("Emission Color", Color) = (0.5, 0.7, 1, 1)
        _EmissionColor2 ("Emission Color 2", Color) = (0.8, 0.9, 1, 1)
        _FresnelPower ("Fresnel Power", Range(0.5, 5)) = 2
    }
    
    SubShader
    {
        Tags 
        { 
            "RenderType"="Transparent" 
            "Queue"="Transparent"
            "RenderPipeline"="UniversalPipeline"
        }
        LOD 300
        
        Pass
        {
            Name "Universal Forward"
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
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
            };
            
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float3 tangentWS : TEXCOORD3;
                float3 viewDirectionWS : TEXCOORD4;
            };
            
            TEXTURE2D(_MainTexture);
            SAMPLER(sampler_MainTexture);
            
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTexture_ST;
                float _EmissionPower;
                float4 _EmissionColor;
                float4 _EmissionColor2;
                float _FresnelPower;
            CBUFFER_END
            
            Varyings vert(Attributes input)
            {
                Varyings output;
                
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                
                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.normalWS = normalInput.normalWS;
                output.tangentWS = normalInput.tangentWS;
                output.uv = TRANSFORM_TEX(input.uv, _MainTexture);
                
                // Pre-normalize view direction in vertex shader for better performance
                output.viewDirectionWS = normalize(GetWorldSpaceViewDir(vertexInput.positionWS));
                
                return output;
            }
            
            half4 frag(Varyings input) : SV_Target
            {
                // Sample main texture
                half4 mainTex = SAMPLE_TEXTURE2D(_MainTexture, sampler_MainTexture, input.uv);
                
                // Calculate fresnel effect (normalize once in vertex shader saves per-pixel work)
                half3 normalWS = normalize(input.normalWS);
                half3 viewDirWS = normalize(input.viewDirectionWS);
                half NdotV = saturate(dot(normalWS, viewDirWS));
                half fresnel = pow(1.0 - NdotV, _FresnelPower);
                
                // Optimized time-based animation
                // Remap sine from [-1,1] to [0,1] in one operation
                half remappedSine = sin(_Time.y) * 0.5 + 0.5;
                
                // Lerp between emission colors (RGB only, skip alpha)
                half3 emissionColor = lerp(_EmissionColor.rgb, _EmissionColor2.rgb, remappedSine);
                
                // Calculate final color in fewer operations
                // Combine emission calculation with fresnel
                half3 emission = emissionColor * (fresnel * _EmissionPower);
                
                // Combine base color and emission, use fresnel-based alpha
                half4 result;
                result.rgb = mainTex.rgb + emission;
                result.a = fresnel * mainTex.a;
                
                return result;
            }
            ENDHLSL
        }
    }
    
    Fallback "Hidden/Universal Render Pipeline/FallbackError"
}