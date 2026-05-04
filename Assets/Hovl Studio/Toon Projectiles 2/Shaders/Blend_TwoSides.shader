Shader "Hovl/Particles/Blend_TwoSides"
{
    Properties
    {
        _Cutoff("Mask Clip Value", Float) = 0.5
        _MainTex("Main Tex", 2D) = "white" {}
        _Mask("Mask", 2D) = "white" {}
        _Noise("Noise", 2D) = "white" {}
        _SpeedMainTexUVNoiseZW("Speed MainTex U/V + Noise Z/W", Vector) = (0,0,0,0)
        _FrontFacesColor("Front Faces Color", Color) = (0,0.2313726,1,1)
        _BackFacesColor("Back Faces Color", Color) = (0.1098039,0.4235294,1,1)
        _Emission("Emission", Float) = 2
        [Toggle]_UseFresnel("Use Fresnel?", Float) = 1
        [Toggle]_SeparateFresnel("SeparateFresnel", Float) = 0
        _SeparateEmission("Separate Emission", Float) = 2
        _FresnelColor("Fresnel Color", Color) = (1,1,1,1)
        _Fresnel("Fresnel", Float) = 1
        _FresnelEmission("Fresnel Emission", Float) = 1
        [Toggle]_UseCustomData("Use Custom Data?", Float) = 0
        [HideInInspector] _texcoord("", 2D) = "white" {}
        [HideInInspector] _tex4coord("", 2D) = "white" {}
        [HideInInspector] __dirty("", Int) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "TransparentCutout"
            "Queue" = "Transparent"
            "IsEmissive" = "true"
            "PreviewType" = "Plane"
        }

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Off
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 color : COLOR;
                float2 uv0 : TEXCOORD0;
                float4 uv1 : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float4 color : COLOR;
                float2 uvMain : TEXCOORD2;
                float2 uvMask : TEXCOORD3;
                float4 uvNoise : TEXCOORD4;
                FRONT_FACE_TYPE facing : FRONT_FACE_SEMANTIC;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_Mask);
            SAMPLER(sampler_Mask);
            TEXTURE2D(_Noise);
            SAMPLER(sampler_Noise);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Mask_ST;
                float4 _Noise_ST;
                float4 _SpeedMainTexUVNoiseZW;
                float4 _FrontFacesColor;
                float4 _BackFacesColor;
                float4 _FresnelColor;
                float _Cutoff;
                float _Emission;
                float _UseFresnel;
                float _SeparateFresnel;
                float _SeparateEmission;
                float _Fresnel;
                float _FresnelEmission;
                float _UseCustomData;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);

                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = normalInputs.normalWS;
                output.color = input.color;
                output.uvMain = TRANSFORM_TEX(input.uv0, _MainTex);
                output.uvMask = TRANSFORM_TEX(input.uv0, _Mask);
                output.uvNoise.xy = TRANSFORM_TEX(input.uv1.xy, _Noise);
                output.uvNoise.zw = input.uv1.zw;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 mainUv = input.uvMain + _SpeedMainTexUVNoiseZW.xy * _Time.y;
                half4 mainSample = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, mainUv);

                float2 noiseUv = input.uvNoise.xy + _SpeedMainTexUVNoiseZW.zw * _Time.y + input.uvNoise.w;
                half maskSample = SAMPLE_TEXTURE2D(_Mask, sampler_Mask, input.uvMask).r;
                half noiseSample = SAMPLE_TEXTURE2D(_Noise, sampler_Noise, noiseUv).r;
                half customMask = lerp(1.0h, saturate((half)input.uvNoise.z), saturate((half)_UseCustomData));
                clip(maskSample * noiseSample * customMask - _Cutoff);

                float3 viewDirWS = SafeNormalize(GetWorldSpaceViewDir(input.positionWS));
                float3 normalWS = SafeNormalize(input.normalWS);
                half fresnel = pow(saturate(1.0h - dot(normalWS, viewDirWS)), max(0.001h, (half)_Fresnel));
                half4 frontColor = lerp(_FrontFacesColor, _FrontFacesColor * (1.0h - fresnel) + _FresnelColor * (_FresnelEmission * fresnel), saturate((half)_UseFresnel));
                half4 faceColor = IS_FRONT_VFACE(input.facing, frontColor, _BackFacesColor);

                half4 regularEmission = faceColor * _Emission * input.color * input.color.a * mainSample;
                half4 separateEmission = (faceColor + _FresnelColor * mainSample * _SeparateEmission) * _Emission * input.color * input.color.a;
                half4 color = lerp(regularEmission, separateEmission, saturate((half)_SeparateFresnel));
                color.a = saturate(mainSample.a * input.color.a);
                return color;
            }
            ENDHLSL
        }
    }

    Fallback "Hidden/Universal Render Pipeline/FallbackError"
}
