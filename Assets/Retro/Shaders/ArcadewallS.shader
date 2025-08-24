Shader "Evolvium/ArcadeWall_StripesURP"
{
    Properties {
        _Base ("Base (RGB)", Color) = (0.02,0.02,0.06,1)
        _Stripe ("Stripe (HDR)", Color) = (1,0.3,0.9,1)
        _Intensity ("Stripe Intensity", Float) = 2.0
        _StripeWidth ("Stripe Width (m)", Float) = 0.18
        _GapWidth ("Gap Width (m)", Float) = 0.32
        _AngleDeg ("Angle (deg)", Float) = 35
        _Speed ("Scroll Speed", Float) = 0.35
        _NoiseAmt ("Band Noise", Range(0,1)) = 0.15
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
            struct A { float4 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct V { float4 pos:SV_POSITION; float3 ws:TEXCOORD0; };
            CBUFFER_START(UnityPerMaterial)
            float4 _Base,_Stripe; float _Intensity,_StripeWidth,_GapWidth,_AngleDeg,_Speed,_NoiseAmt;
            CBUFFER_END
            V vert(A a){ V o; VertexPositionInputs p=GetVertexPositionInputs(a.positionOS.xyz); o.pos=p.positionCS; o.ws=p.positionWS; return o; }
            float hash(float2 p){ p=frac(p*float2(123.34,345.45)); p+=dot(p,p+34.345); return frac(p.x*p.y); }
            half4 frag(V i):SV_Target {
                float ang = radians(_AngleDeg);
                float2 dir = float2(cos(ang), sin(ang));
                float u = dot(i.ws.xz, dir);
                u += _Time.y * _Speed * 2.0;
                float period = max(_StripeWidth + _GapWidth, 1e-4);
                float w = _StripeWidth;
                float dist = fmod(u, period);
                float band = 1.0 - smoothstep(w*0.5, w*0.5+0.02, abs(dist - w*0.5));
                // noisy pulse
                float n = hash(floor(i.ws.xz*2.5));
                float glow = band * (1.0 + _NoiseAmt*(n*2.0-1.0));
                float3 col = _Base.rgb + _Stripe.rgb * _Intensity * saturate(glow);
                return half4(col,1);
            }
            ENDHLSL
        }
    }
}
