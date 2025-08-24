Shader "Evolvium/HorizonGradientStarsURP"
{
    Properties
    {
        _Top("Top Color (HDR)", Color) = (0.02,0.05,0.2,1)
        _Horizon("Horizon Color (HDR)", Color) = (0.2,1.0,1.0,1)
        _RoomHeight("Room Height", Float) = 3.0
        _StarDensity("Star Density", Float) = 0.15
        _StarIntensity("Star Intensity", Float) = 0.6
    }
    SubShader
    {
        Tags{ "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend One One
        ZWrite Off
        Cull Back
        Pass
        {
            Name "Unlit"
            Tags{ "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A { float4 positionOS:POSITION; };
            struct V { float4 positionHCS:SV_POSITION; float3 posWS:TEXCOORD0; };
            CBUFFER_START(UnityPerMaterial)
            float4 _Top, _Horizon;
            float _RoomHeight, _StarDensity, _StarIntensity;
            CBUFFER_END

            V vert(A a){ V o; VertexPositionInputs p = GetVertexPositionInputs(a.positionOS.xyz); o.positionHCS=p.positionCS; o.posWS=p.positionWS; return o; }

            // tiny hash for stars
            float hash21(float2 p){ p=frac(p*float2(123.34,345.45)); p+=dot(p,p+34.345); return frac(p.x*p.y); }

            half4 frag(V i):SV_Target
            {
                float h = saturate(i.posWS.y / max(0.001, _RoomHeight));
                float3 grad = lerp(_Horizon.rgb, _Top.rgb, h);

                // sparse star sprinkle in XZ
                float2 cell = floor(i.posWS.xz * 0.8);
                float s = step(1.0 - _StarDensity, hash21(cell));
                float twinkle = 0.5 + 0.5 * sin(_Time.y * 2.7 + hash21(cell+7.7)*6.28);
                float3 stars = s * _StarIntensity * twinkle * float3(0.8, 1.0, 1.0);

                return half4(grad + stars, 1);
            }
            ENDHLSL
        }
    }
}
