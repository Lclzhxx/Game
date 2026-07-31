// =============================================================
// 文件：HeightFogTests.cs（EditMode 测试，E1-S3 / ADR-010）
// 作用：高度雾并入墨韵全屏 Pass 的守卫测试，无头 CI（-nographics）可跑。
// 覆盖：
//   A. 着色器结构与 keyword 门控
//     1. InkFullscreen 存在、受支持、编译零错误。
//     2. 【C2 红线·着色器侧】SubShader 里只有一个 Pass。
//     3. 声明了 _MJ_HEIGHT_FOG 开关（pragma 层）。
//     4. 6 个雾属性齐全，且默认值与 HeightFogSettings 单一事实来源一致。
//     5. 雾代码全部包在 #if defined(_MJ_HEIGHT_FOG) 内 —— 关雾时不编译，逐像素与 S1 一致。
//     6. 世界坐标重建走 ComputeWorldSpacePosition + UNITY_MATRIX_I_VP，不写版本宏（C5）。
//     7. 雾在勾线之前施加（先晕染、后勾勒）。
//   B. HeightFogSettings 参数防护（沿用 E1-S1 越界保护先例）
//     8. 默认 enabled = false —— 这是「不改既有画面」的前提。
//     9. Guard(float)：NaN / ±Inf 回落默认值；越界 clamp。
//    10. Guard(Color)：脏通道逐通道回落，alpha 恒 1。
//    11. ApplyTo 写入的每个属性都在合法区间内（喂脏数据也不出 NaN）。
//    12. ApplyTo 按 enabled 同步 keyword（开/关往返）。
//    13. ApplyTo(null) 不抛异常。
//   C. C2 红线源码级守卫（零新增 Pass / Blit）
//    14. InkRenderFeature.cs 去注释后：GetTemporaryRT×1、cmd.Blit×2、ReleaseTemporaryRT×1、
//        EnqueuePass×1、ScriptableRenderPass 派生类×1 —— 与 S1 基线序列完全一致。
//    15. HeightFogSettings.cs 里不得出现任何 Pass / RT / Blit 字样（它只是参数容器）。
//   D. 交付物完整性
//    16. Volume Profile 已固化在 Assets/Settings/，且确为低饱和 + 冷调。
//    17. 开雾截图基线条目存在（.png 或真机待采的 .png.pending 标记）。
// 说明：截图逐像素比对需要 GPU，无头环境拿不到（SystemInfo.graphicsDeviceType == Null），
//       故「_MJ_HEIGHT_FOG off 时既有墨韵基线逐像素不变」的硬验收由真机
//       MJ/Test/Compare Active Scene Against Baseline（严格模式）执行，不在本测试范围。
//       本文件用「keyword 门控 + 源码守卫」在 CI 侧把该验收的可证伪前提锁死。
// =============================================================

