// =============================================================
// 文件：ToonShaderTests.cs（EditMode 测试，E1-S2 / ADR-008）
// 作用：Toon 着色器编译烟雾测试 + R5 红线守卫，无头 CI 可跑。
// 覆盖：
//   1. Shader 存在且当前平台受支持。
//   2. 编译零错误（ShaderUtil.ShaderHasError，编辑器 API）。
//   3. R5 红线：材质【零描边参数】——属性名不得含 outline（描边职责 100% 归墨韵 Pass）。
//   4. 三 pass 结构存在（Forward/ShadowCaster/DepthOnly ⇒ passCount ≥ 3）。
// 说明：SRP Batcher 兼容 / 变体总数 ≤64 / 截图基线为真机人工验收（S2 计划 §1），
//       不在本无头测试范围。
// =============================================================

using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class ToonShaderTests
{
    private const string ShaderName = "Custom/ToonGuofeng";

    private static Shader Find()
    {
        Shader shader = Shader.Find(ShaderName);
        Assert.IsNotNull(shader, "找不到着色器 " + ShaderName);
        return shader;
    }

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
    public void Shader_HasForwardShadowCasterDepthOnlyPasses()
    {
        Shader shader = Find();
        Assert.GreaterOrEqual(shader.passCount, 3,
            "期望至少 3 个 pass（UniversalForward / ShadowCaster / DepthOnly，墨线深度 Sobel 依赖 DepthOnly）");
    }
}
