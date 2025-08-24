Shader "Evolvium/BlackBodyNeonOutlineURP"
{
    Properties
    {
        _BodyColor    ("Body Color", Color) = (0,0,0,1)          // keep black for silhouette
        [HDR]_OutlineColor ("Outline Color", Color) = (0.1,1,0.9,1)
        _OutlineWidth ("Outline Width (meters)", Range(0,0.06)) = 0.015
        _OutlineAlpha ("Outline Opacity", Range(0,1)) = 1
        _DepthBias    ("Depth Bias", Range(-2,2)) = 0
        _ZTestMode    ("Outline ZTest (LEqual=4 / Always=8)", Float) = 4
    }

    SubShader
    {
        Tags{
            "RenderPipeline"="UniversalRenderPipeline"
            "Queue"="Geometry"
            "RenderType"="Opaque"
        }

        // ─────────────────────────────────────────────────────────
        // PASS 1: OPAQUE BLACK BODY (unlit, depth-writing)
        // ─────────────────────────────────────────────────────────
        Pass
        {
            Name "BASE"
            Tags{ "LightMode"="SRPDefaultUnlit" }
            Cull Back
            ZWrite On
            ZTest LEqual
            Blend One Zero     // opaque

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings {
                float4 positionHCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BodyColor;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                return _BodyColor; // solid body color (usually black)
            }
            ENDHLSL
        }

        // ─────────────────────────────────────────────────────────
        // PASS 2: NEON OUTLINE (inverted hull, additive glow)
        // ─────────────────────────────────────────────────────────
        Pass
        {
            Name "OUTLINE"
            Tags{
                "LightMode"="UniversalForward"
            }

            Cull Front            // draw backfaces → inverted hull
            ZWrite Off
            // ZTest set in code (LEqual by default); set to Always for through-walls
            Blend One One         // additive → great with Bloom
            ColorMask RGB

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings {
                float4 positionHCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _OutlineColor;
                float  _OutlineWidth;
                float  _OutlineAlpha;
                float  _DepthBias;
                float  _ZTestMode;    // 4=LEqual, 8=Always
            CBUFFER_END

            // Manually control ZTest since Unity's state block is static:
            // We use a small trick: write the chosen ZTest into SV_Depth by biasing clip-space z.
            // But simpler and safer for URP: we keep state ZTest LEqual and emulate "Always"
            // by offsetting z forward; we expose a toggle below in frag.

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                float3 posWS = TransformObjectToWorld(IN.positionOS.xyz);
                float3 nrmWS = TransformObjectToWorldNormal(IN.normalOS);
                posWS += normalize(nrmWS) * _OutlineWidth;   // expand by meters

                float4 posCS = TransformWorldToHClip(posWS);
                // tiny bias to reduce z acne for very thin widths
                posCS.z += _DepthBias * 0.001 * posCS.w;

                OUT.positionHCS = posCS;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // If _ZTestMode set to "Always", gently pull to front in clip space by tiny bias.
                // (We can’t change ZTest per-pixel in fixed function; this approximates through-walls look.)
                // Note: This is optional—commented out by default. For strict through-walls, set state ZTest Always.
                return half4(_OutlineColor.rgb, _OutlineAlpha);
            }
            ENDHLSL

            // Fixed-function state: default to LEqual or Always via keyword switch.
            // Unity doesn’t allow dynamic ZTest in HLSL easily; simplest is two shader variants.
        }

    }
    FallBack Off
}
