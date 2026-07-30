// =============================================================
// 文件：InkFullscreen.shader
// 作用：墨韵全屏后处理（URP 兼容 HLSL）。效果：
//   a) 深度 Sobel 边缘检测 -> 毛笔勾边（屏幕空间轮廓线）
//   b) 程序化纸纹：基于 UV 的 value noise 做 Multiply，暖灰底，模拟宣纸
//   c) 墨渍：边缘附近用噪声外扩做暗墨堆积
//   d) 飞白：高频噪声阈值打孔，制造枯笔留白
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

            float4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                float2 texel = 1.0 / _ScreenParams.xy; // 屏幕空间像素尺寸

                // 源色
                float3 src = tex2D(_SourceTex, uv).rgb;

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
