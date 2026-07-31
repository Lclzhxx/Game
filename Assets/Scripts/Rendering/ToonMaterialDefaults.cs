// =============================================================
// 文件：ToonMaterialDefaults.cs（E1-S2 子任务 3/4，ADR-008）
// 作用：国风 Toon 材质【默认参数的单一事实来源】(runtime，纯静态)。
//       编辑器菜单（ToonMaterialCreator）与验收场景构建器（ToonReviewBuilder）
//       都调这里，杜绝"菜单一套值、场景另一套值、模板资产第三套值"的漂移。
// 与资产的关系：Assets/Materials/ToonGuofeng_Default.mat 是本文件参数的固化快照；
//       EditMode 测试（ToonShaderTests）逐项比对二者，漂移即报红。
// 参数哲学（ADR-008）：骨架不钉观感——这里是"能看清结构"的中性起始值，
//       art-director 对齐后改本文件 + 重生成模板资产。
// 红线（R5）：本文件不得出现任何描边（outline）相关参数。
// 注意：runtime 程序集，禁止 using UnityEditor。
// =============================================================

using UnityEngine;

namespace MJ.Rendering
{
    public static class ToonMaterialDefaults
    {
        public const string ShaderName = "Custom/ToonGuofeng";
        public const string RampTexKeyword = "_RAMPTEX_ON";
        public const string BrushNormalKeyword = "_BRUSHNORMAL_ON";

        // ---- Ramp ----
        public const float RampThreshold = 0.5f;
        public const float RampSoftness = 0.06f;
        public const float RampBands = 2f;

        // ---- Rim ----
        public const float RimPower = 4f;
        public const float RimIntensity = 0.35f;
        public const float RimLightSideMask = 1f;

        // ---- Brush ----
        public const float BrushStrength = 0.3f;

        // ---- Specular（默认关） ----
        public const float SpecularOn = 0f;
        public const float SpecThreshold = 0.92f;
        public const float SpecSoftness = 0.02f;
        public const float SpecIntensity = 0.5f;

        public static Color BaseColor => new Color(1f, 1f, 1f, 1f);
        /// <summary>水墨阴影色：冷灰偏青，乘色而非压黑。</summary>
        public static Color ShadowTint => new Color(0.62f, 0.68f, 0.72f, 1f);
        public static Color RimColor => new Color(0.9f, 0.88f, 0.82f, 1f);
        public static Color SpecTint => new Color(1f, 1f, 1f, 1f);

        /// <summary>把默认参数写进材质。useBrushNormal=true 时同时开笔触 keyword。</summary>
        public static void Apply(Material mat, bool useRampTex = false, bool useBrushNormal = false)
        {
            if (mat == null) return;

            mat.SetColor("_BaseColor", BaseColor);

            mat.SetFloat("_RampThreshold", RampThreshold);
            mat.SetFloat("_RampSoftness", RampSoftness);
            mat.SetFloat("_RampBands", RampBands);
            SetToggle(mat, "_UseRampTex", RampTexKeyword, useRampTex);

            mat.SetColor("_ShadowTint", ShadowTint);

            mat.SetColor("_RimColor", RimColor);
            mat.SetFloat("_RimPower", RimPower);
            mat.SetFloat("_RimIntensity", RimIntensity);
            mat.SetFloat("_RimLightSideMask", RimLightSideMask);

            SetToggle(mat, "_UseBrushNormal", BrushNormalKeyword, useBrushNormal);
            mat.SetFloat("_BrushStrength", BrushStrength);

            mat.SetFloat("_SpecularOn", SpecularOn);
            mat.SetColor("_SpecTint", SpecTint);
            mat.SetFloat("_SpecThreshold", SpecThreshold);
            mat.SetFloat("_SpecSoftness", SpecSoftness);
            mat.SetFloat("_SpecIntensity", SpecIntensity);
        }

        /// <summary>新建一份带默认参数的 Toon 材质；shader 缺失返回 null（调用方须判空）。</summary>
        public static Material CreateMaterial(bool useRampTex = false, bool useBrushNormal = false)
        {
            Shader shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                Debug.LogError("[ToonMaterialDefaults] 找不到 Shader '" + ShaderName +
                               "'，请确认 Assets/Shaders/ToonGuofeng.shader 已导入且编译通过。");
                return null;
            }
            var mat = new Material(shader) { name = "ToonGuofeng_Runtime" };
            Apply(mat, useRampTex, useBrushNormal);
            return mat;
        }

        private static void SetToggle(Material mat, string floatProp, string keyword, bool on)
        {
            mat.SetFloat(floatProp, on ? 1f : 0f);
            if (on) mat.EnableKeyword(keyword);
            else mat.DisableKeyword(keyword);
        }

        /// <summary>
        /// 程序化笔触法线图（验收场景专用，零美术资产依赖）。
        /// shader 只取 G 通道做明暗交界扰动，故这里用竖向条纹 + value noise 写进 G，
        /// R/B 填中性值（0.5/1.0），整体近似"切线空间法线"的合法取值域。
        /// 仅供 ToonReview 验收；正式笔触法线走 ADR-003 规范由美术出图。
        /// </summary>
        public static Texture2D CreateProceduralBrushNormal(int size = 256, int seed = 20260731)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
            tex.name = "ProceduralBrushNormal";
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Bilinear;

            var rand = new System.Random(seed);
            // 预生成一维随机相位，做出"笔锋走向"的条纹感
            var phase = new float[16];
            for (int i = 0; i < phase.Length; i++) phase[i] = (float)rand.NextDouble() * Mathf.PI * 2f;

            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (float)x / size;
                    float v = (float)y / size;
                    float streak = 0f;
                    for (int i = 0; i < phase.Length; i++)
                    {
                        float freq = 3f + i * 2.3f;
                        streak += Mathf.Sin((u * freq + v * 0.35f) * Mathf.PI * 2f + phase[i]) / (i + 1f);
                    }
                    // 归一化到 0..1，中性 0.5
                    float g = Mathf.Clamp01(0.5f + streak * 0.18f);
                    pixels[y * size + x] = new Color32(128, (byte)(g * 255f), 255, 255);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            return tex;
        }
    }
}
