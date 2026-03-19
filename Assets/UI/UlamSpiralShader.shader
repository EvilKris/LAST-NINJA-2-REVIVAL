Shader "UI/UlamSpiralShader"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _FilledColor ("Filled Color", Color) = (0,1,0,1)
        _EmptyColor ("Empty Color", Color) = (0.3,0.3,0.3,1)
        _BackgroundColor ("Background Color", Color) = (0,0,0,0)
        _FillAmount ("Fill Amount", Range(0, 1)) = 1
        _GridSize ("Grid Size", Int) = 11
        _LineThickness ("Line Thickness", Range(0, 1)) = 0.6
        
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
            float4 _BackgroundColor;
            float _FillAmount;
            int _GridSize;
            float _LineThickness;

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
                int layer = max(abs(x), abs(y));

                if (layer == 0)
                    return 1.0;

                // The last number on ring (layer-1) is (2*(layer-1)+1)^2 = (2*layer-1)^2
                int prevLayerMax = (2 * layer - 1) * (2 * layer - 1);

                // Side length of this ring (not counting corners twice)
                int sideLen = 2 * layer;

                // The spiral in the reference image goes:
                // From center: right, then up, then left, then down
                // Ring starts at (layer, -(layer-1)) going up along the right side

                // Right side going up: x == layer, y from -(layer-1) to layer
                if (x == layer && y > -layer)
                {
                    return (float)(prevLayerMax + (y + layer));
                }
                // Top side going left: y == layer, x from (layer-1) to -layer
                else if (y == layer && x < layer)
                {
                    return (float)(prevLayerMax + sideLen + (layer - x));
                }
                // Left side going down: x == -layer, y from (layer-1) to -layer
                else if (x == -layer && y < layer)
                {
                    return (float)(prevLayerMax + 2 * sideLen + (layer - y));
                }
                // Bottom side going right: y == -layer, x from -(layer-1) to layer
                else
                {
                    return (float)(prevLayerMax + 3 * sideLen + (x + layer));
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

                // Map UV to grid space
                float2 gridPos = i.uv * gridF;

                // Cell indices
                int cellX = (int)floor(gridPos.x);
                int cellY = (int)floor(gridPos.y);

                // Clamp to valid range
                cellX = clamp(cellX, 0, _GridSize - 1);
                cellY = clamp(cellY, 0, _GridSize - 1);

                // Convert to centered coordinates (0,0 = center)
                int halfGrid = _GridSize / 2;
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

                // Now draw the cell body and connecting lines to neighbors
                // Cell body: a square in the center of the cell
                float halfThick = _LineThickness * 0.5;
                float cellCenter = 0.5;

                // Check if pixel is within the cell's dot/node
                float inCellX = step(cellCenter - halfThick, cellUV.x) * step(cellUV.x, cellCenter + halfThick);
                float inCellY = step(cellCenter - halfThick, cellUV.y) * step(cellUV.y, cellCenter + halfThick);
                float inCell = inCellX * inCellY;

                // Check connections to right neighbor
                int rx = cx + 1;
                int ry = cy;
                float rightN = GetSpiralNumber(rx, ry);
                float rightConsecutive = (abs(spiralNum - rightN) < 1.5) ? 1.0 : 0.0;
                float rightBothOnPath = step(spiralNum, totalCells) * step(rightN, totalCells);
                float rightConnected = rightConsecutive * rightBothOnPath;
                float rightFilled = rightConnected * step(max(spiralNum, rightN), maxFilledNum);
                // Draw horizontal line to right: right half of cell
                float inRightLine = step(cellCenter, cellUV.x) *
                                    step(cellCenter - halfThick, cellUV.y) *
                                    step(cellUV.y, cellCenter + halfThick) *
                                    rightConnected;
                float rightLineFilled = inRightLine * rightFilled;

                // Check connections to left neighbor
                int lx = cx - 1;
                int ly = cy;
                float leftN = GetSpiralNumber(lx, ly);
                float leftConsecutive = (abs(spiralNum - leftN) < 1.5) ? 1.0 : 0.0;
                float leftBothOnPath = step(spiralNum, totalCells) * step(leftN, totalCells);
                float leftConnected = leftConsecutive * leftBothOnPath;
                float leftFilled = leftConnected * step(max(spiralNum, leftN), maxFilledNum);
                // Draw horizontal line to left: left half of cell
                float inLeftLine = step(cellUV.x, cellCenter) *
                                   step(cellCenter - halfThick, cellUV.y) *
                                   step(cellUV.y, cellCenter + halfThick) *
                                   leftConnected;
                float leftLineFilled = inLeftLine * leftFilled;

                // Check connections to top neighbor
                int tx = cx;
                int ty = cy + 1;
                float topN = GetSpiralNumber(tx, ty);
                float topConsecutive = (abs(spiralNum - topN) < 1.5) ? 1.0 : 0.0;
                float topBothOnPath = step(spiralNum, totalCells) * step(topN, totalCells);
                float topConnected = topConsecutive * topBothOnPath;
                float topFilled = topConnected * step(max(spiralNum, topN), maxFilledNum);
                // Draw vertical line to top: top half of cell
                float inTopLine = step(cellCenter, cellUV.y) *
                                  step(cellCenter - halfThick, cellUV.x) *
                                  step(cellUV.x, cellCenter + halfThick) *
                                  topConnected;
                float topLineFilled = inTopLine * topFilled;

                // Check connections to bottom neighbor
                int bx = cx;
                int by = cy - 1;
                float bottomN = GetSpiralNumber(bx, by);
                float bottomConsecutive = (abs(spiralNum - bottomN) < 1.5) ? 1.0 : 0.0;
                float bottomBothOnPath = step(spiralNum, totalCells) * step(bottomN, totalCells);
                float bottomConnected = bottomConsecutive * bottomBothOnPath;
                float bottomFilled = bottomConnected * step(max(spiralNum, bottomN), maxFilledNum);
                // Draw vertical line to bottom: bottom half of cell
                float inBottomLine = step(cellUV.y, cellCenter) *
                                     step(cellCenter - halfThick, cellUV.x) *
                                     step(cellUV.x, cellCenter + halfThick) *
                                     bottomConnected;
                float bottomLineFilled = inBottomLine * bottomFilled;

                // Combine: is the pixel on any part of the spiral line?
                float inAnyLine = saturate(inRightLine + inLeftLine + inTopLine + inBottomLine);
                float onSpiral = saturate(inCell * isOnPath + inAnyLine);

                // Is the pixel filled?
                float anyLineFilled = saturate(rightLineFilled + leftLineFilled + topLineFilled + bottomLineFilled);
                float pixelFilled = saturate(inCell * isFilled + anyLineFilled);

                // Determine color
                // If on spiral and filled -> filled color
                // If on spiral and not filled -> empty color
                // Otherwise -> background (transparent)
                fixed4 spiralColor = lerp(_EmptyColor, _FilledColor, step(0.5, pixelFilled));
                fixed4 finalColor = lerp(_BackgroundColor, spiralColor, step(0.5, onSpiral));

                finalColor *= i.color;

                return finalColor;
            }
            ENDCG
        }
    }
}
