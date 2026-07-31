// =============================================================
// 文件：BaselineCaptureMenu.cs（E1-S2 子任务 4 / E1-S3 子任务 3）
// 作用：截图基线的【生成 + 比对】工具，把「逐像素不变」这条硬验收变成一键可执行的动作，
//       而不是靠人眼对图。
// 菜单：
//   MJ/Test/Build Toon Review Scene              编辑器内直接搭 Toon 验收场景（不进 Play）
//   MJ/Test/Capture Baseline - toon_baseline     采 Toon 基线
//   MJ/Test/Capture Baseline - ink_baseline      采墨韵基线（关雾）
//   MJ/Test/Capture Baseline - ink_fog_baseline  采墨韵基线（开雾）
//   MJ/Test/Compare Active Scene Against Baseline...  当前画面 vs 选定基线，按容差判定
// 容差（S2-R5 缓解措施）：≥99% 像素的逐通道差 < 2/255 判通过；
//   E1-S3 的「关雾逐像素不变」用更严的 100% / 0 差阈（菜单里单列）。
// 环境约束：本工具需要 GPU。无头 -nographics 环境下 SystemInfo.graphicsDeviceType == Null，
//   直接报错退出——基线只在自托管 runner / 制作人本机（固定机器）生成与比对。
// 分辨率：钉死 1920x1080 单档，不做多分辨率矩阵（S2-R1 内存预算）。
// 注意：需在 Unity 2022.3 下编译（使用 UnityEditor API）。
// =============================================================

