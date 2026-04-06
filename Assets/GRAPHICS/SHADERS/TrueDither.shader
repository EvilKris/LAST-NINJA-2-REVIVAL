Shader "Custom/DitherFade"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)
        _DitherFade ("Dither Fade", Range(0,1)) = 1
        [Toggle] _Horizontal ("Horizontal Dither", Float) = 0
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
            #pragma shader_feature _HORIZONTAL_ON
            
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
                float4 _Color;
                float _DitherFade;
            CBUFFER_END
            
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
                half4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv) * _Color;
                
                // Get screen pixel position
                float2 screenPos = IN.screenPos.xy / IN.screenPos.w * _ScreenParams.xy;
                
                #ifdef _HORIZONTAL_ON
                    int x = int(screenPos.y) & 3;
                    int y = int(screenPos.x) & 3;
                #else
                    int x = int(screenPos.x) & 3;
                    int y = int(screenPos.y) & 3;
                #endif
                
                float threshold = (float((x + y * 4 + 1) * 16 % 64)) / 64.0;
                
                // Discard pixels based on fade value
                clip(_DitherFade - threshold);
                
                return col;
            }
            ENDHLSL
        }
    }
}