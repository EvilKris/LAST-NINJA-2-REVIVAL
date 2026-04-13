Shader "UI/UlamSpiralShader"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _FilledColor ("Filled Color", Color) = (0,1,0,1)
        _EmptyColor ("Empty Color", Color) = (0.3,0.3,0.3,1)
        _BackgroundColor ("Background Color", Color) = (0,0,0,0)
        _EdgeColor ("Edge Color", Color) = (1,1,0,1)
        _EdgeWidth ("Edge Width", Range(0, 0.5)) = 0.08
        _FillAmount ("Fill Amount", Range(0, 1)) = 1
        _GridSize ("Grid Size", Int) = 11
        _LineThickness ("Line Thickness", Range(0, 1)) = 0.6
        [Toggle] _FlipH ("Flip Horizontal", Float) = 0
        [Toggle] _FlipV ("Flip Vertical", Float) = 0
        
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

            CBUFFER_START(UnityPerMaterial)
            float4 _FilledColor;
            float4 _EmptyColor;
            float4 _BackgroundColor;
            float4 _EdgeColor;
            float _EdgeWidth;
            float _FillAmount;
            int _GridSize;
            float _LineThickness;
            float _FlipH;
            float _FlipV;
            CBUFFER_END

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            // Compute the Ulam spiral number for grid coordinates (x, y)
            // where (0,0) is the center of the grid.
            // The spiral goes: 1 at center, 2 right, 3 up, 4 left, 5 left,
            // 6 down, 7 down, 8 right, 9 right, 10 right, ...
            // Matching the diagram pattern from the reference image.
            float GetSpiralNumber(int x, int y)
            {
                // Determine which ring (layer) this cell is on
                // Use uint abs: |n| = n >= 0 ? n : -n, avoids signed-int abs warning
                uint ax = (uint)(x >= 0 ? x : -x);
                uint ay = (uint)(y >= 0 ? y : -y);
                uint layer = max(ax, ay);

                if (layer == 0u)
                    return 1.0;

                // The last number on ring (layer-1) is (2*(layer-1)+1)^2 = (2*layer-1)^2
                uint prevLayerMax = (2u * layer - 1u) * (2u * layer - 1u);

                // Side length of this ring (not counting corners twice)
                uint sideLen = 2u * layer;

                // The spiral in the reference image goes:
                // From center: right, then up, then left, then down
                // Ring starts at (layer, -(layer-1)) going up along the right side

                // Cast layer back to int for signed comparisons with x/y
                int ilayer = (int)layer;

                // Right side going up: x == layer, y from -(layer-1) to layer
                if (x == ilayer && y > -ilayer)
                {
                    return (float)(prevLayerMax + (uint)(y + ilayer));
                }
                // Top side going left: y == layer, x from (layer-1) to -layer
                else if (y == ilayer && x < ilayer)
                {
                    return (float)(prevLayerMax + sideLen + (uint)(ilayer - x));
                }
                // Left side going down: x == -layer, y from (layer-1) to -layer
                else if (x == -ilayer && y < ilayer)
                {
                    return (float)(prevLayerMax + 2u * sideLen + (uint)(ilayer - y));
                }
                // Bottom side going right: y == -layer, x from -(layer-1) to layer
                else
                {
                    return (float)(prevLayerMax + 3u * sideLen + (uint)(x + ilayer));
                }
            }

            // Check if two adjacent cells are consecutive in spiral order
            // and both should be filled - used for drawing connecting lines
            float IsConnected(int x1, int y1, int x2, int y2, float maxNum)
            {
                float n1 = GetSpiralNumber(x1, y1);
                float n2 = GetSpiralNumber(x2, y2);
                float lo = min(n1, n2);
                float hi = max(n1, n2);
                // They are consecutive and both within fill range
                return (hi - lo < 1.5 && hi <= maxNum) ? 1.0 : 0.0;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float gridF = (float)_GridSize;
                float totalCells = gridF * gridF;

                // Apply flip
                float2 uv = i.uv;
                uv.x = lerp(uv.x, 1.0 - uv.x, _FlipH);
                uv.y = lerp(uv.y, 1.0 - uv.y, _FlipV);

                // Map UV to grid space
                float2 gridPos = uv * gridF;

                // Cell indices
                int cellX = (int)floor(gridPos.x);
                int cellY = (int)floor(gridPos.y);

                // Clamp to valid range
                cellX = clamp(cellX, 0, _GridSize - 1);
                cellY = clamp(cellY, 0, _GridSize - 1);

                // Convert to centered coordinates (0,0 = center)
                // Use >> 1 (bitshift) instead of signed integer divide by 2
                int halfGrid = (int)((uint)_GridSize >> 1);
                int cx = cellX - halfGrid;
                int cy = cellY - halfGrid;

                // Position within cell (0 to 1)
                float2 cellUV = frac(gridPos);

                // Get spiral number for this cell (1-based)
                float spiralNum = GetSpiralNumber(cx, cy);

                // Maximum spiral number that should be filled
                float maxFilledNum = _FillAmount * totalCells;

                // Determine if this cell is filled
                float isFilled = step(spiralNum, maxFilledNum);

                // Determine if this cell is on the spiral path at all
                // (all cells within the grid are on the path)
                float isOnPath = step(spiralNum, totalCells);

                // Outer and inner half-thicknesses for stroke effect
                float halfOuter = _LineThickness * 0.5;
                float halfInner = max(0.0, halfOuter - _EdgeWidth);
                float cellCenter = 0.5;

                // --- Cell node (square at cell center) ---
                // Outer rect (includes edge border)
                float inCellOuterX = step(cellCenter - halfOuter, cellUV.x) * step(cellUV.x, cellCenter + halfOuter);
                float inCellOuterY = step(cellCenter - halfOuter, cellUV.y) * step(cellUV.y, cellCenter + halfOuter);
                float inCellOuter = inCellOuterX * inCellOuterY;
                // Inner rect (fill/empty core)
                float inCellInnerX = step(cellCenter - halfInner, cellUV.x) * step(cellUV.x, cellCenter + halfInner);
                float inCellInnerY = step(cellCenter - halfInner, cellUV.y) * step(cellUV.y, cellCenter + halfInner);
                float inCellInner = inCellInnerX * inCellInnerY;

                // --- Right neighbor connection ---
                int rx = cx + 1;
                int ry = cy;
                float rightN = GetSpiralNumber(rx, ry);
                float rightConsecutive = (abs(spiralNum - rightN) < 1.5) ? 1.0 : 0.0;
                float rightBothOnPath = step(spiralNum, totalCells) * step(rightN, totalCells);
                float rightConnected = rightConsecutive * rightBothOnPath;
                float rightFilled = rightConnected * step(max(spiralNum, rightN), maxFilledNum);
                float inRightOuter = step(cellCenter, cellUV.x) *
                                     step(cellCenter - halfOuter, cellUV.y) *
                                     step(cellUV.y, cellCenter + halfOuter) * rightConnected;
                float inRightInner = step(cellCenter, cellUV.x) *
                                     step(cellCenter - halfInner, cellUV.y) *
                                     step(cellUV.y, cellCenter + halfInner) * rightConnected;

                // --- Left neighbor connection ---
                int lx = cx - 1;
                int ly = cy;
                float leftN = GetSpiralNumber(lx, ly);
                float leftConsecutive = (abs(spiralNum - leftN) < 1.5) ? 1.0 : 0.0;
                float leftBothOnPath = step(spiralNum, totalCells) * step(leftN, totalCells);
                float leftConnected = leftConsecutive * leftBothOnPath;
                float leftFilled = leftConnected * step(max(spiralNum, leftN), maxFilledNum);
                float inLeftOuter = step(cellUV.x, cellCenter) *
                                    step(cellCenter - halfOuter, cellUV.y) *
                                    step(cellUV.y, cellCenter + halfOuter) * leftConnected;
                float inLeftInner = step(cellUV.x, cellCenter) *
                                    step(cellCenter - halfInner, cellUV.y) *
                                    step(cellUV.y, cellCenter + halfInner) * leftConnected;

                // --- Top neighbor connection ---
                int tx = cx;
                int ty = cy + 1;
                float topN = GetSpiralNumber(tx, ty);
                float topConsecutive = (abs(spiralNum - topN) < 1.5) ? 1.0 : 0.0;
                float topBothOnPath = step(spiralNum, totalCells) * step(topN, totalCells);
                float topConnected = topConsecutive * topBothOnPath;
                float topFilled = topConnected * step(max(spiralNum, topN), maxFilledNum);
                float inTopOuter = step(cellCenter, cellUV.y) *
                                   step(cellCenter - halfOuter, cellUV.x) *
                                   step(cellUV.x, cellCenter + halfOuter) * topConnected;
                float inTopInner = step(cellCenter, cellUV.y) *
                                   step(cellCenter - halfInner, cellUV.x) *
                                   step(cellUV.x, cellCenter + halfInner) * topConnected;

                // --- Bottom neighbor connection ---
                int bx = cx;
                int by = cy - 1;
                float bottomN = GetSpiralNumber(bx, by);
                float bottomConsecutive = (abs(spiralNum - bottomN) < 1.5) ? 1.0 : 0.0;
                float bottomBothOnPath = step(spiralNum, totalCells) * step(bottomN, totalCells);
                float bottomConnected = bottomConsecutive * bottomBothOnPath;
                float bottomFilled = bottomConnected * step(max(spiralNum, bottomN), maxFilledNum);
                float inBottomOuter = step(cellUV.y, cellCenter) *
                                      step(cellCenter - halfOuter, cellUV.x) *
                                      step(cellUV.x, cellCenter + halfOuter) * bottomConnected;
                float inBottomInner = step(cellUV.y, cellCenter) *
                                      step(cellCenter - halfInner, cellUV.x) *
                                      step(cellUV.x, cellCenter + halfInner) * bottomConnected;

                // Combine outer band (on spiral at all?)
                float inAnyOuter = saturate(inRightOuter + inLeftOuter + inTopOuter + inBottomOuter);
                float onSpiral = saturate(inCellOuter * isOnPath + inAnyOuter);

                // Combine inner band
                float inAnyInner = saturate(inRightInner + inLeftInner + inTopInner + inBottomInner);
                float onInner = saturate(inCellInner * isOnPath + inAnyInner);

                // Is the pixel in the border (outer but not inner)?
                float onBorder = onSpiral * (1.0 - onInner);

                // Filled status for outer lines
                float anyOuterFilled = saturate(inRightOuter * rightFilled +
                                                inLeftOuter * leftFilled +
                                                inTopOuter * topFilled +
                                                inBottomOuter * bottomFilled);
                float pixelFilled = saturate(inCellOuter * isFilled + anyOuterFilled);

                // Determine color
                // Inner pixels: filled or empty color based on fill state
                // Border pixels: edge color
                // Background: background color
                fixed4 coreColor = lerp(_EmptyColor, _FilledColor, step(0.5, pixelFilled));
                fixed4 spiralColor = lerp(coreColor, _EdgeColor, step(0.5, onBorder));
                fixed4 finalColor = lerp(_BackgroundColor, spiralColor, step(0.5, onSpiral));

                finalColor *= i.color;

                return finalColor;
            }
            ENDCG
        }
    }
}
