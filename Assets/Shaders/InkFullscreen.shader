// =============================================================
// 文件：InkFullscreen.shader
// 作用：墨韵全屏后处理（URP 兼容 HLSL）。效果：
//   a) 深度 Sobel 边缘检测 -> 毛笔勾边（屏幕空间轮廓线）
//   b) 程序化纸纹：基于 UV 的 value noise 做 Multiply，暖灰底，模拟宣纸
//   c) 墨渍：边缘附近用噪声外扩做暗墨堆积
//   d) 飞白：高频噪声阈值打孔，制造枯笔留白
//   e) 高度雾（E1-S3 / ADR-010，keyword 门控）：世界空间指数高度雾，
//      作用在【源色上、勾线之前】——对应水墨"先晕染后勾勒"的作画顺序。
//      零新增 Pass、零新增 Blit（C2 红线）：复用本 Pass 已有的深度采样与两次 Blit。
// 依赖：零外部贴图，所有噪声均在 shader 内程序化生成。
// 挂到：由 InkMaterialCreator 菜单自动生成 Materials/InkMaterial.mat 并指派本 Shader，
//       再把该材质拖到 InkRenderFeature 的 inkMaterial 字段。无需手动选 Shader。
// 注意：需在 Unity 2022.3 下编译（经典 HLSLPROGRAM + URP includes，非 RenderGraph）。
// =============================================================

