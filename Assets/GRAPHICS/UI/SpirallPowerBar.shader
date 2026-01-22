Shader "UI/SquareSpiralSlider"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _FilledColor ("Filled Color", Color) = (0,1,0,1)
        _EmptyColor ("Empty Color", Color) = (0.3,0.3,0.3,1)
        _FillAmount ("Fill Amount", Range(0, 1)) = 0.5
        _SpiralScale ("Spiral Scale", Float) = 8.0
        _LineWidth ("Line Width", Range(0, 1)) = 0.4
        
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            sampler2D _MainTex;
            float4 _FilledColor;
            float4 _EmptyColor;
            float _FillAmount;
            float _SpiralScale;
            float _LineWidth;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            float GetSpiralDistance(float2 uv)
            {
                // Center coordinates (range -1 to 1)
                // Add a tiny offset to avoid floating-point precision issues at exactly 0
                float2 p = (uv * 2.0 - 1.0);
                
                // Add small epsilon to avoid artifacts at zero crossings
                p += 0.0001;
                
                // Calculate Chebyshev distance for square rings
                float dist = max(abs(p.x), abs(p.y));
                
                // Scale by spiral scale
                dist *= _SpiralScale;
                
                return dist;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float spiralDist = GetSpiralDistance(i.uv);
                
                // Normalize distance to 0-1 range based on max rings
                float maxRings = _SpiralScale;
                float normalizedDist = spiralDist / maxRings;
                
                // Determine if this pixel is in a "line" or "gap"
                // Use smooth modulo to avoid hard edges
                float ringMod = frac(spiralDist * 0.5) * 2.0;
                float isLine = step(ringMod, _LineWidth * 2.0);
                
                // Check if filled based on distance
                float isFilled = step(normalizedDist, _FillAmount);
                
                // Blend colors
                fixed4 lineColor = lerp(_EmptyColor, _FilledColor, isFilled);
                fixed4 finalColor = lerp(fixed4(0,0,0,0), lineColor, isLine);
                
                finalColor *= i.color;
                
                return finalColor;
            }
            ENDCG
        }
    }
}