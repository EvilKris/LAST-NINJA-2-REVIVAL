Shader "Custom/GhostTrailsAdditive"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint Color", Color) = (1, 1, 1, 1)
        _Alpha ("Alpha", Range(0, 1)) = 0.5
        _VertexExpand ("Vertex Expand", Range(0, 0.1)) = 0.0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        LOD 100
        
        ZWrite Off
        Blend SrcAlpha One
        Cull Off
        
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            
            #include "UnityCG.cginc"
            
            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
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
            float4 _Color;
            float _Alpha;
            float _VertexExpand;
            
            v2f vert (appdata v)
            {
                v2f o;
                // Inflate vertices along their normals to create a soft blur shell
                float4 expandedVertex = v.vertex + float4(v.normal * _VertexExpand, 0.0);
                o.vertex = UnityObjectToClipPos(expandedVertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }
            
            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv);
                // Use _Color as the direct output tint rather than a multiplier.
                // With additive blending, multiplying against a white texture
                // just dims the result — replacing RGB gives full control over
                // the ghost's hue and brightness.
                col.rgb = _Color.rgb;
                col.a *= _Alpha;
                
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }
    
    Fallback "Transparent/Diffuse"
}
