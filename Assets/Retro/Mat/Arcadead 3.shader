Shader "Evolvium/NeonTileGridPulseURP_Safe"
{
    Properties
    {
        _BaseColor    ("Base Color", Color) = (0,0,0,1)
        _LineColor    ("Line Color", Color) = (0.1,1,1,1)
        _Glow         ("Base Glow (emission)", Range(0,4)) = 1.4

        _GridTiling   ("Grid Tiling (tiles/m)", Float) = 1.25
        _LineWidth    ("Line Width (0-0.5)", Range(0.001,0.5)) = 0.08

        // Pulse controls
        _PulseMode    ("Pulse Mode (0=Radial,1=X,2=Z)", Float) = 0
        _PulseAmp     ("Pulse Brightness", Range(0,4)) = 1.2
        _PulseSpeed   ("Pulse Speed", Range(0,10)) = 1.1
        _PulsePeriod  ("Pulse Period (m)", Range(0.1,50)) = 8.0
        _PulseWidth   ("Pulse Band Width", Range(0.01,5)) = 1.0
        _PulseCenter  ("Pulse Center (world XZ)", Vector) = (0,0,0,0)
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }
        LOD 100
        Cull Back
        ZWrite On
        ZTest LEqual
        Blend One Zero  // opaque (no additive)

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS: POSITION; };
            struct Varyings   { float4 positionHCS: SV_POSITION; float3 positionWS: TEXCOORD0; };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor, _LineColor;
                float  _Glow;
                float  _GridTiling, _LineWidth;
                float  _PulseMode, _PulseAmp, _PulseSpeed, _PulsePeriod, _PulseWidth;
                float4 _PulseCenter; // x,z used
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                float3 ws = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionWS = ws;
                OUT.positionHCS = TransformWorldToHClip(ws);
                return OUT;
            }

            float gridMask(float2 uv, float lineW)
            {
                float2 a = abs(frac(uv) - 0.5);
                float halfW = max(lineW * 0.5, 1e-4);
                float2 w = saturate((halfW - a) / halfW);
                return saturate(max(w.x, w.y));
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // world XZ packed as float2: (x, z)
                float2 xz = IN.positionWS.xz;

                // grid coords in tiles
                float2 uvTiles = xz * _GridTiling;
                float g = gridMask(uvTiles, saturate(_LineWidth)); // 0..1 on the grid lines

                // pulse
                float t = _Time.y * _PulseSpeed;
                float dist;
                if (_PulseMode < 0.5) {
                    float2 c = float2(_PulseCenter.x, _PulseCenter.z);
                    dist = distance(xz, c);
                } else if (_PulseMode < 1.5) {
                    dist = xz.x - _PulseCenter.x;   // sweep along X
                } else {
                    dist = xz.y - _PulseCenter.z;   // *** FIXED: use xz.y for Z axis ***
                }

                float period = max(_PulsePeriod, 0.1);
                float w = TWO_PI / period;
                float phase = dist * w - t * w;

                float cosv = cos(phase);
                float band = smoothstep(1.0 - saturate(_PulseWidth), 1.0, cosv);

                float pulse = saturate(_PulseAmp) * band;
                float gain = saturate(_Glow) + pulse;

                float3 col = _BaseColor.rgb * (1.0 - g) + _LineColor.rgb * g;
                col = lerp(col, _LineColor.rgb * gain, g);
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}
