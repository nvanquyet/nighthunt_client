Shader "PolygonR/PBRMetalRough_Multiply" {
    Properties {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0,2)) = 1.0
        _Metallic ("Metallic", Range(0,1)) = 0.0
        _MetallicGlossMap("Metallic (RGB) Gloss (A)", 2D) = "black" {}
        _OcclusionStrength("Occlusion Strength", Range(0.0, 1.0)) = 1.0
        _EmissionColor("Emission Color", Color) = (1,1,1,1)
        _EmissionMap("Emissive", 2D) = "black" {}
        _EmissionFactor("Emissive Factor", Float) = 1.0
        _BumpScale("Normal Factor", Float) = 1.0
        _BumpMap("Normal Map", 2D) = "bump" {}
        _ColorMask("Color Mask (RGB)", 2D) = "black" {}
        _MaskedColorA ("Masked Color 1 (R)", Color) = (1,0,0,1)
        _MaskedColorB ("Masked Color 2 (G)", Color) = (0,1,0,1)
        _MaskedColorC ("Masked Color 3 (B)", Color) = (0,0,1,1)
        _DamageFX("Damage FX", Float) = 0.0
        _TemperatureMask("Temperature Mask (RGB)", 2D) = "white" {}
        _FrozenColor ("Frozen Color 1 (R)", Color) = (0.5,0.6,0.7,1)
        _BurningColor ("Burning Color 1 (G)", Color) = (0.8,0.3,0.1,1)
        _FrozenMix ("Frozen Mix", Range(0,1)) = 0.0
        _BurningMix ("Burning Mix", Range(0,1)) = 0.0
        _TemperatureVFXScale("UV Temperature Scale", Float) = 4.0
    }

    SubShader {
        Tags {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }
        LOD 200

        // ─────────────────────────────────────────────
        // FORWARD LIT PASS
        // ─────────────────────────────────────────────
        Pass {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4  _Color;
                half   _Glossiness;
                half   _Metallic;
                half   _OcclusionStrength;
                half   _EmissionFactor;
                half4  _EmissionColor;
                half   _BumpScale;
                half   _DamageFX;
                half   _FrozenMix;
                half   _BurningMix;
                half   _TemperatureVFXScale;
                half4  _MaskedColorA;
                half4  _MaskedColorB;
                half4  _MaskedColorC;
                half4  _FrozenColor;
                half4  _BurningColor;
            CBUFFER_END

            TEXTURE2D(_MainTex);          SAMPLER(sampler_MainTex);
            TEXTURE2D(_MetallicGlossMap); SAMPLER(sampler_MetallicGlossMap);
            TEXTURE2D(_EmissionMap);      SAMPLER(sampler_EmissionMap);
            TEXTURE2D(_BumpMap);          SAMPLER(sampler_BumpMap);
            TEXTURE2D(_ColorMask);        SAMPLER(sampler_ColorMask);
            TEXTURE2D(_TemperatureMask);  SAMPLER(sampler_TemperatureMask);

            struct Attributes {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
                float2 lightmapUV : TEXCOORD1;
            };

            // Khong dung DECLARE_LIGHTMAP_OR_SH de tranh macro conflict
            struct Varyings {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float4 lightmapUV  : TEXCOORD1;
                float3 positionWS  : TEXCOORD2;
                float3 normalWS    : TEXCOORD3;
                float3 tangentWS   : TEXCOORD4;
                float3 bitangentWS : TEXCOORD5;
                float  fogFactor   : TEXCOORD6;
                half3  vertexSH    : TEXCOORD7;
            };

            Varyings vert(Attributes IN) {
                Varyings OUT;
                VertexPositionInputs posInputs  = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs   normInputs = GetVertexNormalInputs(IN.normalOS, IN.tangentOS);

                OUT.positionHCS  = posInputs.positionCS;
                OUT.positionWS   = posInputs.positionWS;
                OUT.uv           = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.normalWS     = normInputs.normalWS;
                OUT.tangentWS    = normInputs.tangentWS;
                OUT.bitangentWS  = normInputs.bitangentWS;
                OUT.fogFactor    = ComputeFogFactor(posInputs.positionCS.z);

                // Lightmap / SH thu cong
                #ifdef LIGHTMAP_ON
                    OUT.lightmapUV.xy = IN.lightmapUV * unity_LightmapST.xy + unity_LightmapST.zw;
                    OUT.lightmapUV.zw = 0;
                    OUT.vertexSH      = 0;
                #else
                    OUT.lightmapUV    = 0;
                    OUT.vertexSH      = SampleSHVertex(OUT.normalWS);
                #endif

                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target {
                half2 uv = IN.uv;

                // Temperature FX
                half  tempMask = SAMPLE_TEXTURE2D(_TemperatureMask, sampler_TemperatureMask, uv * _TemperatureVFXScale).r;
                half2 fireUV   = (uv * _TemperatureVFXScale) + half2(0.0h, -_Time.x * 4.0h) + tempMask * 0.2h;
                half  fireMask = SAMPLE_TEXTURE2D(_TemperatureMask, sampler_TemperatureMask, fireUV * 2.0h).g;

                // Albedo + Color Mask
                half4 c         = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv) * _Color;
                half4 colorMask = SAMPLE_TEXTURE2D(_ColorMask, sampler_ColorMask, uv);
                half3 maskedCol = (_MaskedColorA.rgb * colorMask.r)
                                + (_MaskedColorB.rgb * colorMask.g)
                                + (c.rgb * (_MaskedColorC.rgb * colorMask.b));
                c.rgb = lerp(c.rgb, c.rgb * maskedCol, colorMask.r + colorMask.g + colorMask.b);

                // Frozen / Burning
                half3 frozenCol  = clamp(c.r + c.g + c.b, 0.3h, 0.95h) * _FrozenColor.rgb;
                half3 burningCol = clamp(c.r + c.g + c.b, 0.1h, 0.75h) * _BurningColor.rgb;
                c.rgb  = lerp(c.rgb, tempMask + frozenCol, _FrozenMix);
                half3 albedo = lerp(c.rgb, burningCol, _BurningMix);

                // Metallic / Smoothness / Occlusion
                half4 mrao       = SAMPLE_TEXTURE2D(_MetallicGlossMap, sampler_MetallicGlossMap, uv);
                half  metallic   = saturate((_Metallic * mrao.r) - _FrozenMix);
                half  smoothness = saturate((_Glossiness * mrao.a) + (_FrozenMix * 0.5h));
                half  occlusion  = lerp(1.0h, mrao.b, _OcclusionStrength);

                // Normal Map
                half4 normalSample = SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uv);
                half3 normalTS     = UnpackNormalScale(normalSample, _BumpScale);
                float3x3 TBN = float3x3(
                    normalize(IN.tangentWS),
                    normalize(IN.bitangentWS),
                    normalize(IN.normalWS)
                );
                half3 normalWS = normalize(mul(normalTS, TBN));

                // Emission
                half3 emitTex  = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, uv).rgb;
                half3 burnEmit = smoothstep(_BurningColor.rgb, half3(0.0h, 0.0h, 0.0h), fireMask) * _BurningMix;
                half3 emission = burnEmit + (emitTex * _EmissionColor.rgb * _EmissionFactor) + _DamageFX;

                // Bakedgi thu cong
                half3 bakedGI;
                #ifdef LIGHTMAP_ON
                    bakedGI = SampleLightmap(IN.lightmapUV.xy, normalWS);
                #else
                    bakedGI = SampleSHPixel(IN.vertexSH, normalWS);
                #endif

                // PBR Lighting
                InputData lightInput = (InputData)0;
                lightInput.positionWS              = IN.positionWS;
                lightInput.normalWS                = normalWS;
                lightInput.viewDirectionWS         = GetWorldSpaceNormalizeViewDir(IN.positionWS);
                lightInput.shadowCoord             = TransformWorldToShadowCoord(IN.positionWS);
                lightInput.fogCoord                = IN.fogFactor;
                lightInput.bakedGI                 = bakedGI;
                lightInput.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(IN.positionHCS);

                SurfaceData surfData = (SurfaceData)0;
                surfData.albedo      = albedo;
                surfData.metallic    = metallic;
                surfData.smoothness  = smoothness;
                surfData.occlusion   = occlusion;
                surfData.emission    = emission;
                surfData.alpha       = c.a;
                surfData.normalTS    = normalTS;

                half4 color = UniversalFragmentPBR(lightInput, surfData);
                color.rgb   = MixFog(color.rgb, IN.fogFactor);
                return color;
            }
            ENDHLSL
        }

        // ─────────────────────────────────────────────
        // SHADOW CASTER PASS - Viet thu cong, tranh
        // ShadowCasterPass.hlsl vi no dung LerpWhiteTo
        // gay loi trong URP version nay
        // ─────────────────────────────────────────────
        Pass {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            Cull Back
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex shadowVert
            #pragma fragment shadowFrag
            #pragma multi_compile_shadowcaster

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
            CBUFFER_END

            float3 _LightDirection;

            struct ShadowAttribs {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            float4 shadowVert(ShadowAttribs IN) : SV_POSITION {
                float3 posWS  = TransformObjectToWorld(IN.positionOS.xyz);
                float3 normWS = TransformObjectToWorldNormal(IN.normalOS);
                return TransformWorldToHClip(ApplyShadowBias(posWS, normWS, _LightDirection));
            }

            half4 shadowFrag(float4 pos : SV_POSITION) : SV_Target {
                return 0;
            }
            ENDHLSL
        }

        // ─────────────────────────────────────────────
        // DEPTH ONLY PASS
        // ─────────────────────────────────────────────
        Pass {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex depthVert
            #pragma fragment depthFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
            CBUFFER_END

            float4 depthVert(float4 posOS : POSITION) : SV_POSITION {
                return TransformObjectToHClip(posOS.xyz);
            }

            half4 depthFrag(float4 pos : SV_POSITION) : SV_Target {
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}