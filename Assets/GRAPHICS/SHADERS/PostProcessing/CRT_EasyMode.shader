Shader "Hidden/Post-processing Custom/CRT_EasyMode"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "CRT_EasyMode"
            ZTest Always Cull Off ZWrite Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D_X(_MainTexture);
            float4 _MainTexture_TexelSize;

            // CRT Parameters
            float _SharpnessH;
            float _SharpnessV;
            float _MaskStrength;
            float _MaskDotWidth;
            float _MaskDotHeight;
            float _MaskStagger;
            float _MaskSize;
            float _ScanlineStrength;
            float _ScanlineBeamWidthMin;
            float _ScanlineBeamWidthMax;
            float _ScanlineBrightMin;
            float _ScanlineBrightMax;
            float _ScanlineCutoff;
            float _GammaInput;
            float _GammaOutput;
            float _BrightBoost;
            float _Dilation;

            #define FIX(c) max(abs(c), 1e-5)
            #ifndef PI
            #define PI 3.141592653589
            #endif

            struct Varyings 
            { 
                float4 positionHCS : SV_POSITION; 
                float2 uv : TEXCOORD0; 
            };

            Varyings Vert(uint id : SV_VertexID)
            {
                Varyings o;
                o.positionHCS = GetFullScreenTriangleVertexPosition(id);
                o.uv = GetFullScreenTriangleTexCoord(id);
                return o;
            }

            float4 dilate(float4 col)
            {
                float4 x = lerp(float4(1.0, 1.0, 1.0, 1.0), col, _Dilation);
                return col * x;
            }

            float curve_distance(float x, float sharp)
            {
                float x_step = step(0.5, x);
                float curve = 0.5 - sqrt(0.25 - (x - x_step) * (x - x_step)) * sign(0.5 - x);
                return lerp(x, curve, sharp);
            }

            float4x4 get_color_matrix(float2 co, float2 dx)
            {
                float4 c1 = dilate(SAMPLE_TEXTURE2D_X(_MainTexture, sampler_LinearClamp, co - dx));
                float4 c2 = dilate(SAMPLE_TEXTURE2D_X(_MainTexture, sampler_LinearClamp, co));
                float4 c3 = dilate(SAMPLE_TEXTURE2D_X(_MainTexture, sampler_LinearClamp, co + dx));
                float4 c4 = dilate(SAMPLE_TEXTURE2D_X(_MainTexture, sampler_LinearClamp, co + 2.0 * dx));
                return float4x4(c1, c2, c3, c4);
            }

            float3 filter_lanczos(float4 coeffs, float4x4 color_matrix)
            {
                float4 col = mul(coeffs, color_matrix);
                float4 sample_min = min(color_matrix[1], color_matrix[2]);
                float4 sample_max = max(color_matrix[1], color_matrix[2]);
                col = clamp(col, sample_min, sample_max);
                return col.rgb;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                float2 texture_size = _MainTexture_TexelSize.zw;
                float2 output_size = _ScreenParams.xy;
                
                float2 dx = float2(1.0 / texture_size.x, 0.0);
                float2 dy = float2(0.0, 1.0 / texture_size.y);
                float2 pix_co = uv * texture_size - float2(0.5, 0.5);
                float2 tex_co = (floor(pix_co) + float2(0.5, 0.5)) / texture_size;
                float2 dist = frac(pix_co);
                
                float curve_x;
                float3 col, col2;

                // Lanczos filtering
                curve_x = curve_distance(dist.x, _SharpnessH * _SharpnessH);
                float4 coeffs = PI * float4(1.0 + curve_x, curve_x, 1.0 - curve_x, 2.0 - curve_x);
                coeffs = FIX(coeffs);
                coeffs = 2.0 * sin(coeffs) * sin(coeffs / 2.0) / (coeffs * coeffs);
                coeffs /= dot(coeffs, float4(1.0, 1.0, 1.0, 1.0));

                col = filter_lanczos(coeffs, get_color_matrix(tex_co, dx));
                col2 = filter_lanczos(coeffs, get_color_matrix(tex_co + dy, dx));

                col = lerp(col, col2, curve_distance(dist.y, _SharpnessV));
                col = pow(max(col, 0.0), float3(_GammaInput / (_Dilation + 1.0), _GammaInput / (_Dilation + 1.0), _GammaInput / (_Dilation + 1.0)));

                // Scanlines - Classic 80s arcade style
                // Calculate which scanline we're on
                float scanline = sin(uv.y * texture_size.y * PI);
                
                // Create sharp scanlines
                float scanline_intensity = scanline * scanline; // Squared for sharper lines
                
                // Mix between dark scanlines and bright areas
                float scan_weight = lerp(_ScanlineBrightMin, 1.0, scanline_intensity);
                
                // Apply scanline strength
                scan_weight = lerp(1.0, scan_weight, _ScanlineStrength);

                // RGB Mask
                float mask = 1.0 - _MaskStrength;
                float2 mod_fac = floor(uv * output_size * texture_size / (texture_size * float2(_MaskSize, _MaskDotHeight * _MaskSize)));
                int dot_no = int(fmod((mod_fac.x + fmod(mod_fac.y, 2.0) * _MaskStagger) / _MaskDotWidth, 3.0));
                float3 mask_weight;

                if (dot_no == 0) mask_weight = float3(1.0, mask, mask);
                else if (dot_no == 1) mask_weight = float3(mask, 1.0, mask);
                else mask_weight = float3(mask, mask, 1.0);

                // Apply scanlines and RGB mask
                col *= scan_weight;
                col *= mask_weight;
                col = pow(max(col, 0.0), float3(1.0 / _GammaOutput, 1.0 / _GammaOutput, 1.0 / _GammaOutput));

                return float4(col * _BrightBoost, 1.0);
            }
            ENDHLSL
        }
    }
}
