// =============================================================
// 文件：FpsProbe.cs
// 作用：屏幕左上角实时显示 FPS、Draw Calls、三角形数 + H1/H2 验收提示，用于 H2（1080p60 帧率）核验。
//       （用 OnGUI，零 UI 依赖，灰盒最省事。）
// 挂到：任意常驻物体（GreyboxBuilder 会自动挂到 Main Camera 上）。
// Inspector 设置：无需设置（updateInterval 可在代码内调，默认 0.5s 刷新一次 FPS）。
//
// 单路径兼容说明（核心原则：不依赖任何编辑器版本宏）：
//   本文件刻意【不出现】任何 #if UNITY_6000_0_OR_NEWER 分支。
//   使用 Unity.Profiling.ProfilerRecorder（2020.2+ 引入，2022.3 与 Unity 6 均稳定存在）
//   取 Draw Calls / Triangles：
//     - ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count" / "Triangles Count")
//     - .LastValue（上一帧采样值，long 转 int），并用 .Valid 判空
//   旧版 UnityEngine.Profiling.Recorder 的 .lastValue/.avg 在不同 Unity 版本中不稳定，已弃用，
//   改用新的 ProfilerRecorder（跨版本一致）。
// =============================================================

using UnityEngine;
using UnityEngine.Rendering;
using Unity.Profiling; // ProfilerRecorder, ProfilerCategory

#pragma warning disable CS0618 // 压掉 InkRenderFeature.cs 带过来的 cameraColorTarget 废弃警告（2022.3 下 no-op，安全）

public class FpsProbe : MonoBehaviour
{
    public float updateInterval = 0.5f;
    private float m_Accum = 0f, m_TimeLeft = 0f, m_Fps = 0f;
    private int m_Frames = 0;
    private GUIStyle m_Style;
    private ProfilerRecorder m_DrawCallsRecorder;
    private ProfilerRecorder m_TrianglesRecorder;

    void Start()
    {
        m_TimeLeft = updateInterval;
        EnsureStyle();
    }

    void EnsureStyle()
    {
        if (m_Style == null)
        {
            m_Style = new GUIStyle(GUI.skin.label);
            m_Style.fontSize = 20;
            m_Style.normal.textColor = Color.yellow;
            m_Style.alignment = TextAnchor.UpperLeft;
        }
    }

    void OnEnable()
    {
        m_DrawCallsRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
        m_TrianglesRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
    }

    void OnDisable()
    {
        m_DrawCallsRecorder.Dispose();
        m_TrianglesRecorder.Dispose();
    }

    void Update()
    {
        m_TimeLeft -= Time.deltaTime;
        m_Accum += Time.timeScale / Time.deltaTime;
        m_Frames++;
        if (m_TimeLeft <= 0f) { m_Fps = m_Accum / m_Frames; m_TimeLeft = updateInterval; m_Accum = 0f; m_Frames = 0; }
    }

    void OnGUI()
    {
        EnsureStyle();
        int drawCalls = m_DrawCallsRecorder.Valid ? (int)m_DrawCallsRecorder.LastValue : 0;
        int triangles = m_TrianglesRecorder.Valid ? (int)m_TrianglesRecorder.LastValue : 0;
        string text = string.Format(
            "【灰盒验收探针】\nH1：能分清地面/空中敌人吗？（绿=地面 青=浮空 红=Boss）\nH2：FPS={0:F1}  DrawCalls={1}  Triangles={2}",
            m_Fps, drawCalls, triangles);

        // 半透明深色背景块：保证白底/亮天空盒下也清晰可见
        GUI.color = new Color(0f, 0f, 0f, 0.6f);
        GUI.DrawTexture(new Rect(10, 10, 580, 104), Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(new Rect(20, 18, 560, 88), text, m_Style);
    }
}
