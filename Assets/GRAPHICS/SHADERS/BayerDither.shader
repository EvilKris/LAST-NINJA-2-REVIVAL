Shader "Custom/BayerDither"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _DitherScale ("Dither Scale", Float) = 1.0
    }
    
    SubShader
    {
        Tags 
        { 
            "RenderType"="Opaque" 
            "RenderPipeline"="UniversalPipeline"
        }
        
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
                float4 screenPos : TEXCOORD1;
            };
            
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float _DitherScale;
            CBUFFER_END
            
            // 8x8 Bayer matrix
            static const float bayer8x8[64] = {
                 0, 32,  8, 40,  2, 34, 10, 42,
                48, 16, 56, 24, 50, 18, 58, 26,
                12, 44,  4, 36, 14, 46,  6, 38,
                60, 28, 52, 20, 62, 30, 54, 22,
                 3, 35, 11, 43,  1, 33,  9, 41,
                51, 19, 59, 27, 49, 17, 57, 25,
                15, 47,  7, 39, 13, 45,  5, 37,
                63, 31, 55, 23, 61, 29, 53, 21
            };
            
            float GetBayer(float2 screenPos)
            {
                int x = int(screenPos.x) % 8;
                int y = int(screenPos.y) % 8;
                return bayer8x8[y * 8 + x] / 64.0;
            }
            
            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.screenPos = ComputeScreenPos(OUT.positionHCS);
                return OUT;
            }
            
            half4 frag(Varyings IN) : SV_Target
            {
                // Sample texture
                half4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                
                // Convert to grayscale
                float gray = dot(col.rgb, float3(0.299, 0.587, 0.114));
                
                // Get screen position for dithering
                float2 screenPos = IN.screenPos.xy / IN.screenPos.w;
                screenPos *= _ScreenParams.xy * _DitherScale;
                
                // Get Bayer threshold
                float threshold = GetBayer(screenPos);
                
                // Apply dither
                float dithered = step(threshold, gray);
                
                return half4(dithered, dithered, dithered, 1.0);
            }
            ENDHLSL
        }
    }
}
