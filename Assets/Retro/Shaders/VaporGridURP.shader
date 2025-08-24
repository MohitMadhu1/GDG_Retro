Shader "Evolvium/NeonTileGridURP"
{
    Properties
    {
        // Geometry
        _CellSize      ("Cell Size (m)", Float) = 0.60
        _LineWidth     ("Core Line Width (m)", Float) = 0.020
        _GlowWidth     ("Halo Extra Width (m)", Float) = 0.060

        // Emission
        _GridColor     ("Line Color (HDR)", Color) = (1,0.2,0.9,1)
        _LineIntensity ("Line Core Intensity", Float) = 2.4
        _HaloIntensity ("Line Halo Intensity", Float) = 1.6

        // Tiles
        _TileBase      ("Tile Base (RGB)", Color) = (0.02,0.02,0.05,1)
        _EdgeTint      ("Tile Edge Tint (RGB)", Color) = (0.06,0.06,0.12,1)
        _Bevel         ("Bevel Size (m)", Float) = 0.12
        _Vignette      ("Center Vignette Strength", Range(0,1)) = 0.35
        _CheckerAmt    ("Subtle Checker Amount", Range(0,1)) = 0.08
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode"="UniversalForward" }
            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS: POSITION; };
            struct Varyings  { float4 positionHCS: SV_POSITION; float3 posWS: TEXCOORD0; };

            CBUFFER_START(UnityPerMaterial)
            float  _CellSize, _LineWidth, _GlowWidth;
            float4 _GridColor;
            float  _LineIntensity, _HaloIntensity;

            float4 _TileBase, _EdgeTint;
            float  _Bevel, _Vignette, _CheckerAmt;
            CBUFFER_END

            Varyings vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionHCS = p.positionCS;
                o.posWS       = p.positionWS;
                return o;
            }

            // returns distance (in cell fraction) to nearest grid line (either axis)
            float nearestLineFrac(float2 uvCell)
            {
                float2 f = frac(uvCell);
                float2 d = min(f, 1.0 - f);  // distances to nearest edges in each axis
                return min(d.x, d.y);        // nearest of X/Z lines
            }

            half4 frag(Varyings i) : SV_Target
            {
                // world-space grid
                float cell = max(_CellSize, 1e-5);
                float2 uv  = i.posWS.xz / cell;

                // distance in "meters" to nearest line
                float dFrac = nearestLineFrac(uv);          // in [0..0.5] (fraction of a cell)
                float dM    = dFrac * cell;                 // convert to meters

                // --- LINE CORE & HALO -------------------------------------------------------
                float w  = max(_LineWidth, 1e-5);
                float gw = max(_GlowWidth, 0.0);

                // Core: hard line
                float core = 1.0 - step(w, dM);

                // Halo: soft falloff outside core
                float halo = 1.0 - smoothstep(w, w + gw, dM);

                float3 lineCol =
                    core * _GridColor.rgb * _LineIntensity +
                    halo * _GridColor.rgb * _HaloIntensity;

                // --- TILE SHADING -----------------------------------------------------------
                // Edge bevel: brighten near edges, darken center
                float bev = saturate(1.0 - smoothstep(0.0, max(_Bevel, 1e-5), dM));
                // Center vignette contribution
                float vign = (1.0 - bev) * _Vignette;

                // Base tile color with subtle edge tint
                float3 tileCol = lerp(_TileBase.rgb, _EdgeTint.rgb, bev);

                // Subtle checker variation (alternating tiles)
                float chk = fmod(floor(uv.x) + floor(uv.y), 2.0);
                tileCol *= (1.0 - _CheckerAmt * (chk * 2.0 - 1.0)); // ± variation

                // Darken centers a bit
                tileCol *= (1.0 - vign);

                // Composite: tile base + emissive lines
                float3 col = tileCol + lineCol;

                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}