using System.IO;
using System.Text.RegularExpressions;
using MJ.Rendering;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class HeightFogTests
{
    private const string ShaderName = "Custom/InkFullscreen";
    private const string ShaderPath = "Assets/Shaders/InkFullscreen.shader";
    private const string FeaturePath = "Assets/Scripts/Rendering/InkRenderFeature.cs";
    private const string SettingsScriptPath = "Assets/Scripts/Rendering/HeightFogSettings.cs";
    private const string VolumeProfilePath = "Assets/Settings/InkGuofeng_PostProcess.asset";
    private const string FogBaselineDir = "Assets/Tests/Baseline";
    private const string FogBaselineName = "ink_fog_baseline.png";

    // ---------------- 工具 ----------------

    private static string RepoRoot => Path.GetDirectoryName(Application.dataPath);

    private static string ReadRepoText(string assetPath)
    {
        string full = Path.Combine(RepoRoot, assetPath);
        Assert.IsTrue(File.Exists(full), "文件不存在：" + assetPath);
        return File.ReadAllText(full);
    }

    /// <summary>剥掉 // 行注释与 /* */ 块注释，只留可执行代码——避免注释里的字样把计数守卫带偏。</summary>
    private static string StripComments(string source)
    {
        string noBlock = Regex.Replace(source, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
        return Regex.Replace(noBlock, @"//[^\n]*", string.Empty);
    }

    private static int CountOccurrences(string text, string needle)
    {
        int count = 0, idx = 0;
        while ((idx = text.IndexOf(needle, idx, System.StringComparison.Ordinal)) >= 0)
        {
            count++;
            idx += needle.Length;
        }
        return count;
    }

    private static Shader FindInkShader()
    {
        Shader shader = Shader.Find(ShaderName);
        Assert.IsNotNull(shader, "找不到着色器 " + ShaderName);
        return shader;
    }

    private static Material NewInkMaterial()
    {
        Material mat = new Material(FindInkShader());
        mat.hideFlags = HideFlags.HideAndDontSave;
        return mat;
    }

    // ---------------- A. 着色器结构与 keyword 门控 ----------------

    [Test]
    public void InkShader_Exists_Supported_AndCompilesClean()
    {
        Shader shader = FindInkShader();
        Assert.IsTrue(shader.isSupported, "InkFullscreen 在当前平台不受支持（多为编译失败）");
        Assert.IsFalse(ShaderUtil.ShaderHasError(shader), "InkFullscreen 存在编译错误，高度雾接入破坏了 S1 墨韵栈");
    }

    [Test]
    public void InkShader_HasExactlyOnePass_C2RedLine()
    {
        string src = ReadRepoText(ShaderPath);
        // 只数 Pass 块头（行首缩进 + Pass + 换行/花括号），不数注释里的"Pass"字样
        int passCount = Regex.Matches(StripComments(src), @"^\s*Pass\s*$", RegexOptions.Multiline).Count;
        Assert.AreEqual(1, passCount,
            "C2 红线：墨韵着色器必须保持单 Pass，高度雾只能作为本 Pass 内的阶段，不得另起 Pass。实测 Pass 数=" + passCount);
    }

    [Test]
    public void InkShader_DeclaresHeightFogKeywordSwitch()
    {
        string src = ReadRepoText(ShaderPath);
        Assert.IsTrue(Regex.IsMatch(src, @"#pragma\s+(multi_compile|shader_feature)\w*\s+_\s+" + HeightFogSettings.Keyword),
            "着色器未声明 " + HeightFogSettings.Keyword + " 的 keyword 开关 pragma（关雾零成本的基础）");
    }

    [Test]
    public void InkShader_ExposesAllFogProperties_MatchingSingleSourceOfTruth()
    {
        Shader shader = FindInkShader();
        string[] props =
        {
            HeightFogSettings.PropFogColor,
            HeightFogSettings.PropFogBaseHeight,
            HeightFogSettings.PropFogDensity,
            HeightFogSettings.PropFogHeightFalloff,
            HeightFogSettings.PropFogDistFade,
            HeightFogSettings.PropFogSkyBlend,
        };

        foreach (string p in props)
        {
            Assert.GreaterOrEqual(shader.FindPropertyIndex(p), 0,
                "着色器缺少雾属性 " + p + "（HeightFogSettings 会 SetFloat/SetColor 到一个不存在的槽位，静默失效）");
        }

        // 默认值必须与 C# 侧单一事实来源一致，否则「材质新建时」与「Feature 首帧 ApplyTo 后」画面会跳变
        Material mat = NewInkMaterial();
        try
        {
            Assert.AreEqual(HeightFogSettings.DefaultBaseHeight, mat.GetFloat(HeightFogSettings.PropFogBaseHeight), 1e-4f, "_FogBaseHeight 默认值漂移");
            Assert.AreEqual(HeightFogSettings.DefaultDensity, mat.GetFloat(HeightFogSettings.PropFogDensity), 1e-4f, "_FogDensity 默认值漂移");
            Assert.AreEqual(HeightFogSettings.DefaultHeightFalloff, mat.GetFloat(HeightFogSettings.PropFogHeightFalloff), 1e-4f, "_FogHeightFalloff 默认值漂移");
            Assert.AreEqual(HeightFogSettings.DefaultDistFade, mat.GetFloat(HeightFogSettings.PropFogDistFade), 1e-4f, "_FogDistFade 默认值漂移");
            Assert.AreEqual(HeightFogSettings.DefaultSkyBlend, mat.GetFloat(HeightFogSettings.PropFogSkyBlend), 1e-4f, "_FogSkyBlend 默认值漂移");
        }
        finally
        {
            Object.DestroyImmediate(mat);
        }
    }

    [Test]
    public void InkShader_FogCodeIsFullyGatedByKeyword()
    {
        string src = StripComments(ReadRepoText(ShaderPath));

        // 雾的三个实体：属性读取用的 CBUFFER 声明可以常驻（不占指令），
        // 但函数定义与调用必须在 #if defined(_MJ_HEIGHT_FOG) 内。
        string guard = "#if defined(" + HeightFogSettings.Keyword + ")";
        Assert.GreaterOrEqual(CountOccurrences(src, guard), 2,
            "雾函数定义与 frag 内调用都应各自被 " + guard + " 包裹（关雾时整段不进变体）");

        int defIdx = src.IndexOf("float3 applyHeightFog", System.StringComparison.Ordinal);
        Assert.Greater(defIdx, 0, "找不到 applyHeightFog 定义");
        int guardBeforeDef = src.LastIndexOf(guard, defIdx, System.StringComparison.Ordinal);
        int endifBeforeDef = src.LastIndexOf("#endif", defIdx, System.StringComparison.Ordinal);
        Assert.Greater(guardBeforeDef, endifBeforeDef,
            "applyHeightFog 定义不在 " + HeightFogSettings.Keyword + " 守卫块内——关雾时仍会编译雾代码");
    }

    [Test]
    public void InkShader_WorldPosReconstruction_UsesUrp14SafeApi()
    {
        string src = ReadRepoText(ShaderPath);
        Assert.IsTrue(src.Contains("ComputeWorldSpacePosition"),
            "世界坐标重建应使用 Core RP Common.hlsl 的 ComputeWorldSpacePosition，而不是手搓矩阵");
        Assert.IsTrue(src.Contains("UNITY_MATRIX_I_VP"),
            "应使用 UNITY_MATRIX_I_VP（URP14 Input.hlsl 里即 unity_MatrixInvVP）");
        // C5：不允许出现 Unity 版本条件宏，防止跨版本行为分叉
        Assert.IsFalse(Regex.IsMatch(src, @"#if\s+UNITY_VERSION|UNITY_20\d\d_\d+_OR_NEWER"),
            "C5：着色器内不得出现 Unity 版本宏；引擎版本已被 ADR 钉死");
    }

    [Test]
    public void InkShader_FogAppliedBeforeLineWork()
    {
        string src = StripComments(ReadRepoText(ShaderPath));
        int fogCall = src.IndexOf("applyHeightFog(src", System.StringComparison.Ordinal);
        int sobelCall = src.IndexOf("sobelDepth(uv", System.StringComparison.Ordinal);
        Assert.Greater(fogCall, 0, "frag 里找不到 applyHeightFog 调用");
        Assert.Greater(sobelCall, 0, "frag 里找不到 sobelDepth 调用");
        Assert.Less(fogCall, sobelCall,
            "ADR-010：应先晕染（雾）后勾勒（墨线），墨线要叠在雾之上；当前顺序反了");
    }

    // ---------------- B. HeightFogSettings 参数防护 ----------------

    [Test]
    public void Settings_DefaultsToDisabled_SoS1PixelsAreUntouched()
    {
        HeightFogSettings s = new HeightFogSettings();
        Assert.IsFalse(s.enabled,
            "高度雾默认必须关闭：E1-S3 硬验收要求 keyword off 时既有墨韵截图基线逐像素不变");
        Assert.AreEqual("_MJ_HEIGHT_FOG", HeightFogSettings.Keyword, "keyword 名与 ADR-010 约定不符");
    }

    [TestCase(float.NaN)]
    [TestCase(float.PositiveInfinity)]
    [TestCase(float.NegativeInfinity)]
    public void Guard_Float_RejectsNonFinite(float dirty)
    {
        float result = HeightFogSettings.Guard(dirty, 0f, 1f, 0.25f);
        Assert.AreEqual(0.25f, result, 1e-6f, "非有限值应回落到 fallback，绝不能把 NaN 灌进材质");
    }

    [TestCase(-999f, 0f)]
    [TestCase(999f, 1f)]
    [TestCase(0.5f, 0.5f)]
    public void Guard_Float_ClampsOutOfRange(float input, float expected)
    {
        Assert.AreEqual(expected, HeightFogSettings.Guard(input, 0f, 1f, 0.25f), 1e-6f);
    }

    [Test]
    public void Guard_Color_SanitizesPerChannel_AndForcesOpaqueAlpha()
    {
        Color fallback = HeightFogSettings.DefaultFogColor;
        Color dirty = new Color(float.NaN, 5f, -3f, float.NegativeInfinity);
        Color safe = HeightFogSettings.Guard(dirty, fallback);

        Assert.AreEqual(fallback.r, safe.r, 1e-6f, "NaN 通道应回落 fallback");
        Assert.AreEqual(1f, safe.g, 1e-6f, "超上界通道应 clamp 到 1");
        Assert.AreEqual(0f, safe.b, 1e-6f, "超下界通道应 clamp 到 0");
        Assert.AreEqual(1f, safe.a, 1e-6f, "雾色 alpha 恒为 1");
    }

    [Test]
    public void ApplyTo_WritesOnlySaneValues_EvenWithPoisonedInput()
    {
        Material mat = NewInkMaterial();
        try
        {
            HeightFogSettings s = new HeightFogSettings
            {
                enabled = true,
                fogColor = new Color(float.NaN, 2f, -1f, 0f),
                baseHeight = float.PositiveInfinity,
                density = 9999f,
                heightFalloff = -50f,
                distFade = float.NaN,
                skyBlend = 42f,
            };
            s.ApplyTo(mat);

            AssertFinite(mat.GetFloat(HeightFogSettings.PropFogBaseHeight), "_FogBaseHeight");
            AssertFinite(mat.GetFloat(HeightFogSettings.PropFogDensity), "_FogDensity");
            AssertFinite(mat.GetFloat(HeightFogSettings.PropFogHeightFalloff), "_FogHeightFalloff");
            AssertFinite(mat.GetFloat(HeightFogSettings.PropFogDistFade), "_FogDistFade");
            AssertFinite(mat.GetFloat(HeightFogSettings.PropFogSkyBlend), "_FogSkyBlend");

            Assert.AreEqual(HeightFogSettings.DefaultBaseHeight, mat.GetFloat(HeightFogSettings.PropFogBaseHeight), 1e-4f, "Inf 应回落默认高度");
            Assert.AreEqual(HeightFogSettings.MaxDensity, mat.GetFloat(HeightFogSettings.PropFogDensity), 1e-4f, "超界浓度应 clamp 到上限");
            Assert.AreEqual(HeightFogSettings.MinHeightFalloff, mat.GetFloat(HeightFogSettings.PropFogHeightFalloff), 1e-4f, "负衰减应 clamp 到下限");
            Assert.AreEqual(HeightFogSettings.DefaultDistFade, mat.GetFloat(HeightFogSettings.PropFogDistFade), 1e-4f, "NaN 距离淡入应回落默认");
            Assert.AreEqual(HeightFogSettings.MaxSkyBlend, mat.GetFloat(HeightFogSettings.PropFogSkyBlend), 1e-4f, "超界天空混合应 clamp 到 1");

            Color c = mat.GetColor(HeightFogSettings.PropFogColor);
            AssertFinite(c.r, "_FogColor.r");
            AssertFinite(c.g, "_FogColor.g");
            AssertFinite(c.b, "_FogColor.b");
            Assert.AreEqual(1f, c.a, 1e-4f);
        }
        finally
        {
            Object.DestroyImmediate(mat);
        }
    }

    private static void AssertFinite(float v, string label)
    {
        Assert.IsFalse(float.IsNaN(v), label + " 是 NaN —— 会在 GPU 上污染整屏");
        Assert.IsFalse(float.IsInfinity(v), label + " 是 Inf");
    }

    [Test]
    public void ApplyTo_SyncsKeyword_BothDirections()
    {
        Material mat = NewInkMaterial();
        try
        {
            HeightFogSettings s = new HeightFogSettings();

            s.enabled = false;
            s.ApplyTo(mat);
            Assert.IsFalse(mat.IsKeywordEnabled(HeightFogSettings.Keyword), "关雾时 keyword 不应点亮");

            s.enabled = true;
            s.ApplyTo(mat);
            Assert.IsTrue(mat.IsKeywordEnabled(HeightFogSettings.Keyword), "开雾时 keyword 应点亮");

            // 往返：Inspector 里反复勾选不得残留状态
            s.enabled = false;
            s.ApplyTo(mat);
            Assert.IsFalse(mat.IsKeywordEnabled(HeightFogSettings.Keyword), "关回去时 keyword 必须被清掉（否则关雾画面不回到 S1）");
        }
        finally
        {
            Object.DestroyImmediate(mat);
        }
    }

    [Test]
    public void ApplyTo_NullMaterial_DoesNotThrow()
    {
        Assert.DoesNotThrow(() => new HeightFogSettings().ApplyTo(null),
            "材质未配时 Feature 会传 null，这里必须静默返回而不是炸掉整条管线");
    }

    // ---------------- C. C2 红线源码级守卫 ----------------

    [Test]
    public void InkRenderFeature_BlitSequenceUnchangedFromS1_C2RedLine()
    {
        string code = StripComments(ReadRepoText(FeaturePath));

        Assert.AreEqual(1, CountOccurrences(code, "GetTemporaryRT"),
            "C2：临时 RT 申请次数必须仍为 1（高度雾不得引入新 RT）");
        Assert.AreEqual(1, CountOccurrences(code, "ReleaseTemporaryRT"),
            "C2：临时 RT 释放次数必须仍为 1，且与申请配对");
        Assert.AreEqual(2, CountOccurrences(code, "cmd.Blit("),
            "C2：Blit 次数必须仍为 2（取屏 + 回写），高度雾不得新增 Blit");
        Assert.AreEqual(1, CountOccurrences(code, "EnqueuePass("),
            "C2：只允许入队 1 条 Pass");
        Assert.AreEqual(1, Regex.Matches(code, @":\s*ScriptableRenderPass\b").Count,
            "C2：只允许存在 1 个 ScriptableRenderPass 派生类");
        Assert.IsTrue(code.Contains("heightFog.ApplyTo(mat)"),
            "高度雾未接线：Execute 里应调用 heightFog.ApplyTo(mat) 同步参数与 keyword");
    }

    [Test]
    public void HeightFogSettings_IsPureDataMapper_NoPipelineObjects()
    {
        string code = StripComments(ReadRepoText(SettingsScriptPath));
        string[] forbidden = { "Blit", "GetTemporaryRT", "ReleaseTemporaryRT", "EnqueuePass", "ScriptableRenderPass", "CommandBufferPool" };
        foreach (string token in forbidden)
        {
            Assert.IsFalse(code.Contains(token),
                "HeightFogSettings 只应是参数容器 + 材质映射，出现了管线对象：" + token);
        }
        Assert.IsTrue(code.Contains("CoreUtils.SetKeyword"),
            "keyword 同步应统一走 CoreUtils.SetKeyword（与 URP 内部行为一致）");
    }

    // ---------------- D. 交付物完整性 ----------------

    [Test]
    public void VolumeProfile_IsCommitted_AndIsLowSaturationCoolTone()
    {
        string yaml = ReadRepoText(VolumeProfilePath);

        // 三个 override 的脚本 GUID（URP 14.0.12）
        Assert.IsTrue(yaml.Contains("66f335fb1ffd8684294ad653bf1c7564"), "Volume Profile 缺 ColorAdjustments");
        Assert.IsTrue(yaml.Contains("221518ef91623a7438a71fef23660601"), "Volume Profile 缺 WhiteBalance");
        Assert.IsTrue(yaml.Contains("97c23e3b12dc18c42a140437e53d3951"), "Volume Profile 缺 Tonemapping");

        float saturation = ReadOverrideValue(yaml, "saturation");
        float temperature = ReadOverrideValue(yaml, "temperature");
        Assert.Less(saturation, 0f, "水墨国风要求低饱和：saturation 必须为负，实测 " + saturation);
        Assert.Less(temperature, 0f, "水墨国风要求冷调：白平衡色温必须为负（偏冷），实测 " + temperature);

        Assert.IsTrue(File.Exists(Path.Combine(RepoRoot, VolumeProfilePath + ".meta")),
            "Volume Profile 缺 .meta —— 换机器后 GUID 会重生成，引用全断");
    }

    private static float ReadOverrideValue(string yaml, string field)
    {
        Match m = Regex.Match(yaml, @"^\s*" + field + @":\s*\r?\n\s*m_OverrideState:\s*1\s*\r?\n\s*m_Value:\s*(-?[\d.]+)",
            RegexOptions.Multiline);
        Assert.IsTrue(m.Success, "Volume Profile 里找不到已勾选 override 的字段：" + field);
        return float.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
    }

    [Test]
    public void FogBaseline_EntryExists_RealOrPending()
    {
        string dir = Path.Combine(RepoRoot, FogBaselineDir);
        Assert.IsTrue(Directory.Exists(dir), "基线目录不存在：" + FogBaselineDir);

        string real = Path.Combine(dir, FogBaselineName);
        string pending = real + ".pending";
        Assert.IsTrue(File.Exists(real) || File.Exists(pending),
            "开雾截图基线条目缺失：需要 " + FogBaselineName + "（真机采集）或其 .pending 占位标记");

        if (File.Exists(real))
        {
            Assert.Greater(new FileInfo(real).Length, 1024,
                "基线 PNG 过小，疑似 LFS 指针未拉取或占位假图——比对会假阳性");
        }
    }
}
