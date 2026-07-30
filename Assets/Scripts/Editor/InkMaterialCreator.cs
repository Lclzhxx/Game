// =============================================================
// 文件：InkMaterialCreator.cs（编辑器脚本，必须放在名为 Editor 的文件夹下）
// 作用：一键生成墨韵材质，省去用户手动建 Material、选 Shader 的步骤。
// 使用：Unity 菜单 -> Greybox/Create Ink Material
//       生成位置：Assets/Materials/InkMaterial.mat（与 Shaders/ 同级）
// 挂到：无需挂物体，纯编辑器菜单。
// 注意：需在 Unity 2022.3 下编译（使用 UnityEditor API）。
// =============================================================

using UnityEngine;
using UnityEditor;
using System.IO;

public class InkMaterialCreator
{
    private const string Folder = "Assets/Materials";
    private const string Path = "Assets/Materials/InkMaterial.mat";

    [MenuItem("Greybox/Create Ink Material")]
    public static void CreateInkMaterial()
    {
        // 已存在则跳过
        if (File.Exists(Application.dataPath + "/Materials/InkMaterial.mat"))
        {
            Debug.Log("[InkMaterialCreator] 材质已存在，跳过：" + Path +
                      "（请将其拖到 InkRenderFeature 的 inkMaterial 字段）。");
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<Material>(Path);
            return;
        }

        if (!AssetDatabase.IsValidFolder(Folder))
            AssetDatabase.CreateFolder("Assets", "Materials");

        Shader shader = Shader.Find("Custom/InkFullscreen");
        if (shader == null)
        {
            Debug.LogError("[InkMaterialCreator] 找不到 Shader 'Custom/InkFullscreen'，" +
                           "请确认 InkFullscreen.shader 已放入工程并已完成编译。");
            return;
        }

        Material mat = new Material(shader);
        AssetDatabase.CreateAsset(mat, Path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject = mat;
        Debug.Log("[InkMaterialCreator] 已生成墨韵材质：" + Path +
                  " —— 请将其拖到 InkRenderFeature 的 inkMaterial 字段，并在 URP Renderer 资产里 Add Renderer Feature 选 InkRenderFeature。");
    }
}
