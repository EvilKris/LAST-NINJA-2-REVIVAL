Shader "URP/BigRockShader"
{
    Properties
    {
        _ProjectionTexture_BASE("Projection Texture BASE", 2D) = "white" {}
        _ProjectionTexture_NORM("Projection Texture NORM", 2D) = "bump" {}
        _ProjectionTexture_METL("Projection Texture METL", 2D) = "white" {}
        _ObjectNormals("Object Normals", 2D) = "bump" {}
        _ObjectColor("Object Color", 2D) = "white" {}
        _OverTextureTile("Over Texture Tile", Vector) = (1, 1, 0, 0)
        _Distance("Distance", Range(0, 2000)) = 15
        _Falloff("Falloff", Range(0, 2000)) = 0.25
        
        // URP Required Properties
        [HideInInspector] _Surface("__surface", Float) = 0.0
        [HideInInspector] _Blend("__blend", Float) = 0.0
        [HideInInspector] _AlphaClip("__clip", Float) = 0.0
        [HideInInspector] _SrcBlend("__src", Float) = 1.0
        [HideInInspector] _DstBlend("__dst", Float) = 0.0
        [HideInInspector] _ZWrite("__zw", Float) = 1.0
        [HideInInspector] _Cull("__cull", Float) = 2.0
    }

    SubShader
    {
        Tags 
        { 
            "RenderType" = "Opaque" 
            "RenderPipeline" = "UniversalPipeline" 
            "Queue" = "Geometry"
        }
        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 4.5

            // URP Keywords
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile_fragment _ _LIGHT_LAYERS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DYNAMICLIGHTMAP_ON
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #pragma vertex LitPassVertex
            #pragma fragment LitPassFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float4 tangentOS    : TANGENT;
                float2 texcoord     : TEXCOORD0;
                float2 lightmapUV   : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float2 uv                       : TEXCOORD0;
                float3 positionWS               : TEXCOORD1;
                float3 normalWS                 : TEXCOORD2;
                float4 tangentWS                : TEXCOORD3;
                float3 viewDirWS                : TEXCOORD4;
                #ifdef REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR
                    float4 shadowCoord          : TEXCOORD5;
                #endif
                float fogFactor                 : TEXCOORD6;
                DECLARE_LIGHTMAP_OR_SH(lightmapUV, vertexSH, 7);
                float4 positionCS               : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _ProjectionTexture_BASE_ST;
                float4 _ObjectColor_ST;
                float4 _ProjectionTexture_NORM_ST;
                float4 _ObjectNormals_ST;
                float4 _ProjectionTexture_METL_ST;
                float2 _OverTextureTile;
                float _Distance;
                float _Falloff;
            CBUFFER_END

            TEXTURE2D(_ProjectionTexture_BASE);    SAMPLER(sampler_ProjectionTexture_BASE);
            TEXTURE2D(_ProjectionTexture_NORM);    SAMPLER(sampler_ProjectionTexture_NORM);
            TEXTURE2D(_ProjectionTexture_METL);    SAMPLER(sampler_ProjectionTexture_METL);
            TEXTURE2D(_ObjectNormals);             SAMPLER(sampler_ObjectNormals);
            TEXTURE2D(_ObjectColor);               SAMPLER(sampler_ObjectColor);

            // Voronoi noise function
            float2 VoronoiHash(float2 p)
            {
                p = float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)));
                return frac(sin(p) * 43758.5453);
            }

            float Voronoi(float2 v, float time)
            {
                float2 n = floor(v);
                float2 f = frac(v);
                float F1 = 8.0;
                
                for (int j = -1; j <= 1; j++)
                {
                    for (int i = -1; i <= 1; i++)
                    {
                        float2 g = float2(i, j);
                        float2 o = VoronoiHash(n + g);
                        o = (sin(time + o * 6.2831) * 0.5 + 0.5);
                        float2 r = f - g - o;
                        float d = 0.5 * dot(r, r);
                        F1 = min(F1, d);
                    }
                }
                return F1;
            }

            // Triplanar sampling
            float4 TriplanarSample(TEXTURE2D_PARAM(tex, samplerTex), float3 worldPos, float3 worldNormal, float2 tiling, float4 st)
            {
                float3 projNormal = pow(abs(worldNormal), 1.0);
                projNormal /= (projNormal.x + projNormal.y + projNormal.z) + 0.00001;
                float3 nsign = sign(worldNormal);
                
                float4 xSample = SAMPLE_TEXTURE2D(tex, samplerTex, tiling * worldPos.zy * float2(nsign.x, 1.0) * st.xy + st.zw);
                float4 ySample = SAMPLE_TEXTURE2D(tex, samplerTex, tiling * worldPos.xz * float2(nsign.y, 1.0) * st.xy + st.zw);
                float4 zSample = SAMPLE_TEXTURE2D(tex, samplerTex, tiling * worldPos.xy * float2(-nsign.z, 1.0) * st.xy + st.zw);
                
                return xSample * projNormal.x + ySample * projNormal.y + zSample * projNormal.z;
            }

            float3 TriplanarNormal(TEXTURE2D_PARAM(tex, samplerTex), float3 worldPos, float3 worldNormal, float2 tiling, float4 st)
            {
                float3 projNormal = pow(abs(worldNormal), 1.0);
                projNormal /= (projNormal.x + projNormal.y + projNormal.z) + 0.00001;
                float3 nsign = sign(worldNormal);
                
                float3 xNorm = UnpackNormal(SAMPLE_TEXTURE2D(tex, samplerTex, tiling * worldPos.zy * float2(nsign.x, 1.0) * st.xy + st.zw));
                float3 yNorm = UnpackNormal(SAMPLE_TEXTURE2D(tex, samplerTex, tiling * worldPos.xz * float2(nsign.y, 1.0) * st.xy + st.zw));
                float3 zNorm = UnpackNormal(SAMPLE_TEXTURE2D(tex, samplerTex, tiling * worldPos.xy * float2(-nsign.z, 1.0) * st.xy + st.zw));
                
                xNorm = float3(xNorm.xy * float2(nsign.x, 1.0) + worldNormal.zy, worldNormal.x).zyx;
                yNorm = float3(yNorm.xy * float2(nsign.y, 1.0) + worldNormal.xz, worldNormal.y).xzy;
                zNorm = float3(zNorm.xy * float2(-nsign.z, 1.0) + worldNormal.xy, worldNormal.z).xyz;
                
                return normalize(xNorm * projNormal.x + yNorm * projNormal.y + zNorm * projNormal.z);
            }

            Varyings LitPassVertex(Attributes input)
            {
                Varyings output = (Varyings)0;

                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);

                output.uv = input.texcoord;
                output.positionWS = vertexInput.positionWS;
                output.normalWS = normalInput.normalWS;
                output.tangentWS = float4(normalInput.tangentWS, input.tangentOS.w);
                output.viewDirWS = GetWorldSpaceViewDir(vertexInput.positionWS);
                output.fogFactor = ComputeFogFactor(vertexInput.positionCS.z);
                output.positionCS = vertexInput.positionCS;

                #ifdef REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR
                    output.shadowCoord = GetShadowCoord(vertexInput);
                #endif

                OUTPUT_LIGHTMAP_UV(input.lightmapUV, unity_LightmapST, output.lightmapUV);
                OUTPUT_SH(output.normalWS.xyz, output.vertexSH);

                return output;
            }

            half4 LitPassFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                // Distance-based blend factor
                float3 viewDir = input.positionWS - _WorldSpaceCameraPos;
                float distFactor = dot(viewDir / _Distance, viewDir / _Distance);
                float blendFactor = saturate(pow(saturate(distFactor), _Falloff));
                blendFactor = saturate(lerp(-2.0, 3.0, blendFactor));

                // Triplanar base color
                float4 triplanarBase = TriplanarSample(TEXTURE2D_ARGS(_ProjectionTexture_BASE, sampler_ProjectionTexture_BASE), 
                                                       input.positionWS, input.normalWS, _OverTextureTile, _ProjectionTexture_BASE_ST);

                // Voronoi noise for detail blending
                float voronoi = Voronoi(input.uv * 50.0, 1.71);
                float voronoiBlend = voronoi * 5.0;

                // Object color with voronoi blend
                float4 objectColor = SAMPLE_TEXTURE2D(_ObjectColor, sampler_ObjectColor, input.uv * _ObjectColor_ST.xy + _ObjectColor_ST.zw);
                float4 baseColor = lerp(triplanarBase, objectColor, voronoiBlend);
                
                // Final base color with distance blend
                baseColor = lerp(triplanarBase, baseColor, blendFactor);

                // Normals
                float3 bitangentWS = cross(input.normalWS, input.tangentWS.xyz) * input.tangentWS.w;
                float3x3 tangentToWorld = float3x3(input.tangentWS.xyz, bitangentWS, input.normalWS);
                
                float3 triplanarNormal = TriplanarNormal(TEXTURE2D_ARGS(_ProjectionTexture_NORM, sampler_ProjectionTexture_NORM), 
                                                         input.positionWS, input.normalWS, _OverTextureTile, _ProjectionTexture_NORM_ST);
                float3 tangentNormal = mul(transpose(tangentToWorld), triplanarNormal);
                
                float3 objectNormal = UnpackNormal(SAMPLE_TEXTURE2D(_ObjectNormals, sampler_ObjectNormals, 
                                                    input.uv * _ObjectNormals_ST.xy + _ObjectNormals_ST.zw));
                float3 finalNormalTS = lerp(tangentNormal, objectNormal, blendFactor);
                float3 normalWS = TransformTangentToWorld(finalNormalTS, tangentToWorld);

                // Metallic and Smoothness
                float4 metallicMap = TriplanarSample(TEXTURE2D_ARGS(_ProjectionTexture_METL, sampler_ProjectionTexture_METL), 
                                                     input.positionWS, input.normalWS, _OverTextureTile, _ProjectionTexture_METL_ST);

                // Lighting setup
                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = normalize(normalWS);
                inputData.viewDirectionWS = SafeNormalize(input.viewDirWS);
                
                #ifdef REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR
                    inputData.shadowCoord = input.shadowCoord;
                #else
                    inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                #endif
                
                inputData.fogCoord = input.fogFactor;
                inputData.bakedGI = SAMPLE_GI(input.lightmapUV, input.vertexSH, inputData.normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowMask = SAMPLE_SHADOWMASK(input.lightmapUV);

                // Surface data
                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = baseColor.rgb;
                surfaceData.metallic = metallicMap.r;
                surfaceData.specular = 0;
                surfaceData.smoothness = metallicMap.a;
                surfaceData.normalTS = finalNormalTS;
                surfaceData.emission = 0;
                surfaceData.occlusion = 1;
                surfaceData.alpha = 1;

                // Calculate lighting
                half4 color = UniversalFragmentPBR(inputData, surfaceData);
                
                // Apply fog
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                
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

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderVariablesFunctions.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
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

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
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