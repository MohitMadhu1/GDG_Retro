Shader "Evolvium/ArcadeWall_ShinyEdgeURP"
{
    Properties
    {
        // Base wall look
        _BaseColor   ("Base (RGB)", Color) = (0.015, 0.015, 0.03, 1)
        _GlossTint   ("Gloss Tint (RGB)", Color) = (0.12, 0.14, 0.20, 1)
        _GlossStrength("Gloss Strength", Range(0,2)) = 0.55
        _RimPower    ("Rim Sharpness", Range(0.5, 8)) = 3.2
        _VerticalVignette ("Vertical Vignette", Range(0,1)) = 0.25

        // Edge neon
        _EdgeColor   ("Edge (HDR)", Color) = (0.0, 1.0, 1.0, 1)
        _Intensity   ("Edge Intensity", Float) = 2.2
        _EdgeWidth   ("Core Edge Width (UV)", Range(0.0005, 0.05)) = 0.013
        _GlowWidth   ("Extra Glow Width (UV)", Range(0, 0.2)) = 0.06
        _CornerBoost ("Corner Boost", Range(0, 2)) = 0.6
        _PulseHz     ("Pulse Hz", Range(0, 5)) = 0.6
        _PulseAmt    ("Pulse Amount", Range(0, 1)) = 0.3
    }

    SubShader
    {
        // Opaque base (so it occludes) + additive-looking edges baked into color
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" "RenderType"="Opaque" }
        ZWrite On
        Cull Back

        Pass
        {
            Name "ShinyEdge"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float2 uv          : TEXCOORD2;
                float3 viewWS      : TEXCOORD3;
            };

            CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor, _GlossTint;
            float  _GlossStrength, _RimPower, _VerticalVignette;

            float4 _EdgeColor;
            float  _Intensity, _EdgeWidth, _GlowWidth, _CornerBoost, _PulseHz, _PulseAmt;
            CBUFFER_END

            Varyings vert (Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                VertexNormalInputs   n = GetVertexNormalInputs(v.normalOS);

                o.positionHCS = p.positionCS;
                o.positionWS  = p.positionWS;
                o.normalWS    = normalize(n.normalWS);
                o.viewWS      = normalize(_WorldSpaceCameraPos - p.positionWS);
                o.uv          = v.uv;                     // 0..1 across the quad
                return o;
            }

            // distance to nearest of the four edges in UV space
            float edgeDistance(float2 uv)
            {
                float2 d = min(uv, 1.0 - uv);
                return min(d.x, d.y);
            }

            // "shiny" look: rim/spec highlight that responds to camera angle
            float3 GlossHighlight(float3 N, float3 V, float3 tint, float power, float strength)
            {
                // Fresnel-style rim: bright when glancing
                float fres = pow(saturate(1.0 - dot(N, V)), power);
                return tint * (fres * strength);
            }

            half4 frag (Varyings i) : SV_Target
            {
                // Base shiny wall
                float3 baseCol = _BaseColor.rgb;

                // Vertical vignette (slightly darker toward top/bottom -> glossy slab)
                float vvig = 1.0 - _VerticalVignette * abs(i.uv.y * 2.0 - 1.0);
                baseCol *= vvig;

                // Fake glossy/rim highlight (no lights required)
                float3 gloss = GlossHighlight(normalize(i.normalWS), normalize(i.viewWS),
                                              _GlossTint.rgb, _RimPower, _GlossStrength);
                float3 col = baseCol + gloss;

                // Neon edge mask
                float d = edgeDistance(i.uv);
                float core = 1.0 - smoothstep(_EdgeWidth, _EdgeWidth + 1e-4, d);
                float halo = 1.0 - smoothstep(_EdgeWidth, _EdgeWidth + _GlowWidth, d);

                // Corner boost (brighter near the four corners)
                float2 uv = i.uv;
                float c1 = 1.0 - length(uv);
                float c2 = 1.0 - length(float2(uv.x, 1.0 - uv.y));
                float c3 = 1.0 - length(float2(1.0 - uv.x, uv.y));
                float c4 = 1.0 - length(1.0 - uv);
                float corner = saturate(max(max(c1,c2), max(c3,c4))) * _CornerBoost;

                // Subtle pulse for life
                float pulse = 1.0 + _PulseAmt * sin(_Time.y * (6.28318 * max(0.0, _PulseHz)));

                float glow = (core * 1.0 + halo * 0.7 + corner * 0.4) * _Intensity * pulse;

                col += _EdgeColor.rgb * glow;

                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}
