Shader "Evolvium/GlassTubeURP"
{
    Properties
    {
        _Tint("Tint (HDR)", Color) = (1,0.4,1,1)
        _FresnelPower("Fresnel Power", Float) = 4.0
        _Intensity("Emission Intensity", Float) = 2.2
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
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A { float4 positionOS:POSITION; float3 normalOS:NORMAL; };
            struct V { float4 positionHCS:SV_POSITION; float3 normalWS:TEXCOORD0; };
            CBUFFER_START(UnityPerMaterial)
            float4 _Tint; float _FresnelPower; float _Intensity;
            CBUFFER_END
            V vert(A a){ V o; VertexPositionInputs p = GetVertexPositionInputs(a.positionOS.xyz); o.positionHCS=p.positionCS; o.normalWS=TransformObjectToWorldNormal(a.normalOS); return o; }
            half4 frag(V i):SV_Target
            {
                float3 n = normalize(i.normalWS);
                float3 v = normalize(_WorldSpaceCameraPos - TransformWorldToObject(float3(0,0,0))); // camera dir approx
                float fres = pow(1.0 - saturate(dot(n, normalize(_WorldSpaceCameraPos))), _FresnelPower);
                float3 col = _Tint.rgb * _Intensity * fres;
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}
