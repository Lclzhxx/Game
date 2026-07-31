// =============================================================
// 文件：ToonGuofeng.shader（E1-S2，ADR-008 国风 Toon 技术骨架）
// 作用：角色/场景国风 Toon 前向着色——Ramp 分档 + 水墨阴影色 + Rim + 笔触扰动。
//       视觉参数全部暴露给美术（art-director S2 末对齐），骨架不钉观感。
// 三 pass：UniversalForward / ShadowCaster / DepthOnly（无 DepthNormals，墨线只用深度 Sobel）。
// 红线：
//   - R5：【零描边参数】。属性区/CBUFFER 禁止出现任何 outline/描边字段，
//     屏幕勾线 100% 归墨韵 Ink Pass（EditMode 测试守卫属性名）。
//   - C5：只用 URP 14 经典 includes，不写版本宏。
//   - 变体预算（≤64）：keyword 仅 2 个 shader_feature_local（RampTex/Brush）
//     + 主光阴影(3) × 软阴影(2) × 附加光(2) ⇒ Forward 48 变体；高光走 uniform 分支不占 keyword。
//     刻意【不加】multi_compile_fog：雾由墨韵全屏 Pass 负责（ADR-010）。
// SRP Batcher：全部材质属性进 CBUFFER(UnityPerMaterial)，且三 pass 共用同一 HLSLINCLUDE
//   （CBUFFER 布局跨 pass 一致是 SRP Batcher 兼容前提）。
// 注意：需在 Unity 2022.3 + URP 14.0.12 下编译。
// =============================================================

