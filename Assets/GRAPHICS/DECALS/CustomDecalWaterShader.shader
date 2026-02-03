Shader "Custom/WaterPuddle"
{
    Properties
    {
        _BaseMap ("Albedo (Puddle Mask in Alpha)", 2D) = "white" {}
        _NormalMap ("Water Normal Map", 2D) = "bump" {}
        _RoughnessMap ("Water Roughness", 2D) = "white" {}
        _MaskTexture ("Puddle Shape Mask", 2D) = "white" {}
        _WaterAmount ("Water Amount (Size)", Range(0.0, 1.0)) = 1.0
        _WaterColor ("Water Color", Color) = (0.2, 0.3, 0.4, 1.0)
        _Intensity ("Normal Intensity", Range(0.0, 5.0)) = 1.0
        _Smoothness ("Smoothness", Range(0.0, 1.0)) = 0.9
        _EdgeSoftness ("Edge Softness", Range(0.0, 0.5)) = 0.1
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" }
        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float4 tangentWS : TEXCOORD2;
                float2 uv : TEXCOORD3;
                float fogFactor : TEXCOORD4;
                #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                    float4 shadowCoord : TEXCOORD5;
                #endif
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_BaseMap);        SAMPLER(sampler_BaseMap);
            TEXTURE2D(_NormalMap);      SAMPLER(sampler_NormalMap);
            TEXTURE2D(_RoughnessMap);   SAMPLER(sampler_RoughnessMap);
            TEXTURE2D(_MaskTexture);    SAMPLER(sampler_MaskTexture);

            CBUFFER_START(UnityPerMaterial)
                float4 _NormalMap_ST;
                float4 _MaskTexture_ST;
                float _WaterAmount;
                float4 _WaterColor;
                float _Intensity;
                float _Smoothness;
                float _EdgeSoftness;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT = (Varyings)0;
                
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                VertexPositionInputs vertexInput = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(IN.normalOS, IN.tangentOS);

                OUT.positionWS = vertexInput.positionWS;
                OUT.normalWS = normalInput.normalWS;
                OUT.tangentWS = float4(normalInput.tangentWS, IN.tangentOS.w);
                OUT.positionCS = vertexInput.positionCS;
                OUT.uv = TRANSFORM_TEX(IN.uv, _NormalMap);
                OUT.fogFactor = ComputeFogFactor(vertexInput.positionCS.z);

                #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                    OUT.shadowCoord = GetShadowCoord(vertexInput);
                #endif

                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                // Sample mask texture to determine puddle shape
                half mask = SAMPLE_TEXTURE2D(_MaskTexture, sampler_MaskTexture, IN.uv).r;
                
                // Create dynamic puddle edge based on WaterAmount slider
                // This creates a smooth transition that grows/shrinks the puddle
                half puddle = smoothstep(_WaterAmount - _EdgeSoftness, _WaterAmount + _EdgeSoftness, mask);
                
                // Early discard for completely transparent areas (optimization)
                clip(puddle - 0.001);

                // Sample textures
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);
                half4 normalSample = SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, IN.uv);
                half3 normalTS = UnpackNormal(normalSample);
                normalTS.xy *= _Intensity;
                
                half roughness = SAMPLE_TEXTURE2D(_RoughnessMap, sampler_RoughnessMap, IN.uv).r;

                // Calculate tangent to world matrix
                half3 bitangentWS = IN.tangentWS.w * cross(IN.normalWS, IN.tangentWS.xyz);
                half3x3 tangentToWorld = half3x3(IN.tangentWS.xyz, bitangentWS, IN.normalWS);
                half3 normalWS = normalize(mul(normalTS, tangentToWorld));

                // Setup InputData
                InputData lightingInput = (InputData)0;
                lightingInput.positionWS = IN.positionWS;
                lightingInput.normalWS = normalWS;
                lightingInput.viewDirectionWS = SafeNormalize(GetWorldSpaceViewDir(IN.positionWS));
                
                #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                    lightingInput.shadowCoord = IN.shadowCoord;
                #elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                    lightingInput.shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                #else
                    lightingInput.shadowCoord = float4(0, 0, 0, 0);
                #endif
                
                lightingInput.fogCoord = IN.fogFactor;
                lightingInput.bakedGI = 0;
                lightingInput.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(IN.positionCS);
                lightingInput.shadowMask = 0;

                // Setup SurfaceData
                SurfaceData surface = (SurfaceData)0;
                surface.albedo = _WaterColor.rgb * albedo.rgb;
                surface.metallic = 0.0;
                surface.smoothness = lerp(1.0 - roughness, _Smoothness, 0.5);
                surface.normalTS = normalTS;
                surface.occlusion = 1.0;
                surface.emission = 0;
                surface.alpha = puddle * _WaterColor.a;
                surface.specular = 0;

                half4 color = UniversalFragmentPBR(lightingInput, surface);
                color.rgb = MixFog(color.rgb, lightingInput.fogCoord);
                color.a = surface.alpha;
                
                return color;
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            float3 _LightDirection;

            Varyings ShadowPassVertex(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

                output.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));
                return output;
            }

            half4 ShadowPassFragment(Varyings input) : SV_TARGET
            {
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings DepthOnlyVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 DepthOnlyFragment(Varyings input) : SV_TARGET
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}