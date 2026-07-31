// =============================================================
// 文件：HeightFogSettings.cs（E1-S3 / ADR-010）
// 作用：高度雾的【参数容器 + 数值防护 + 材质/keyword 同步】。
//       被 InkRenderFeature.InkSettings 以子块方式持有，在 Ink Pass 的 Execute 里逐帧 ApplyTo。
//
// 为什么单独成文件（而不是塞进 InkRenderFeature 当嵌套类）：
//   1. 职责分离——settings 是纯数据 + 纯映射，不该和 RendererFeature 的生命周期缠在一起；
//   2. 可测性——嵌套在 ScriptableRendererFeature 里的类型，EditMode 引用时会被 C# 要求
//      连 URP 程序集一起引用（CS0012）；独立出来后测试程序集只依赖 MJ.Runtime，测试边界干净。
//
// 红线：
//   - C2：本类【不创建】任何 RenderPass / RT / Blit。雾是 Ink Pass 内 keyword 门控的一个阶段，
//     不是第二条全屏 Pass。它对渲染管线的全部影响仅限于「改材质属性 + 开关一个 keyword」。
//   - 参数安全：Range 特性只挡 Inspector 拖拽，挡不住脚本赋值与反序列化脏数据，
//     故 ApplyTo 里统一再过 Guard()（NaN/Inf → 回落默认值；越界 → clamp），
//     沿用 E1-S1 越界保护先例。Guard 刻意 public static，供 EditMode 直接参数化断言。
// 注意：需在 Unity 2022.3 + URP 14.0.12 下编译；不写任何版本宏（C5）。
// =============================================================

using UnityEngine;
using UnityEngine.Rendering;

namespace MJ.Rendering
{
    [System.Serializable]
    public class HeightFogSettings
    {
        public const string Keyword = "_MJ_HEIGHT_FOG";

        [Tooltip("开启高度雾。关闭时 keyword 不点亮，雾代码不进变体，画面与 S1 墨韵逐像素一致。")]
        public bool enabled = false;

        [Tooltip("雾色：默认淡墨青灰，呼应水墨低饱和冷调。")]
        public Color fogColor = new Color(0.62f, 0.68f, 0.72f, 1f);

        [Tooltip("雾面世界 Y 高度：低于此高度雾开始变浓。")]
        [Range(MinBaseHeight, MaxBaseHeight)] public float baseHeight = DefaultBaseHeight;

        [Tooltip("雾浓度上限。")]
        [Range(MinDensity, MaxDensity)] public float density = DefaultDensity;

        [Tooltip("高度衰减：越大雾越贴地（高台越快清透）。")]
        [Range(MinHeightFalloff, MaxHeightFalloff)] public float heightFalloff = DefaultHeightFalloff;

        [Tooltip("距离淡入（米）：近处不糊、远处渐入。")]
        [Range(MinDistFade, MaxDistFade)] public float distFade = DefaultDistFade;

        [Tooltip("天空混合上限：防止天空被雾糊死（0 = 天空完全不受雾）。")]
        [Range(MinSkyBlend, MaxSkyBlend)] public float skyBlend = DefaultSkyBlend;

        // ---- clamp 区间与默认值：Inspector / ApplyTo / EditMode 共用同一套常量，杜绝多处漂移 ----
        public const float MinBaseHeight = -100f, MaxBaseHeight = 500f, DefaultBaseHeight = 0f;
        public const float MinDensity = 0f, MaxDensity = 5f, DefaultDensity = 0.8f;
        public const float MinHeightFalloff = 0f, MaxHeightFalloff = 2f, DefaultHeightFalloff = 0.15f;
        public const float MinDistFade = 0.01f, MaxDistFade = 1000f, DefaultDistFade = 60f;
        public const float MinSkyBlend = 0f, MaxSkyBlend = 1f, DefaultSkyBlend = 0.25f;

        public static Color DefaultFogColor => new Color(0.62f, 0.68f, 0.72f, 1f);

        // ---- Shader 属性名与 ID（ID 避免每帧字符串哈希；名字暴露给测试做存在性断言） ----
        public const string PropFogColor = "_FogColor";
        public const string PropFogBaseHeight = "_FogBaseHeight";
        public const string PropFogDensity = "_FogDensity";
        public const string PropFogHeightFalloff = "_FogHeightFalloff";
        public const string PropFogDistFade = "_FogDistFade";
        public const string PropFogSkyBlend = "_FogSkyBlend";

        private static readonly int IdFogColor = Shader.PropertyToID(PropFogColor);
        private static readonly int IdFogBaseHeight = Shader.PropertyToID(PropFogBaseHeight);
        private static readonly int IdFogDensity = Shader.PropertyToID(PropFogDensity);
        private static readonly int IdFogHeightFalloff = Shader.PropertyToID(PropFogHeightFalloff);
        private static readonly int IdFogDistFade = Shader.PropertyToID(PropFogDistFade);
        private static readonly int IdFogSkyBlend = Shader.PropertyToID(PropFogSkyBlend);

        /// <summary>
        /// 单值防护：NaN / ±Inf → fallback；其余 clamp 到 [min, max]。
        /// </summary>
        public static float Guard(float value, float min, float max, float fallback)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return fallback;
            return Mathf.Clamp(value, min, max);
        }

        /// <summary>颜色防护：逐通道 Guard 到 [0,1]，脏通道回落 fallback 对应通道；alpha 恒 1（雾色不用 alpha）。</summary>
        public static Color Guard(Color value, Color fallback)
        {
            return new Color(
                Guard(value.r, 0f, 1f, fallback.r),
                Guard(value.g, 0f, 1f, fallback.g),
                Guard(value.b, 0f, 1f, fallback.b),
                1f);
        }

        /// <summary>把经过防护的参数写进材质，并按 enabled 同步 keyword。不做任何 Pass/RT/Blit 操作（C2）。</summary>
        public void ApplyTo(Material mat)
        {
            if (mat == null) return;

            mat.SetColor(IdFogColor, Guard(fogColor, DefaultFogColor));
            mat.SetFloat(IdFogBaseHeight, Guard(baseHeight, MinBaseHeight, MaxBaseHeight, DefaultBaseHeight));
            mat.SetFloat(IdFogDensity, Guard(density, MinDensity, MaxDensity, DefaultDensity));
            mat.SetFloat(IdFogHeightFalloff, Guard(heightFalloff, MinHeightFalloff, MaxHeightFalloff, DefaultHeightFalloff));
            mat.SetFloat(IdFogDistFade, Guard(distFade, MinDistFade, MaxDistFade, DefaultDistFade));
            mat.SetFloat(IdFogSkyBlend, Guard(skyBlend, MinSkyBlend, MaxSkyBlend, DefaultSkyBlend));

            // keyword 是「关雾零成本」的开关本体（ADR-010）
            CoreUtils.SetKeyword(mat, Keyword, enabled);
        }
    }
}
