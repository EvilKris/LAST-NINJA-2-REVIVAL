Shader "Unlit/ScrollingGradientShader"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _ScrollAngle ("Scroll Angle", Range(0, 360)) = 45
        _ScrollSpeed ("Scroll Speed", Float) = 1.0
        _Color1 ("Color 1", Color) = (1, 0, 0, 1)
        _Color2 ("Color 2", Color) = (1, 1, 0, 1)
        _Color3 ("Color 3", Color) = (0, 1, 0, 1)
        _Color4 ("Color 4", Color) = (0, 0, 1, 1)
        // stencil for (UI) Masking
		_StencilComp ("Stencil Comparison", Float) = 8
		_Stencil ("Stencil ID", Float) = 0
		_StencilOp ("Stencil Operation", Float) = 0
		_StencilWriteMask ("Stencil Write Mask", Float) = 255
		_StencilReadMask ("Stencil Read Mask", Float) = 255
		_ColorMask ("Color Mask", Float) = 15
    }
    SubShader
    {
        Tags { "Queue"="Transparent" 
            "IgnoreProjector"="True" 
            "RenderType"="Transparent" 
			"PreviewType"="Plane"
			"CanUseSpriteAtlas"="True" }
        LOD 100


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
		Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM

            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            #pragma multi_compile_fog

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;                
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _ScrollAngle;
            float _ScrollSpeed;
            float4 _Color1;
            float4 _Color2;
            float4 _Color3;
            float4 _Color4;


            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Convert angle to radians
                float angleRad = radians(_ScrollAngle);
                float2 direction = float2(cos(angleRad), sin(angleRad));
                
                // Calculate scrolling UV with directional movement
                float scroll = dot(i.uv, direction) + _Time.y * _ScrollSpeed;
                
                // Create a gradient value that cycles 0->1->0
                float t = frac(scroll);
                
                // Blend between the four colors based on scroll position
                float3 color;
                if (t < 0.25)
                {
                    color = lerp(_Color1.rgb, _Color2.rgb, t * 4.0);
                }
                else if (t < 0.5)
                {
                    color = lerp(_Color2.rgb, _Color3.rgb, (t - 0.25) * 4.0);
                }
                else if (t < 0.75)
                {
                    color = lerp(_Color3.rgb, _Color4.rgb, (t - 0.5) * 4.0);
                }
                else
                {
                    color = lerp(_Color4.rgb, _Color1.rgb, (t - 0.75) * 4.0);
                }
        
	            return float4(color, 1.0);
            }
            ENDCG
        }
    }
}