Shader "Custom/InkFullscreen"
{
    Properties
    {
        _SourceTex        ("Source Texture", 2D)  = "white" {}
        _LineThickness    ("Ink Line Thickness", Float) = 1.0
        _LineStrength     ("Ink Line Strength",  Float) = 1.0
        _PaperStrength    ("Paper Texture Strength", Float) = 0.35
        _FeibaiThreshold  ("Feibai Threshold", Float) = 0.7
        _InkStainStrength ("Ink Stain Strength", Float) = 0.6

        // ---- 高度雾（E1-S3 / ADR-010）。全部由 InkRenderFeature 的 HeightFogSettings 驱动，
        //      Inspector 上调 Feature 即可，无需手改材质。keyword 关闭时这些值完全不参与计算。----
        _FogColor         ("Fog Color (淡墨青灰)", Color) = (0.62, 0.68, 0.72, 1)
        _FogBaseHeight    ("Fog Base Height (雾面世界 Y)", Float) = 0.0
        _FogDensity       ("Fog Density", Float) = 0.8
        _FogHeightFalloff ("Fog Height Falloff (越大越贴地)", Float) = 0.15
        _FogDistFade      ("Fog Distance Fade (米)", Float) = 60.0
        _FogSkyBlend      ("Fog Sky Blend Cap (天空混合上限)", Float) = 0.25
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        // 全屏后处理：关深度写入、永远通过深度测试、不剔除。
        ZTest Always ZWrite Off Cull Off

        Pass
        {
            Name "InkFullscreenPass"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5

            // ---- 高度雾 keyword（E1-S3 / ADR-010） ----
            // 【与 ADR-010 原文的偏差，需主理人确认】ADR 写的是 shader_feature_local；
            // 这里改用 multi_compile_local_fragment，理由：
            //   shader_feature 的变体只按【材质资产保存时的 keyword 状态】入包，
            //   而本项目的雾开关是运行时由 InkRenderFeature 通过 CoreUtils.SetKeyword 打的——
            //   若 InkMaterial.mat 落盘时雾是关的，开雾变体会在构建期被剥掉，
            //   真机上"打开雾但画面没反应"，且只在出包后才暴露。
            // 代价：始终编译 2 个变体（关雾 1 + 开雾 1），本 shader 无其他 keyword，总量仍是 2，
            //       远低于任何变体预算；【关雾时的运行时成本仍为零】——关雾变体里雾代码根本不存在，
            //       故"关雾时既有墨韵基线逐像素不变"这条硬验收不受影响。
            #pragma multi_compile_local_fragment _ _MJ_HEIGHT_FOG

            // URP 经典 includes（2022.3），提供 Linear01Depth / _ZBufferParams / SAMPLE_DEPTH_TEXTURE 等。
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            sampler2D _SourceTex;
            // 深度纹理：由 RenderFeature 的 ConfigureInput(Depth) 提供。
            // URP 14（Unity 2022.3）下不能用 SAMPLE_DEPTH_TEXTURE 宏（仅在关键字下声明），
            // 故按 URP 标准写法用 TEXTURE2D_X + SAMPLER + SAMPLE_TEXTURE2D_X 采样。
            TEXTURE2D_X(_CameraDepthTexture);
            SAMPLER(sampler_CameraDepthTexture);

            float _LineThickness;
            float _LineStrength;
            float _PaperStrength;
            float _FeibaiThreshold;
            float _InkStainStrength;

            // 高度雾参数（keyword 关闭时不参与任何计算；声明保留，避免材质属性丢失告警）
            float4 _FogColor;
            float _FogBaseHeight;
            float _FogDensity;
            float _FogHeightFalloff;
            float _FogDistFade;
            float _FogSkyBlend;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };

            // Blit 用的全屏三角：顶点已处于裁剪空间，直接透传 + 传 uv。
            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = float4(input.positionOS.xy, 0.0, 1.0);
                output.uv = input.uv;
                return output;
            }

            // ---------- 程序化噪声（无贴图） ----------
            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float valueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float a = hash21(i);
                float b = hash21(i + float2(1.0, 0.0));
                float c = hash21(i + float2(0.0, 1.0));
                float d = hash21(i + float2(1.0, 1.0));
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            // 采样并线性化深度
            float sampleLinearDepth(float2 uv)
            {
                float raw = SAMPLE_TEXTURE2D_X(_CameraDepthTexture, sampler_CameraDepthTexture, uv).r;
                return Linear01Depth(raw, _ZBufferParams);
            }

            // 屏幕空间深度 Sobel -> 轮廓强度
            float sobelDepth(float2 uv, float2 texel)
            {
                float2 o = texel * _LineThickness;
                float d00 = sampleLinearDepth(uv + float2(-o.x, -o.y));
                float d10 = sampleLinearDepth(uv + float2( 0.0, -o.y));
                float d20 = sampleLinearDepth(uv + float2( o.x, -o.y));
                float d01 = sampleLinearDepth(uv + float2(-o.x,  0.0));
                float d21 = sampleLinearDepth(uv + float2( o.x,  0.0));
                float d02 = sampleLinearDepth(uv + float2(-o.x,  o.y));
                float d12 = sampleLinearDepth(uv + float2( 0.0,  o.y));
                float d22 = sampleLinearDepth(uv + float2( o.x,  o.y));

                float gx = d00 + 2.0 * d01 + d02 - d20 - 2.0 * d21 - d22;
                float gy = d00 + 2.0 * d10 + d20 - d02 - 2.0 * d12 - d22;
                return sqrt(gx * gx + gy * gy);
            }

            // ================= 高度雾阶段（E1-S3 / ADR-010） =================
            // 只在 _MJ_HEIGHT_FOG 开启时编译进来；关闭时下面整段代码不存在，
            // 主流程一行不变 => 关雾画面与 S1 已验证墨韵逐像素一致。
            #if defined(_MJ_HEIGHT_FOG)

            // 世界坐标重建：深度 -> NDC -> mul(UNITY_MATRIX_I_VP, ...)。
            // UNITY_MATRIX_I_VP 在 URP 14 的 Input.hlsl 里定义为 unity_MatrixInvVP（已由 Core.hlsl 引入），
            // ComputeWorldSpacePosition 来自 Core RP 的 Common.hlsl。两者在 2022.3/URP14 与
            // Unity 6/URP17 下同名同义，无需版本宏（C5）。
            float3 reconstructWorldPos(float2 uv, float rawDepth)
            {
                return ComputeWorldSpacePosition(uv, rawDepth, UNITY_MATRIX_I_VP);
            }

            // 指数高度雾（解析式，无 raymarch —— 性能契约 §5 硬禁）。
            // fog = density * exp(-falloff * (posWS.y - baseHeight)) * saturate(dist / distFade)
            // 天空（depth 到远平面）单独受 _FogSkyBlend 上限约束，避免天空糊死。
            // 数值安全：指数项先 clamp 指数再 exp，杜绝 inf/NaN；除法分母有下限；
            //           所有对外结果 saturate（沿用 E1-S1 越界保护先例）。
            float3 applyHeightFog(float3 color, float2 uv)
            {
                float rawDepth = SAMPLE_TEXTURE2D_X(_CameraDepthTexture, sampler_CameraDepthTexture, uv).r;
                float linear01 = Linear01Depth(rawDepth, _ZBufferParams);

                float3 posWS = reconstructWorldPos(uv, rawDepth);
                float viewDist = length(posWS - _WorldSpaceCameraPos);

                // 高度项：低于雾面越多越浓，高于雾面迅速清透
                float falloff = max(_FogHeightFalloff, 0.0);
                float expArg = clamp(-falloff * (posWS.y - _FogBaseHeight), -30.0, 30.0);
                float heightTerm = exp(expArg);

                // 距离项：近处不糊，远处渐入
                float distTerm = saturate(viewDist / max(_FogDistFade, 1e-3));

                float fogFactor = saturate(max(_FogDensity, 0.0) * heightTerm * distTerm);

                // 天空混合上限：linear01 逼近 1 即远平面/天空
                float skyMask = step(0.999, linear01);
                float blendCap = lerp(1.0, saturate(_FogSkyBlend), skyMask);
                fogFactor = min(fogFactor, blendCap);

                // 最后一道保险：任何非有限值收敛为 0（宁可无雾，不可出黑块/NaN）
                fogFactor = (fogFactor >= 0.0 && fogFactor <= 1.0) ? fogFactor : 0.0;

                return lerp(color, _FogColor.rgb, fogFactor);
            }
            #endif // _MJ_HEIGHT_FOG

            float4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                float2 texel = 1.0 / _ScreenParams.xy; // 屏幕空间像素尺寸

                // 源色
                float3 src = tex2D(_SourceTex, uv).rgb;

                // (e) 高度雾：先晕染，后面才勾线——勾线叠在雾之上（ADR-010）
                #if defined(_MJ_HEIGHT_FOG)
                    src = applyHeightFog(src, uv);
                #endif

                // (b) 程序化纸纹：value noise 压暗 + 暖灰底
                float paper = valueNoise(uv * _ScreenParams.xy * 0.02);
                paper = lerp(1.0, paper, _PaperStrength);
                float3 warmGray = float3(0.95, 0.92, 0.86);
                float3 col = src * paper * warmGray;

                // (a) 深度 Sobel 墨线
                float edge = sobelDepth(uv, texel);
                edge = saturate(edge * _LineStrength * 8.0);

                // (c) 墨渍：边缘附近噪声外扩做暗墨堆积
                float stainNoise = valueNoise(uv * _ScreenParams.xy * 0.05 + 7.0);
                float stain = saturate(edge * stainNoise * _InkStainStrength * 2.0);

                // (d) 飞白：高频噪声阈值在墨线处打孔留白
                float hf = valueNoise(uv * _ScreenParams.xy * 0.2);
                float feibai = step(_FeibaiThreshold, hf) * edge;

                float3 inkColor = float3(0.08, 0.07, 0.06);
                float ink = saturate(edge - feibai);
                col = lerp(col, inkColor, ink);                       // 墨线
                col = lerp(col, inkColor, stain);                    // 墨渍堆积
                col = lerp(col, warmGray * 0.9, saturate(feibai));  // 飞白留白（减淡）

                return float4(col, 1.0);
            }
            ENDHLSL
        }
    }
}
