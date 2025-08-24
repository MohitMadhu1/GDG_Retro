Shader "Evolvium/ArcadeWall_EqualizerURP"
{
    Properties {
        _Base ("Base (RGB)", Color) = (0.02,0.02,0.06,1)
        _Bar ("Bar (HDR)", Color) = (1,0.7,0.2,1)
        _Intensity ("Bar Intensity", Float) = 2.2
        _Bars ("Bars Across", Float) = 22
        _Gap ("Bar Gap Fraction", Range(0,0.5)) = 0.2
        _HeightNoise ("Height Noise Amt", Range(0,1)) = 0.6
        _Scroll ("Scroll Speed", Float) = 0.25
        _TopGlow ("Top Halo (m)", Float) = 0.12
    }
    SubShader {
        Tags{ "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        ZWrite Off Blend One One
        Pass {
            Name "Unlit"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A{ float4 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct V{ float4 pos:SV_POSITION; float3 ws:TEXCOORD0; float2 uv:TEXCOORD1; };
            CBUFFER_START(UnityPerMaterial)
            float4 _Base,_Bar; float _Intensity,_Bars,_Gap,_HeightNoise,_Scroll,_TopGlow;
            CBUFFER_END
            V vert(A a){ V o; VertexPositionInputs p=GetVertexPositionInputs(a.positionOS.xyz); o.pos=p.positionCS; o.ws=p.positionWS; o.uv=a.uv; return o; }
            float hash(float2 p){ p=frac(p*float2(123.4,345.6)); p+=dot(p,p+34.5); return frac(p.x*p.y); }
            half4 frag(V i):SV_Target {
                // map world XZ to a stable across axis; use wall local uv.x if available
                float x = i.uv.x; // 0..1 across the quad
                float id = floor(x * _Bars);
                float cellX = (id + 0.5)/_Bars;
                float within = abs(x - cellX) * _Bars * 2.0;
                float gapMask = smoothstep(_Gap, 0.0, within); // 1 in bar, 0 in gap

                // animated height per bar
                float seed = id*17.23 + floor(_Time.y*_Scroll)*3.1;
                float h = 0.25 + _HeightNoise * hash(float2(seed, seed*1.37)); // 0.25..(≈0.85)
                float y = saturate(i.uv.y / max(1e-4, h));

                float body = step(i.uv.y, h); // below height
                float topHalo = 1.0 - smoothstep(h, h + _TopGlow, i.uv.y);

                float bar = saturate(body + topHalo*0.8) * gapMask;

                float3 col = _Base.rgb + _Bar.rgb * _Intensity * bar;
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}
