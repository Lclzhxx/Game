// =============================================================
// 文件：ToonMaterialCreator.cs（E1-S2 子任务 3，ADR-008）
// 作用：一键生成国风 Toon 材质，省去手建 Material + 选 Shader。
// 菜单：
//   MJ/Create Toon Material              生成/选中 Assets/Materials/ToonGuofeng_Default.mat（默认模板）
//   MJ/Create Toon Material (New Variant) 以模板参数为基，新建一份带序号的材质供美术试参
// 默认模板参数哲学（ADR-008）：骨架不钉观感——这里给的是"能看清结构"的中性起始值，
//   art-director S2 末对齐后再改模板；改模板 = 改 ToonGuofeng_Default.mat 一处。
// 红线（R5）：生成后立刻做【零描边参数】断言——一旦 shader 里混进 outline 属性，
//   菜单直接报 LogError（与 EditMode 的 ToonShaderTests 双保险）。
// 注意：需在 Unity 2022.3 下编译（使用 UnityEditor API）。
// =============================================================

using MJ.Rendering;
using UnityEditor;
using UnityEngine;

public static class ToonMaterialCreator
{
    public const string ShaderName = ToonMaterialDefaults.ShaderName;
    public const string MaterialFolder = "Assets/Materials";
    public const string DefaultMaterialPath = "Assets/Materials/ToonGuofeng_Default.mat";

    [MenuItem("MJ/Create Toon Material", false, 100)]
    public static void CreateDefaultToonMaterial()
    {
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(DefaultMaterialPath);
        if (existing != null)
        {
            Debug.Log("[ToonMaterialCreator] 默认 Toon 材质已存在，直接选中：" + DefaultMaterialPath +
                      "（要试参请用 MJ/Create Toon Material (New Variant)，别改模板）。");
            Selection.activeObject = existing;
            AssertNoOutline(existing);
            return;
        }

        Material mat = CreateAt(DefaultMaterialPath);
        if (mat == null) return;
        Debug.Log("[ToonMaterialCreator] 已生成默认 Toon 材质：" + DefaultMaterialPath +
                  " —— 指到角色/场景 Renderer 上即可；描边不在这里，勾线由墨韵 Pass 出（R5）。");
    }

    [MenuItem("MJ/Create Toon Material (New Variant)", false, 101)]
    public static void CreateVariantToonMaterial()
    {
        EnsureFolder();
        string path = AssetDatabase.GenerateUniqueAssetPath(MaterialFolder + "/ToonGuofeng_Variant.mat");
        Material mat = CreateAt(path);
        if (mat == null) return;
        Debug.Log("[ToonMaterialCreator] 已生成 Toon 试参材质：" + path);
    }

    // ---------------- 内部 ----------------

    private static Material CreateAt(string path)
    {
        Shader shader = Shader.Find(ShaderName);
        if (shader == null)
        {
            Debug.LogError("[ToonMaterialCreator] 找不到 Shader '" + ShaderName +
                           "'，请确认 Assets/Shaders/ToonGuofeng.shader 已导入且编译通过。");
            return null;
        }

        EnsureFolder();
        Material mat = new Material(shader);
        ApplyDefaultParams(mat);
        AssetDatabase.CreateAsset(mat, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeObject = mat;
        AssertNoOutline(mat);
        return mat;
    }

    private static void EnsureFolder()
    {
        if (!AssetDatabase.IsValidFolder(MaterialFolder))
            AssetDatabase.CreateFolder("Assets", "Materials");
    }

    /// <summary>
    /// 默认模板参数——【单一事实来源在 MJ.Rendering.ToonMaterialDefaults】，
    /// 这里只做转调，保证「菜单 / 验收场景 / 模板资产」三者永不漂移。
    /// </summary>
    public static void ApplyDefaultParams(Material mat)
    {
        ToonMaterialDefaults.Apply(mat);
    }

    /// <summary>R5 红线：材质不得携带任何描边参数（描边 100% 归墨韵 Ink Pass）。</summary>
    private static void AssertNoOutline(Material mat)
    {
        if (mat == null || mat.shader == null) return;
        int count = mat.shader.GetPropertyCount();
        for (int i = 0; i < count; i++)
        {
            string name = mat.shader.GetPropertyName(i);
            if (name.ToLowerInvariant().Contains("outline"))
            {
                Debug.LogError("[ToonMaterialCreator] R5 红线违规：Toon shader 出现描边属性 '" + name +
                               "'。描边必须 100% 由墨韵 Ink Pass 负责，请移除该属性。");
                return;
            }
        }
    }
}
