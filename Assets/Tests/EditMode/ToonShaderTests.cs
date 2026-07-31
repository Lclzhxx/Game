// =============================================================
// 文件：ToonShaderTests.cs（EditMode 测试，E1-S2 / ADR-008）
// 作用：Toon 着色器编译烟雾测试 + R5 红线守卫 + 交付物完整性守卫，无头 CI 可跑。
// 覆盖：
//   A. 编译与结构
//     1. Shader 存在且当前平台受支持。
//     2. 编译零错误（ShaderUtil.ShaderHasError，编辑器 API）。
//     3. 三 pass 结构存在，且 LightMode 标签确为 UniversalForward / ShadowCaster / DepthOnly。
//   B. R5 红线（零描边）
//     4. shader 属性名不得含 outline。
//     5. 源码级守卫：.shader 与 .hlsl 文本里不得出现 outline（连注释掉的实现都不许留）。
//     6. 默认材质模板不得携带 outline keyword。
//   C. 变体预算（ADR-008：shader_feature_local ≤ 4；刻意不加 multi_compile_fog）
//     7. 本地 shader_feature 数量 ≤ 4。
//     8. 源码里不得出现 multi_compile_fog（雾归墨韵 Pass，ADR-010）。
//   D. 交付物完整性（子任务 3/4）
//     9. 默认材质模板 ToonGuofeng_Default.mat 存在且指向本 shader。
//    10. 模板参数与 ToonMaterialDefaults（单一事实来源）逐项一致——防三处漂移。
//    11. Toon 验收场景 ToonReview.unity 存在。
// 说明：SRP Batcher 兼容 / 真实编译变体总数 / 截图基线为真机人工验收（S2 计划 §1、S2-R7），
//       无头 -nographics 环境下拿不到 GPU，不在本测试范围。
// =============================================================

