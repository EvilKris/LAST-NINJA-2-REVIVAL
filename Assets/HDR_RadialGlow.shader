Shader "UI/HDR_RadialGlow"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        [HDR] _GlowColor ("Glow Color", Color) = (0,1,1,1)
        _GlowIntensity ("Glow Intensity", Range(0, 5)) = 0
        _PulseSpeed ("Pulse Speed", Float) = 2.0

        // Required for UI Masking
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        
        Stencil {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off Lighting Off ZWrite Off ZTest [fixed] Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord  : TEXCOORD0;
            };

            sampler2D _MainTex;
            fixed4 _GlowColor;
            float _GlowIntensity;
            float _PulseSpeed;

            v2f vert(appdata_t IN) {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target {
                half4 tex = tex2D(_MainTex, IN.texcoord);
                
                // Calculate Pulse (Subtle breathing effect)
                float pulse = 1.0 + (sin(_Time.y * _PulseSpeed) * 0.1);
                
                // Combine texture color with HDR Glow
                // The _GlowIntensity multiplier pushes the color into Bloom range
                half3 finalRGB = tex.rgb * IN.color.rgb;
                finalRGB += (finalRGB * _GlowColor.rgb * _GlowIntensity * pulse);

                return half4(finalRGB, tex.a * IN.color.a);
            }
            ENDCG
        }
    }
}