using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class BaselineCaptureMenu
{
    public const string BaselineFolder = "Assets/Tests/Baseline";
    public const int CaptureWidth = 1920;
    public const int CaptureHeight = 1080;

    /// <summary>默认容差：单通道差 < 2/255 视为相同像素。</summary>
    private const int DefaultChannelTolerance = 2;
    /// <summary>默认通过率：≥99% 像素相同。</summary>
    private const float DefaultPassRatio = 0.99f;

    // ---------------- 场景搭建 ----------------

    [MenuItem("MJ/Test/Build Toon Review Scene", false, 200)]
    public static void BuildToonReviewScene()
    {
        ToonReviewBuilder builder = Object.FindObjectOfType<ToonReviewBuilder>();
        if (builder == null)
        {
            var go = new GameObject("ToonReviewBootstrap");
            builder = go.AddComponent<ToonReviewBuilder>();
            Debug.Log("[BaselineCapture] 当前场景没有 ToonReviewBuilder，已临时创建一个。" +
                      "常规用法是打开 Assets/Tests/Scenes/ToonReview.unity。");
        }
        builder.Build();
    }

    // ---------------- 采基线 ----------------

    [MenuItem("MJ/Test/Capture Baseline - toon_baseline", false, 220)]
    public static void CaptureToon() { Capture("toon_baseline"); }

    [MenuItem("MJ/Test/Capture Baseline - ink_baseline", false, 221)]
    public static void CaptureInk() { Capture("ink_baseline"); }

    [MenuItem("MJ/Test/Capture Baseline - ink_fog_baseline", false, 222)]
    public static void CaptureInkFog() { Capture("ink_fog_baseline"); }

    private static void Capture(string baseName)
    {
        Texture2D shot = RenderMainCamera();
        if (shot == null) return;

        Directory.CreateDirectory(BaselineFolder);
        string path = BaselineFolder + "/" + baseName + ".png";
        File.WriteAllBytes(path, shot.EncodeToPNG());
        Object.DestroyImmediate(shot);

        AssetDatabase.Refresh();
        Debug.Log("[BaselineCapture] 基线已写入：" + path + "（" + CaptureWidth + "x" + CaptureHeight + "）。" +
                  "\n提醒：*.png 走 Git LFS（见 .gitattributes），提交前确认 `git lfs status` 里它是 LFS 对象。");
    }

    // ---------------- 比对 ----------------

    [MenuItem("MJ/Test/Compare Active Scene Against Baseline...", false, 240)]
    public static void CompareAgainstBaseline()
    {
        string path = EditorUtility.OpenFilePanel("选择基线 PNG", BaselineFolder, "png");
        if (string.IsNullOrEmpty(path)) return;

        bool strict = EditorUtility.DisplayDialog(
            "比对模式",
            "选择比对严格度：\n\n" +
            "【逐像素严格】100% 像素零差异 —— E1-S3 硬验收「关雾时既有墨韵基线逐像素不变」用这个。\n\n" +
            "【容差】≥99% 像素逐通道差 < 2/255 —— 常规观感回归用这个（S2-R5 缓解）。",
            "逐像素严格", "容差");

        Compare(path, strict);
    }

    private static void Compare(string baselinePath, bool strict)
    {
        byte[] raw;
        try { raw = File.ReadAllBytes(baselinePath); }
        catch (System.Exception e)
        {
            Debug.LogError("[BaselineCapture] 读基线失败：" + e.Message);
            return;
        }

        var baseline = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!baseline.LoadImage(raw))
        {
            Debug.LogError("[BaselineCapture] 基线不是合法 PNG（是不是 LFS 指针文件没拉下来？先跑 `git lfs pull`）：" + baselinePath);
            Object.DestroyImmediate(baseline);
            return;
        }

        Texture2D shot = RenderMainCamera();
        if (shot == null) { Object.DestroyImmediate(baseline); return; }

        if (baseline.width != shot.width || baseline.height != shot.height)
        {
            Debug.LogError("[BaselineCapture] 尺寸不一致：基线 " + baseline.width + "x" + baseline.height +
                           " vs 当前 " + shot.width + "x" + shot.height + "。基线钉死 1920x1080，请勿改分辨率。");
            Object.DestroyImmediate(baseline);
            Object.DestroyImmediate(shot);
            return;
        }

        Color32[] a = baseline.GetPixels32();
        Color32[] b = shot.GetPixels32();
        int tolerance = strict ? 0 : DefaultChannelTolerance;
        int total = a.Length;
        int differing = 0;
        int maxDiff = 0;

        for (int i = 0; i < total; i++)
        {
            int dr = Mathf.Abs(a[i].r - b[i].r);
            int dg = Mathf.Abs(a[i].g - b[i].g);
            int db = Mathf.Abs(a[i].b - b[i].b);
            int d = Mathf.Max(dr, Mathf.Max(dg, db));
            if (d > maxDiff) maxDiff = d;
            if (d > tolerance) differing++;
        }

        float sameRatio = 1f - (float)differing / total;
        float requiredRatio = strict ? 1f : DefaultPassRatio;
        bool pass = sameRatio >= requiredRatio;

        string msg = "[BaselineCapture] 比对" + (pass ? "通过" : "未通过") +
                     "\n基线：" + baselinePath +
                     "\n模式：" + (strict ? "逐像素严格（0 差异 / 100%）" : "容差（<2/255 / >=99%）") +
                     "\n相同像素率：" + (sameRatio * 100f).ToString("F4") + "%（差异像素 " + differing + " / " + total + "）" +
                     "\n最大单通道差：" + maxDiff + "/255";

        if (pass) Debug.Log(msg);
        else Debug.LogError(msg + "\n处理：先确认是否真的改了渲染代码；若只是驱动/平台噪声，改用容差模式复核。");

        Object.DestroyImmediate(baseline);
        Object.DestroyImmediate(shot);
    }

    // ---------------- 渲染主相机到 Texture2D ----------------

    private static Texture2D RenderMainCamera()
    {
        if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
        {
            Debug.LogError("[BaselineCapture] 当前是无头（-nographics）环境，拿不到 GPU，无法采/比基线。" +
                           "请在制作人本机 Unity Hub 或带 GPU 的自托管 runner 上执行。");
            return null;
        }

        Camera cam = Camera.main;
        if (cam == null)
        {
            Debug.LogError("[BaselineCapture] 场景里没有 MainCamera。先跑一次场景构建器（如 MJ/Test/Build Toon Review Scene）。");
            return null;
        }

        RenderTexture rt = RenderTexture.GetTemporary(CaptureWidth, CaptureHeight, 24, RenderTextureFormat.ARGB32);
        RenderTexture prevTarget = cam.targetTexture;
        RenderTexture prevActive = RenderTexture.active;
        var shot = new Texture2D(CaptureWidth, CaptureHeight, TextureFormat.RGBA32, false);
        try
        {
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            shot.ReadPixels(new Rect(0, 0, CaptureWidth, CaptureHeight), 0, 0);
            shot.Apply(false, false);
        }
        finally
        {
            cam.targetTexture = prevTarget;
            RenderTexture.active = prevActive;
            RenderTexture.ReleaseTemporary(rt);
        }
        return shot;
    }
}