using System.IO;
using MJ.Rendering;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public class ToonShaderTests
{
    private const string ShaderName = "Custom/ToonGuofeng";
    private const string ShaderPath = "Assets/Shaders/ToonGuofeng.shader";
    private const string IncludePath = "Assets/Shaders/Include/ToonGuofengLighting.hlsl";
    private const string DefaultMaterialPath = "Assets/Materials/ToonGuofeng_Default.mat";
    private const string ReviewScenePath = "Assets/Tests/Scenes/ToonReview.unity";

    private static Shader Find()
    {
        Shader shader = Shader.Find(ShaderName);
        Assert.IsNotNull(shader, "找不到着色器 " + ShaderName);
        return shader;
    }

    private static string ReadRepoText(string assetPath)
    {
        // Application.dataPath => <repo>/Assets
        string full = Path.Combine(Path.GetDirectoryName(Application.dataPath), assetPath);
        Assert.IsTrue(File.Exists(full), "文件不存在：" + assetPath);
        return File.ReadAllText(full);
    }

    // ---------------- A. 编译与结构 ----------------

    [Test]
    public void Shader_Exists_AndSupported()
    {
        Shader shader = Find();
        Assert.IsTrue(shader.isSupported, "着色器在当前平台不受支持（多为编译失败）");
    }

    [Test]
    public void Shader_CompilesWithoutErrors()
    {
        Shader shader = Find();
        Assert.IsFalse(ShaderUtil.ShaderHasError(shader), "着色器存在编译错误（看 Console/Inspector）");
    }

    [Test]
    public void Shader_HasForwardShadowCasterDepthOnlyPasses()
    {
        Shader shader = Find();
        Assert.GreaterOrEqual(shader.passCount, 3,
            "期望至少 3 个 pass（UniversalForward / ShadowCaster / DepthOnly，墨线深度 Sobel 依赖 DepthOnly）");
    }

    [Test]
    public void Shader_PassesCarryExpectedLightModeTags()
    {
        Shader shader = Find();
        var lightMode = new ShaderTagId("LightMode");
        bool forward = false, shadowCaster = false, depthOnly = false;

        int passCount = shader.GetPassCountInSubshader(0);
        for (int i = 0; i < passCount; i++)
        {
            string tag = shader.FindPassTagValue(0, i, lightMode).name;
            if (tag == "UniversalForward") forward = true;
            else if (tag == "ShadowCaster") shadowCaster = true;
            else if (tag == "DepthOnly") depthOnly = true;
        }

        Assert.IsTrue(forward, "缺 LightMode=UniversalForward 的 pass");
        Assert.IsTrue(shadowCaster, "缺 LightMode=ShadowCaster 的 pass（Toon 物体投不出影）");
        Assert.IsTrue(depthOnly, "缺 LightMode=DepthOnly 的 pass（墨线深度 Sobel 勾不到 Toon 物体）");
    }

    // ---------------- B. R5 红线：零描边 ----------------

    [Test]
    public void Shader_HasNoOutlineProperties_R5RedLine()
    {
        Shader shader = Find();
        int count = shader.GetPropertyCount();
        for (int i = 0; i < count; i++)
        {
            string name = shader.GetPropertyName(i);
            Assert.IsFalse(name.ToLowerInvariant().Contains("outline"),
                "R5 红线违规：材质不得携带描边参数（发现属性 " + name + "）。描边 100% 归墨韵 Ink Pass。");
        }
    }

    [Test]
    public void ShaderSource_ContainsNoOutlineCode_R5RedLine()
    {
        // 源码级守卫：连"注释掉的描边实现"都不许留，否则迟早被人取消注释复活（S2-R4）。
        string shaderSrc = ReadRepoText(ShaderPath).ToLowerInvariant();
        string includeSrc = ReadRepoText(IncludePath).ToLowerInvariant();

        // 文件头/注释里写的是"零描边""禁止 outline"这类红线说明，故只查英文标识符 outline，
        // 并允许出现在明确的红线声明行里（这些行同时含 "r5" 或 "禁"/"不得"）。
        AssertNoLiveOutline(shaderSrc, ShaderPath);
        AssertNoLiveOutline(includeSrc, IncludePath);
    }

    private static void AssertNoLiveOutline(string lowerSrc, string label)
    {
        string[] lines = lowerSrc.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            if (!line.Contains("outline")) continue;
            // 红线说明行豁免：明确标注 r5 / 禁止 / 不得 / 永远不 的注释
            bool isRedLineNote = line.Contains("r5") || line.Contains("禁") || line.Contains("不得") || line.Contains("无");
            Assert.IsTrue(isRedLineNote,
                "R5 红线违规：" + label + " 第 " + (i + 1) + " 行出现描边代码：\n" + line.Trim());
        }
    }

    [Test]
    public void DefaultMaterial_HasNoOutlineKeyword_R5RedLine()
    {
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(DefaultMaterialPath);
        Assert.IsNotNull(mat, "默认 Toon 材质模板缺失：" + DefaultMaterialPath);
        foreach (string kw in mat.shaderKeywords)
        {
            Assert.IsFalse(kw.ToLowerInvariant().Contains("outline"),
                "R5 红线违规：默认材质开了描边 keyword " + kw);
        }
    }

    // ---------------- C. 变体预算 ----------------

    [Test]
    public void Shader_LocalShaderFeatureCount_WithinBudget()
    {
        // ADR-008：shader_feature_local ≤ 4（当前实际 2 个：_RAMPTEX_ON / _BRUSHNORMAL_ON）
        string src = ReadRepoText(ShaderPath);
        int count = 0;
        foreach (string rawLine in src.Split('\n'))
        {
            string line = rawLine.Trim();
            if (line.StartsWith("#pragma shader_feature_local")) count++;
        }
        Assert.LessOrEqual(count, 4,
            "变体预算违规：shader_feature_local 有 " + count + " 个，ADR-008 上限 4 个。");
        Assert.GreaterOrEqual(count, 1, "一个 shader_feature_local 都没有？请确认 shader 未被误改。");
    }

    [Test]
    public void Shader_DoesNotDeclareFogVariants_FogBelongsToInkPass()
    {
        // ADR-008/ADR-010：雾由墨韵全屏 Pass 负责，Toon 不许自己接内建雾（会双重上雾 + 炸变体）
        string src = ReadRepoText(ShaderPath);
        Assert.IsFalse(src.Contains("multi_compile_fog"),
            "ToonGuofeng 不得声明 multi_compile_fog：雾归墨韵全屏 Pass（ADR-010）。");
    }

    // ---------------- D. 交付物完整性 ----------------

    [Test]
    public void DefaultMaterialTemplate_ExistsAndUsesToonShader()
    {
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(DefaultMaterialPath);
        Assert.IsNotNull(mat, "默认 Toon 材质模板缺失：" + DefaultMaterialPath +
                              "（菜单 MJ/Create Toon Material 可重建）");
        Assert.IsNotNull(mat.shader, "默认材质没有 shader");
        Assert.AreEqual(ShaderName, mat.shader.name,
            "默认材质指向了错误的 shader：" + mat.shader.name);
    }

    [Test]
    public void DefaultMaterialTemplate_MatchesSingleSourceOfTruth()
    {
        // 防漂移：模板资产 / ToonMaterialDefaults / 菜单 三者必须同值。
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(DefaultMaterialPath);
        Assert.IsNotNull(mat, "默认 Toon 材质模板缺失：" + DefaultMaterialPath);

        AssertFloat(mat, "_RampThreshold", ToonMaterialDefaults.RampThreshold);
        AssertFloat(mat, "_RampSoftness", ToonMaterialDefaults.RampSoftness);
        AssertFloat(mat, "_RampBands", ToonMaterialDefaults.RampBands);
        AssertFloat(mat, "_RimPower", ToonMaterialDefaults.RimPower);
        AssertFloat(mat, "_RimIntensity", ToonMaterialDefaults.RimIntensity);
        AssertFloat(mat, "_RimLightSideMask", ToonMaterialDefaults.RimLightSideMask);
        AssertFloat(mat, "_BrushStrength", ToonMaterialDefaults.BrushStrength);
        AssertFloat(mat, "_SpecularOn", ToonMaterialDefaults.SpecularOn);
        AssertFloat(mat, "_SpecThreshold", ToonMaterialDefaults.SpecThreshold);
        AssertFloat(mat, "_SpecSoftness", ToonMaterialDefaults.SpecSoftness);
        AssertFloat(mat, "_SpecIntensity", ToonMaterialDefaults.SpecIntensity);

        AssertColor(mat, "_BaseColor", ToonMaterialDefaults.BaseColor);
        AssertColor(mat, "_ShadowTint", ToonMaterialDefaults.ShadowTint);
        AssertColor(mat, "_RimColor", ToonMaterialDefaults.RimColor);
        AssertColor(mat, "_SpecTint", ToonMaterialDefaults.SpecTint);
    }

    private static void AssertFloat(Material mat, string prop, float expected)
    {
        Assert.IsTrue(mat.HasProperty(prop), "材质缺属性 " + prop);
        Assert.AreEqual(expected, mat.GetFloat(prop), 1e-4f,
            "模板参数与 ToonMaterialDefaults 漂移：" + prop);
    }

    private static void AssertColor(Material mat, string prop, Color expected)
    {
        Assert.IsTrue(mat.HasProperty(prop), "材质缺属性 " + prop);
        Color actual = mat.GetColor(prop);
        Assert.AreEqual(expected.r, actual.r, 1e-3f, "模板参数漂移：" + prop + ".r");
        Assert.AreEqual(expected.g, actual.g, 1e-3f, "模板参数漂移：" + prop + ".g");
        Assert.AreEqual(expected.b, actual.b, 1e-3f, "模板参数漂移：" + prop + ".b");
    }

    [Test]
    public void ToonReviewScene_Exists()
    {
        string full = Path.Combine(Path.GetDirectoryName(Application.dataPath), ReviewScenePath);
        Assert.IsTrue(File.Exists(full),
            "Toon 验收场景缺失：" + ReviewScenePath + "（截图基线 toon_baseline.png 的取景源）");
    }

    [Test]
    public void ToonMaterialDefaults_CreatesUsableMaterial()
    {
        Material mat = ToonMaterialDefaults.CreateMaterial();
        Assert.IsNotNull(mat, "ToonMaterialDefaults.CreateMaterial() 返回 null（shader 没找到？）");
        try
        {
            Assert.AreEqual(ShaderName, mat.shader.name);
            Assert.IsFalse(mat.IsKeywordEnabled(ToonMaterialDefaults.BrushNormalKeyword),
                "默认不应开启笔触 keyword（无笔触贴图时应零采样）");
            Assert.IsFalse(mat.IsKeywordEnabled(ToonMaterialDefaults.RampTexKeyword),
                "默认不应开启 Ramp LUT keyword");
        }
        finally
        {
            Object.DestroyImmediate(mat);
        }
    }

    [Test]
    public void ToonMaterialDefaults_BrushVariant_EnablesKeyword()
    {
        Material mat = ToonMaterialDefaults.CreateMaterial(false, true);
        Assert.IsNotNull(mat);
        try
        {
            Assert.IsTrue(mat.IsKeywordEnabled(ToonMaterialDefaults.BrushNormalKeyword),
                "开笔触时必须点亮 " + ToonMaterialDefaults.BrushNormalKeyword);
            Assert.AreEqual(1f, mat.GetFloat("_UseBrushNormal"), 1e-4f,
                "Toggle 属性与 keyword 必须同步（否则 Inspector 显示与实际渲染不一致）");
        }
        finally
        {
            Object.DestroyImmediate(mat);
        }
    }

    [Test]
    public void ProceduralBrushNormal_IsValidAndNeutralCentered()
    {
        Texture2D tex = ToonMaterialDefaults.CreateProceduralBrushNormal(64);
        Assert.IsNotNull(tex);
        try
        {
            Assert.AreEqual(64, tex.width);
            Assert.AreEqual(64, tex.height);
            Color32[] px = tex.GetPixels32();
            // G 通道是 shader 实际采样的通道：必须在 0..1 内且均值接近中性 0.5，
            // 否则笔触扰动会整体推偏明暗交界（相当于偷改了 RampThreshold）。
            double sum = 0;
            foreach (Color32 c in px) sum += c.g;
            double meanG = sum / px.Length / 255.0;
            Assert.That(meanG, Is.EqualTo(0.5).Within(0.08),
                "笔触法线 G 通道均值应接近中性 0.5，实测 " + meanG.ToString("F3") +
                "（偏移会整体推移明暗交界）");
        }
        finally
        {
            Object.DestroyImmediate(tex);
        }
    }
}
