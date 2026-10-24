Shader "Universal Render Pipeline/Custom/GoldenNeedleURP"
{
    Properties
    {
        _BaseColor ("Base Gold Color", Color) = (1.0, 0.84, 0.25, 1.0)
        _Metallic ("Metallic", Range(0.0, 1.0)) = 0.98
        _Smoothness ("Smoothness", Range(0.0, 1.0)) = 0.92
        _GlintSpeed ("Glint Speed", Float) = 2.5
        _GlintColor ("Glint Sparkle Color", Color) = (1.0, 1.0, 0.9, 1.0)
    }

    SubShader
    {
        Tags 
        { 
            "RenderType" = "Opaque" 
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }
        LOD 250

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float2 uv           : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float3 positionWS   : TEXCOORD0;
                float3 normalWS     : TEXCOORD1;
                float2 uv           : TEXCOORD2;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float _Metallic;
                float _Smoothness;
                float _GlintSpeed;
                float4 _GlintColor;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                float3 normal = normalize(input.normalWS);
                float3 viewDir = normalize(GetCameraPositionWS() - input.positionWS);
                float3 lightDir = normalize(mainLight.direction);
                float3 halfVec = normalize(lightDir + viewDir);

                float NdotL = saturate(dot(normal, lightDir));
                float NdotV = saturate(dot(normal, viewDir));
                float NdotH = saturate(dot(normal, halfVec));

                // High-specular metallic reflection
                float specPower = exp2(10.0 * _Smoothness + 1.0);
                float spec = pow(NdotH, specPower) * 2.0;
                float fresnel = pow(1.0 - NdotV, 4.0) * 0.8;

                // Pulsing sparkle gleam to reward player discovery
                float glintWave = sin(_Time.y * _GlintSpeed + input.positionWS.y * 12.0);
                float glint = pow(saturate(glintWave), 8.0) * 1.5;

                float3 baseReflect = _BaseColor.rgb * (NdotL * 0.4 + 0.6);
                float3 specularLight = (spec + fresnel) * _BaseColor.rgb * mainLight.color;
                float3 glintSparkle = glint * _GlintColor.rgb;

                float3 finalColor = baseReflect * mainLight.color * mainLight.shadowAttenuation + specularLight + glintSparkle;
                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Lit"
}
