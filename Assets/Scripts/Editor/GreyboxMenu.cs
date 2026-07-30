// =============================================================
// 文件：GreyboxMenu.cs（编辑器脚本，必须放在名为 Editor 的文件夹下）
// 作用：提供 Unity 菜单【Greybox/Rebuild Scene】，一键代码化搭建灰盒场景。
//       真正的构建逻辑在 Scripts/Core/GreyboxBuilder.cs（运行时脚本）里，
//       本文件只负责"菜单入口"，所以放在 Editor 文件夹、引用 UnityEditor 是合规的。
// 使用：Unity 菜单栏 -> Greybox -> Rebuild Scene
// 注意：需在 Unity 2022.3 下编译（使用 UnityEditor API）。
// =============================================================

using UnityEditor;
using UnityEngine;

public class GreyboxMenu
{
    [MenuItem("Greybox/Rebuild Scene")]
    public static void RebuildScene()
    {
        GreyboxBuilder.BuildScene();
    }
}
