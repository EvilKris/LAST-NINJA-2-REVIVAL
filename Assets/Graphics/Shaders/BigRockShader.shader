Shader "URP/BigRockShader"
{
    Properties
    {
        _BaseMap ("Albedo", 2D) = "white" {}
        _NormalMap ("Normal", 2D) = "bump" {}
        _RoughnessMap ("Roughness (R)", 2D) = "white" {}

        _Tiling ("Tiling", Float) = 1
        _NormalStrength ("Normal Strength", Range(0,2)) = 1

        _TintA ("Tint A", Color) = (0.9,0.9,0.9,1)
        _TintB ("Tint B", Color) = (1.05,1.05,1.05,1)

        _NoiseScale ("Noise Scale", Float) = 0.2
        _NoiseStrength ("Noise Strength", Float) = 0.15
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag

            // URP Keywords
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 lightmapUV : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float  fogFactor  : TEXCOORD2;
                DECLARE_LIGHTMAP_OR_SH(lightmapUV, vertexSH, 3);
                #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                    float4 shadowCoord : TEXCOORD4;
                #endif
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_BaseMap);        SAMPLER(sampler_BaseMap);
            TEXTURE2D(_NormalMap);      SAMPLER(sampler_NormalMap);
            TEXTURE2D(_RoughnessMap);   SAMPLER(sampler_RoughnessMap);

            CBUFFER_START(UnityPerMaterial)
                float _Tiling;
                float _NormalStrength;
                float4 _TintA;
                float4 _TintB;
                float _NoiseScale;
                float _NoiseStrength;
            CBUFFER_END

            Varyings vert (Attributes IN)
            {
                Varyings OUT = (Varyings)0;
                
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                VertexPositionInputs vertexInput = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(IN.normalOS);

                OUT.positionWS = vertexInput.positionWS;
                OUT.normalWS = normalInput.normalWS;
                OUT.positionCS = vertexInput.positionCS;
                OUT.fogFactor = ComputeFogFactor(vertexInput.positionCS.z);

                #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                    OUT.shadowCoord = GetShadowCoord(vertexInput);
                #endif

                OUTPUT_LIGHTMAP_UV(IN.lightmapUV, unity_LightmapST, OUT.lightmapUV);
                OUTPUT_SH(OUT.normalWS.xyz, OUT.vertexSH);

                return OUT;
            }

            float4 TriplanarSample(TEXTURE2D_PARAM(tex, samp), float3 pos, float3 normal)
            {
                float3 n = abs(normal);
                n = pow(n, 4);
                n /= (n.x + n.y + n.z) + 0.00001;

                float2 uvX = pos.zy * _Tiling;
                float2 uvY = pos.xz * _Tiling;
                float2 uvZ = pos.xy * _Tiling;

                float4 x = SAMPLE_TEXTURE2D(tex, samp, uvX);
                float4 y = SAMPLE_TEXTURE2D(tex, samp, uvY);
                float4 z = SAMPLE_TEXTURE2D(tex, samp, uvZ);

                return x * n.x + y * n.y + z * n.z;
            }

            float Hash(float3 p)
            {
                return frac(sin(dot(p, float3(12.9898,78.233,37.719))) * 43758.5453);
            }

            half4 frag (Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                float3 normalWS = normalize(IN.normalWS);
                float3 posWS = IN.positionWS;

                float4 albedo = TriplanarSample(TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap), posWS, normalWS);
                float rough = TriplanarSample(TEXTURE2D_ARGS(_RoughnessMap, sampler_RoughnessMap), posWS, normalWS).r;

                // Per-object tint
                float seed = Hash(floor(UNITY_MATRIX_M._m03_m13_m23));
                float3 tint = lerp(_TintA.rgb, _TintB.rgb, seed);
                albedo.rgb *= tint;

                // Noise breakup
                float noise = Hash(posWS * _NoiseScale);
                albedo.rgb += noise * _NoiseStrength;

                // Setup InputData
                InputData lightingInput = (InputData)0;
                lightingInput.positionWS = posWS;
                lightingInput.normalWS = normalWS;
                lightingInput.viewDirectionWS = SafeNormalize(GetWorldSpaceViewDir(posWS));
                
                #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                    lightingInput.shadowCoord = IN.shadowCoord;
                #elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                    lightingInput.shadowCoord = TransformWorldToShadowCoord(posWS);
                #else
                    lightingInput.shadowCoord = float4(0, 0, 0, 0);
                #endif
                
                lightingInput.fogCoord = IN.fogFactor;
                lightingInput.bakedGI = SAMPLE_GI(IN.lightmapUV, IN.vertexSH, normalWS);
                lightingInput.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(IN.positionCS);
                lightingInput.shadowMask = SAMPLE_SHADOWMASK(IN.lightmapUV);

                // Setup SurfaceData
                SurfaceData surface = (SurfaceData)0;
                surface.albedo = saturate(albedo.rgb);
                surface.metallic = 0;
                surface.smoothness = 1.0 - rough;
                surface.normalTS = float3(0,0,1);
                surface.occlusion = 1;
                surface.emission = 0;
                surface.alpha = 1;
                surface.specular = 0;

                half4 color = UniversalFragmentPBR(lightingInput, surface);
                color.rgb = MixFog(color.rgb, lightingInput.fogCoord);
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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderVariablesFunctions.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
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
