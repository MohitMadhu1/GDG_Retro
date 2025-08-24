Shader "Evolvium/NeonWall_LuminousDrops"
{
    Properties
    {
        // Colors / glow
        [HDR]_BarA            ("Bar Color A", Color) = (0.38, 0.85, 1.00, 1)
        [HDR]_BarB            ("Bar Color B", Color) = (0.95, 0.55, 1.00, 1)
        _UseGradient          ("Use A→B gradient (0/1)", Float) = 1
        _BgColor              ("Background", Color)  = (0,0,0,1)

        [HDR]_EmitColor       ("Emission Color", Color) = (0.70, 0.90, 1.20, 1)
        _EmitStrength         ("Emission Strength", Range(0,12)) = 7.0
        _HeadBoost            ("Head Glow Boost", Range(0,4)) = 1.6

        // Layout
        _Density              ("Bars per UV width", Float) = 22.0
        _Thickness            ("Bar Thickness", Range(0.004,0.18)) = 0.05
        _SoftEdge             ("Edge Softness", Range(0.0005,0.12)) = 0.035
        _CapRound             ("Cap Roundness", Range(0.0,0.5)) = 0.30
        _AngleDeg             ("Bar Angle (deg)", Range(-30,30)) = 10.0

        // Drops (per bar)
        _DropRate             ("Base Drop Rate (Hz)", Float) = 0.8
        _AccelPow             ("Acceleration Curve", Range(1,4)) = 2.2
        _TrailLen             ("Trail Length", Range(0.03,1.2)) = 0.42
        _TrailSoft            ("Trail Softness", Range(0.001,0.12)) = 0.03

        // Composition (to match ref)
        _SliverChance         ("Short Sliver Chance", Range(0,1)) = 0.25
        _SliverMin            ("Sliver Min Len", Range(0.02,0.6)) = 0.10
        _SliverMax            ("Sliver Max Len", Range(0.02,1.2)) = 0.28

        _TopDots              ("Top Dots (0/1)", Float) = 1
        _DotDensity           ("Dot Density", Range(0,1)) = 0.25
        _DotSize              ("Dot Size", Range(0.002,0.06)) = 0.02
        _DotSoft              ("Dot Softness", Range(0.001,0.08)) = 0.02
    }

    SubShader
    {
        Tags{ "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 100

        Pass
        {
            Name "ForwardUnlit"
            Tags{ "LightMode"="UniversalForward" }

            Cull Off          // double‑sided walls
            ZWrite On
            ZTest LEqual
            Blend One Zero    // opaque

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex   vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            // URP include + safe fallback
            #if defined(UNITY_RENDER_PIPELINE_UNIVERSAL)
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #define O2W(v) TransformObjectToWorld(v)
            #define W2H(p) TransformWorldToHClip(p)
            #else
            #include "UnityCG.cginc"
            #define O2W(v) mul(unity_ObjectToWorld, float4(v,1)).xyz
            #define W2H(p) mul(UNITY_MATRIX_VP, float4(p,1))
            #endif

            CBUFFER_START(UnityPerMaterial)
                float4 _BarA, _BarB, _BgColor;
                float  _UseGradient;

                float4 _EmitColor;
                float  _EmitStrength, _HeadBoost;

                float  _Density, _Thickness, _SoftEdge, _CapRound;
                float  _AngleDeg;

                float  _DropRate, _AccelPow, _TrailLen, _TrailSoft;

                float  _SliverChance, _SliverMin, _SliverMax;

                float  _TopDots, _DotDensity, _DotSize, _DotSoft;
            CBUFFER_END

            struct Attributes { float4 positionOS: POSITION; float2 uv: TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings   { float4 positionCS: SV_POSITION; float2 uv: TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };

            float hash11(float n) { return frac(sin(n) * 43758.5453123); }
            float hash21(float2 p){ return frac(sin(dot(p, float2(12.9898,78.233))) * 43758.5453); }

            float2 rot2(float2 p, float deg)
            {
                float r = deg * 0.017453292519943295; // PI/180
                float s = sin(r), c = cos(r);
                return float2(c*p.x - s*p.y, s*p.x + c*p.y);
            }

            Varyings vert (Attributes IN)
            {
                Varyings OUT; UNITY_SETUP_INSTANCE_ID(IN); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.positionCS = W2H(O2W(IN.positionOS.xyz));
                OUT.uv = IN.uv;
                return OUT;
            }

            // soft bar with rounded head/foot feel
            float roundedBarMask(float2 local, float halfWidth, float capRound, float edgeSoft)
            {
                float dx   = abs(local.x) - halfWidth;
                float side = 1.0 - smoothstep(0.0, max(1e-5, edgeSoft), dx);
                float ry   = saturate(capRound);
                float cap  = 1.0 - smoothstep(0.0, max(1e-5, ry), abs(local.y));
                return saturate(side * (0.65 + 0.35 * cap));
            }

            // 1 inside [a..b] with soft borders (s)
            float smoothRange(float x, float a, float b, float s)
            {
                float L = smoothstep(a - s, a + s, x);
                float R = smoothstep(b - s, b + s, x);
                return saturate(L * (1.0 - R));
            }

            // accelerating drop head position (UV y)
            float headY(float t, float rateHz, float phase, float accelPow)
            {
                float u = frac(t * rateHz + phase);
                float v = pow(u, max(1.0, accelPow));
                return 1.05 - v * 1.25; // enters from top, exits below bottom
            }

            half4 frag (Varyings IN) : SV_Target
            {
                // rotate uv for that slight slant
                float2 uv = IN.uv;
                float t = _Time.y;

                // bar indexing
                float xScaled = uv.x * _Density;
                float barId   = floor(xScaled);
                float cellX   = frac(xScaled) - 0.5;

                // per‑bar randoms
                float seed   = barId * 0.12345 + 7.13;
                float halfW  = max(1e-5, _Thickness * (0.88 + 0.35 * hash11(seed + 2.1)) * 0.5);
                float rate   = _DropRate * (0.85 + 0.3 * hash11(seed + 3.7));
                float phase0 = hash11(seed + 9.4);
                float phase1 = frac(phase0 + 0.42 * (0.8 + 0.4 * hash11(seed + 5.9)));
                float trail0 = _TrailLen * (0.9 + 0.25 * hash11(seed + 1.7));
                float trail1 = _TrailLen * (0.6 + 0.25 * hash11(seed + 8.8));

                // heads + trails
                float yH0 = headY(t, rate,          phase0, _AccelPow);
                float yH1 = headY(t, rate * 1.08,   phase1, _AccelPow);

                float yL0 = uv.y - yH0;
                float yL1 = uv.y - yH1;

                float trailMask0 = smoothRange(yL0, 0.0, trail0, _TrailSoft);
                float trailMask1 = smoothRange(yL1, 0.0, trail1, _TrailSoft);

                float headGlow0 = 1.0 - smoothstep(0.0, _TrailSoft*2.0, abs(yL0));
                float headGlow1 = 1.0 - smoothstep(0.0, _TrailSoft*2.0, abs(yL1));

                // bar shape (x) with rounded feel
                float2 localShape = float2(cellX, 0.0); // only x matters for sides here
                float barSide = roundedBarMask(localShape, halfW, _CapRound, _SoftEdge);

                // compose vertical: two drops + brighter heads
                float vertical = saturate(trailMask0 + trailMask1);
                float heads    = saturate(max(headGlow0, headGlow1) * _HeadBoost);

                float barDrops = saturate((vertical + heads) * barSide);

                // --- short slivers (static little pieces like in your ref) ---
                float sliver = 0.0;
                float sRoll = hash11(seed + floor(uv.y * 3.0) + 12.3);
                if (sRoll < _SliverChance)
                {
                    float len = lerp(_SliverMin, _SliverMax, hash11(seed + 6.6));
                    // center a sliver at a quantized y band
                    float yBand = floor(uv.y * 4.0) / 4.0 + 0.125 * hash11(seed + 4.4);
                    float yLocal = uv.y - yBand;
                    sliver = smoothRange(yLocal, -len*0.5, len*0.5, _TrailSoft);
                    // apply the same barSide so it's a thin vertical segment
                    sliver *= barSide;
                }

                // --- top dots (sparse bright points along the ceiling line) ---
                float dotMask = 0.0;
                if (_TopDots > 0.5)
                {
                    float2 grid = float2(floor(uv.x * _Density), 0);
                    float chance = hash21(grid);
                    if (chance < _DotDensity && uv.y > 0.92)
                    {
                        float2 p = float2(cellX, uv.y - 0.98 - 0.03 * hash11(seed + 1.2));
                        float d  = length(p);
                        dotMask = 1.0 - smoothstep(_DotSize, _DotSize + _DotSoft, d);
                    }
                }

                // final mask
                float mask = saturate(barDrops + sliver + dotMask);

                // gradient across bar width
                float gT = saturate(cellX + 0.5);
                float3 barCol = lerp(_BarA.rgb, _BarB.rgb, (_UseGradient > 0.5) ? gT : 0.0);

                float3 baseCol  = lerp(_BgColor.rgb, barCol, mask);
                float3 emission = _EmitColor.rgb * (_EmitStrength * mask);

                return half4(saturate(baseCol + emission), 1.0);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
