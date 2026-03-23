Shader "Hidden/Post-processing Custom/GrayScale"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "GrayScale"
            ZTest Always Cull Off ZWrite Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D_X(_MainTexture);
            SAMPLER(sampler_BlitTexture);
            float4 _MainTexture_TexelSize;
            float _Weight;

            struct Varyings { float4 positionHCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings Vert(uint id : SV_VertexID)
            {
                Varyings o;
                o.positionHCS = GetFullScreenTriangleVertexPosition(id);
                o.uv          = GetFullScreenTriangleTexCoord(id);
                return o;
            }

            half3 ApplyGrayScale(half3 color)
            {
                half gray_color = dot(color.rgb, half3(0.299, 0.587, 0.114));
                return lerp(color.rgb, gray_color.xxx, _Weight);
            }

            half4 Frag(Varyings i) : SV_Target
            {
                half4 col = SAMPLE_TEXTURE2D_X(_MainTexture, sampler_LinearClamp, i.uv);
                col.rgb = ApplyGrayScale(col.rgb);
                return col;
            }
            ENDHLSL
        }
    }
}