Shader "Custom/ToonGuofeng"
{
    Properties
    {
        [Header(Base)]
        _BaseMap("Base Map (RGB)", 2D) = "white" {}
        _BaseColor("Base Color", Color) = (1, 1, 1, 1)

        [Header(Ramp)]
        _RampThreshold("Ramp Threshold (明暗交界)", Range(0.0, 1.0)) = 0.5
        _RampSoftness("Ramp Softness (交界软硬)", Range(0.001, 0.5)) = 0.06
        _RampBands("Ramp Bands (2或3档)", Range(2.0, 3.0)) = 2.0
        [Toggle(_RAMPTEX_ON)] _UseRampTex("Use Ramp LUT", Float) = 0
        _RampTex("Ramp LUT (1D, 可选)", 2D) = "white" {}

        [Header(Ink Shadow)]
        _ShadowTint("Shadow Tint (水墨阴影色·冷灰偏青)", Color) = (0.62, 0.68, 0.72, 1)

        [Header(Rim)]
        _RimColor("Rim Color", Color) = (0.9, 0.88, 0.82, 1)
        _RimPower("Rim Power", Range(0.5, 8.0)) = 4.0
        _RimIntensity("Rim Intensity", Range(0.0, 2.0)) = 0.35
        _RimLightSideMask("Rim 受光侧掩码", Range(0.0, 1.0)) = 1.0

        [Header(Brush)]
        [Toggle(_BRUSHNORMAL_ON)] _UseBrushNormal("Use Brush Normal (笔触扰动)", Float) = 0
        _BrushNormalMap("Brush Normal Map (ADR-003 规范)", 2D) = "bump" {}
        _BrushStrength("Brush Strength", Range(0.0, 1.0)) = 0.3

        [Header(Specular Optional)]
        [Toggle] _SpecularOn("Specular On (默认关·国风非塑料感)", Float) = 0
        _SpecTint("Specular Tint", Color) = (1, 1, 1, 1)
        _SpecThreshold("Specular Threshold", Range(0.5, 1.0)) = 0.92
        _SpecSoftness("Specular Softness", Range(0.001, 0.2)) = 0.02
        _SpecIntensity("Specular Intensity", Range(0.0, 2.0)) = 0.5

        // 【R5 红线】此处永远不得出现描边(Outline)参数——描边 100% 归墨韵 Ink Pass。
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

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        // SRP Batcher 兼容：三 pass 共用同一 UnityPerMaterial 布局（勿在单 pass 内另开 CBUFFER）
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half _RampThreshold;
            half _RampSoftness;
            half _RampBands;
            half _UseRampTex;
            float4 _RampTex_ST;
            half4 _ShadowTint;
            half4 _RimColor;
            half _RimPower;
            half _RimIntensity;
            half _RimLightSideMask;
            half _UseBrushNormal;
            float4 _BrushNormalMap_ST;
            half _BrushStrength;
            half _SpecularOn;
            half4 _SpecTint;
            half _SpecThreshold;
            half _SpecSoftness;
            half _SpecIntensity;
        CBUFFER_END

        TEXTURE2D(_BaseMap);        SAMPLER(sampler_BaseMap);
        TEXTURE2D(_RampTex);        SAMPLER(sampler_RampTex);
        TEXTURE2D(_BrushNormalMap); SAMPLER(sampler_BrushNormalMap);
        ENDHLSL

        // ---------------- Pass 1：前向主光照 ----------------
        Pass
        {
            Name "ToonGuofengForward"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex ToonVert
            #pragma fragment ToonFrag

            // ---- 变体预算（见文件头）：3 × 2 × 2 × 2 × 2 = 48 ----
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma shader_feature_local _RAMPTEX_ON
            #pragma shader_feature_local_fragment _BRUSHNORMAL_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Include/ToonGuofengLighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float2 uv         : TEXCOORD2;
            };

            Varyings ToonVert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs pos = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionCS = pos.positionCS;
                OUT.positionWS = pos.positionWS;
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                return OUT;
            }

            half4 ToonFrag(Varyings IN) : SV_Target
            {
                half3 baseColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv).rgb * _BaseColor.rgb;
                half3 N = normalize(IN.normalWS);
                half3 V = normalize(GetWorldSpaceViewDir(IN.positionWS));

                // ---- 主光（含阴影衰减） ----
                float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                half3 L = mainLight.direction;

                half rampInput = saturate(dot(N, L)) * mainLight.shadowAttenuation;

                // 笔触扰动：明暗交界呈毛笔干湿变化（keyword 门控，关闭零采样）
                #if defined(_BRUSHNORMAL_ON)
                    half brush = SAMPLE_TEXTURE2D(_BrushNormalMap, sampler_BrushNormalMap,
                                     TRANSFORM_TEX(IN.uv, _BrushNormalMap)).g;
                    rampInput = MJBrushJitter(rampInput, brush, _BrushStrength);
                #endif

                // Ramp：smoothstep 分档为默认；LUT 为可选开关
                #if defined(_RAMPTEX_ON)
                    half litFactor = SAMPLE_TEXTURE2D(_RampTex, sampler_RampTex, float2(rampInput, 0.5)).r;
                #else
                    half litFactor = MJToonRamp(rampInput, _RampThreshold, _RampSoftness, _RampBands);
                #endif

                // 水墨阴影色：乘色而非压黑（"淡墨"）
                half3 color = MJShadowTintMix(baseColor, _ShadowTint.rgb, litFactor) * mainLight.color;

                // ---- 附加光：半兰伯特 × ramp 一档（限 1–3 盏点光） ----
                #if defined(_ADDITIONAL_LIGHTS)
                    uint lightCount = GetAdditionalLightsCount();
                    for (uint li = 0u; li < lightCount; li++)
                    {
                        Light addLight = GetAdditionalLight(li, IN.positionWS);
                        half band = MJAdditionalLightBand(
                            dot(N, addLight.direction),
                            addLight.distanceAttenuation * addLight.shadowAttenuation);
                        color += baseColor * addLight.color * band;
                    }
                #endif

                // ---- Rim：剪影勾勒，替代高光 ----
                color += MJRimLight(N, V, L, _RimColor.rgb, _RimPower, _RimLightSideMask)
                         * _RimIntensity * mainLight.color;

                // ---- 可选分档高光（uniform 分支，不占 keyword 预算） ----
                if (_SpecularOn > 0.5h)
                {
                    half spec = MJSpecularBand(N, V, L, _SpecThreshold, _SpecSoftness);
                    color += _SpecTint.rgb * spec * _SpecIntensity
                             * mainLight.color * mainLight.shadowAttenuation;
                }

                // ---- 高度雾接入点（S2 恒等 no-op，雾走墨韵 Pass，ADR-010） ----
                color = ApplyMJHeightFog(color, IN.positionWS);

                return half4(color, 1.0h);
            }
            ENDHLSL
        }

        // ---------------- Pass 2：ShadowCaster（Toon 物体投影） ----------------
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            // 注意顺序：Shadows.hlsl 依赖 CommonMaterial.hlsl 的 LerpWhiteTo（URP 14 不自带）
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            float4 GetShadowPositionHClip(Attributes IN)
            {
                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(IN.normalOS);

                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirectionWS = _LightDirection;
                #endif

                float4 positionCS = TransformWorldToHClip(
                    ApplyShadowBias(positionWS, normalWS, lightDirectionWS));

                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                return positionCS;
            }

            Varyings ShadowVert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = GetShadowPositionHClip(IN);
                return OUT;
            }

            half4 ShadowFrag(Varyings IN) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        // ---------------- Pass 3：DepthOnly（墨线深度 Sobel 勾边的前提） ----------------
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma vertex DepthOnlyVert
            #pragma fragment DepthOnlyFrag

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings DepthOnlyVert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                return OUT;
            }

            half DepthOnlyFrag(Varyings IN) : SV_Target
            {
                return IN.positionCS.z;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
