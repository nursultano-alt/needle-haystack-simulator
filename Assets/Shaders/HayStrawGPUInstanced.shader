Shader "Universal Render Pipeline/Custom/HayStrawGPUInstanced"
{
    Properties
    {
        _BaseColor ("Base Color Tint", Color) = (0.88, 0.76, 0.42, 1.0)
        _Smoothness ("Smoothness", Range(0.0, 1.0)) = 0.45
        _Metallic ("Metallic", Range(0.0, 1.0)) = 0.08
        _Anisotropy ("Fiber Anisotropy", Range(0.0, 1.0)) = 0.65
    }

    SubShader
    {
        Tags 
        { 
            "RenderType" = "Opaque" 
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }
        LOD 200

        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float2 uv           : TEXCOORD0;
                uint instanceID     : SV_InstanceID;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float3 positionWS   : TEXCOORD0;
                float3 normalWS     : TEXCOORD1;
                float4 color        : COLOR;
                float fogFactor     : TEXCOORD3;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float _Smoothness;
                float _Metallic;
                float _Anisotropy;
            CBUFFER_END

            #if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED) || defined(SHADER_API_D3D11) || defined(SHADER_API_GLCORE) || defined(SHADER_API_GLES3) || defined(SHADER_API_METAL) || defined(SHADER_API_VULKAN)
            StructuredBuffer<float4x4> _TransformBuffer;
            StructuredBuffer<float4> _ColorBuffer;
            #endif

            Varyings vert(Attributes input)
            {
                Varyings output;
                
                float4x4 matInstance;
                float4 instColor = _BaseColor;

                #if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED) || defined(SHADER_API_D3D11) || defined(SHADER_API_GLCORE) || defined(SHADER_API_GLES3) || defined(SHADER_API_METAL) || defined(SHADER_API_VULKAN)
                matInstance = _TransformBuffer[input.instanceID];
                instColor = _ColorBuffer[input.instanceID];
                #else
                matInstance = unity_ObjectToWorld;
                #endif

                // Transform object-space position by per-instance matrix
                float4 posWS = mul(matInstance, input.positionOS);
                output.positionWS = posWS.xyz;
                output.positionCS = TransformWorldToHClip(posWS.xyz);

                // Transform normals (using 3x3 orientation part)
                float3x3 rotMat = (float3x3)matInstance;
                output.normalWS = normalize(mul(rotMat, input.normalOS));
                output.color = instColor;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Surface lighting calculations
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                float3 lightDir = normalize(mainLight.direction);
                float3 viewDir = normalize(GetCameraPositionWS() - input.positionWS);
                float3 normal = normalize(input.normalWS);

                // Diffuse NdotL
                float NdotL = saturate(dot(normal, lightDir));

                // Anisotropic fiber sheen for straw & needles
                float3 halfVec = normalize(lightDir + viewDir);
                float NdotH = saturate(dot(normal, halfVec));
                float spec = pow(NdotH, 32.0 * _Smoothness + 2.0) * (_Smoothness * 0.8 + 0.1);

                // Sub-surface warmth (simulating sun passing through dry translucent straw)
                float sss = saturate(dot(-viewDir, lightDir) * 0.5 + 0.5) * 0.18;

                // Combined color
                float3 albedo = input.color.rgb;
                float3 diffuseLighting = (albedo * NdotL + sss * albedo) * mainLight.color * mainLight.distanceAttenuation * mainLight.shadowAttenuation;
                float3 ambient = SampleSH(normal) * albedo * 0.45;
                float3 specular = spec * mainLight.color;

                float3 finalColor = diffuseLighting + ambient + specular;
                finalColor = MixFog(finalColor, input.fogFactor);

                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }

        // Shadow Caster Pass
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vertShadow
            #pragma fragment fragShadow

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                uint instanceID     : SV_InstanceID;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
            };

            #if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED) || defined(SHADER_API_D3D11) || defined(SHADER_API_GLCORE) || defined(SHADER_API_GLES3) || defined(SHADER_API_METAL) || defined(SHADER_API_VULKAN)
            StructuredBuffer<float4x4> _TransformBuffer;
            #endif

            float3 _LightDirection;

            Varyings vertShadow(Attributes input)
            {
                Varyings output;
                #if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED) || defined(SHADER_API_D3D11) || defined(SHADER_API_GLCORE) || defined(SHADER_API_GLES3) || defined(SHADER_API_METAL) || defined(SHADER_API_VULKAN)
                float4x4 matInstance = _TransformBuffer[input.instanceID];
                #else
                float4x4 matInstance = unity_ObjectToWorld;
                #endif

                float4 posWS = mul(matInstance, input.positionOS);
                float3x3 rotMat = (float3x3)matInstance;
                float3 normalWS = normalize(mul(rotMat, input.normalOS));

                output.positionCS = TransformWorldToHClip(ApplyShadowBias(posWS.xyz, normalWS, _LightDirection));
                return output;
            }

            half4 fragShadow(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Lit"
}
