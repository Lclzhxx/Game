// =============================================================
// 文件：Telemetry.cs（E0-S6，S2 顺延）
// 作用：统一性能遥测服务——采集 FPS / 帧时 / DrawCall / SetPass / 墨韵耗时，
//       供 BatchProbe(E1-S5)、InkBudgetGate(E1-S6) 复用出口。
//       演进自 FpsProbe（Core/FpsProbe.cs）：复用其 ProfilerRecorder 取数思路，
//       但抽成「采集核心纯逻辑 + MonoBehaviour 外壳」两层，纯 C#、零渲染依赖、
//       可无头跑（EditMode）的统一服务。
//
// 分层：
//   1) FrameStatsAggregator：纯逻辑，环形缓冲 + 滑动平均 + 单位换算(ms<->fps)。
//      零 Unity 渲染依赖，可在 EditMode 直接单测（满足「零渲染依赖 / 无头可跑」）。
//   2) Telemetry (MonoBehaviour)：用 ProfilerRecorder 取 DrawCall/SetPass、
//      用 Time.deltaTime 推帧时、接收墨韵耗时上报，喂给聚合器；可导出 JSON。
//
// 墨韵耗时接入：本批次不改动 InkRenderFeature（属 E1-S4/E1-S6）。
//      预留 public ReportInkPassMs(ms) 供墨韵栈每帧上报（E1-S6 接实），
//      届时在 InkRenderPass.Execute 末尾调用 Telemetry 实例的 ReportInkPassMs(...)
//      即可，无需本批次再动墨韵代码。
//
// 与 FpsProbe 的关系：本文件是 FpsProbe 的「演进」产物（新统一服务）。
//      S2 收口的 FpsProbe.OnGUI HUD 本批次【不改动】，退役留 E13-S1。
//      Telemetry 不依赖 FpsProbe，二者可并存（各自独立 ProfilerRecorder，互不干扰）。
//
// 红线：纯 C# 零渲染依赖；不破坏 FpsProbe 既有行为（S2 收口不回退）。
// 注意：需在 Unity 2022.3 下编译（ProfilerRecorder 2020.2+，跨版本一致）。
// =============================================================

using System;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Profiling;

namespace MJ.Services
{
    /// <summary>单帧结构化遥测快照（瞬时值 + 滑动平均）。</summary>
    [Serializable]
    public struct FrameStats
    {
        public float fps;            // 瞬时 FPS（由帧时换算）
        public float frameTimeMs;    // 瞬时帧时（毫秒）
        public int drawCalls;        // 瞬时 DrawCall 数
        public int setPassCalls;     // 瞬时 SetPass 数
        public float inkPassMs;      // 瞬时墨韵耗时（毫秒）

        public float avgFps;         // 滑动平均 FPS
        public float avgFrameTimeMs; // 滑动平均帧时
        public int avgDrawCalls;     // 滑动平均 DrawCall
        public int avgSetPassCalls;  // 滑动平均 SetPass
        public float avgInkPassMs;   // 滑动平均墨韵耗时
    }

    /// <summary>
    /// 纯逻辑聚合器：环形缓冲 + 滑动平均。零 Unity 渲染依赖，可在 EditMode 直接单测。
    /// 单位换算（ms&lt;-&gt;fps）以静态方法提供，独立可测。
    /// 热路径零分配：Record / GetAverages 均为 O(1)，不分配托管堆。
    /// </summary>
    public class FrameStatsAggregator
    {
        private readonly float[] m_FrameTime;
        private readonly int[] m_DrawCalls;
        private readonly int[] m_SetPass;
        private readonly float[] m_InkPass;
        private int m_Head;       // 下一个写入位置（满时指向最旧样本）
        private int m_Count;      // 已写入样本数（<= capacity）

        private float m_SumFrameTime;
        private float m_SumInkPass;
        private long m_SumDrawCalls;
        private long m_SumSetPass;

        public int Capacity { get; }

        public FrameStatsAggregator(int capacity = 64)
        {
            if (capacity <= 0) capacity = 1;
            Capacity = capacity;
            m_FrameTime = new float[capacity];
            m_DrawCalls = new int[capacity];
            m_SetPass = new int[capacity];
            m_InkPass = new float[capacity];
            m_Head = 0;
            m_Count = 0;
            m_SumFrameTime = 0f;
            m_SumInkPass = 0f;
            m_SumDrawCalls = 0L;
            m_SumSetPass = 0L;
        }

        /// <summary>推入一帧采样。超过 capacity 后覆盖最旧样本（滑动窗口）。O(1)。</summary>
        public void Record(float frameTimeMs, int drawCalls, int setPass, float inkPassMs)
        {
            if (m_Count == Capacity)
            {
                // 覆盖最旧样本前，先从运行总和里扣掉它
                int oldest = m_Head;
                m_SumFrameTime -= m_FrameTime[oldest];
                m_SumInkPass -= m_InkPass[oldest];
                m_SumDrawCalls -= m_DrawCalls[oldest];
                m_SumSetPass -= m_SetPass[oldest];
            }

            m_FrameTime[m_Head] = frameTimeMs;
            m_DrawCalls[m_Head] = drawCalls;
            m_SetPass[m_Head] = setPass;
            m_InkPass[m_Head] = inkPassMs;

            m_SumFrameTime += frameTimeMs;
            m_SumInkPass += inkPassMs;
            m_SumDrawCalls += drawCalls;
            m_SumSetPass += setPass;

            m_Head = (m_Head + 1) % Capacity;
            if (m_Count < Capacity) m_Count++;
        }

        public int SampleCount => m_Count;
        public bool HasData => m_Count > 0;

        /// <summary>回填滑动平均。无数据时全部清零。</summary>
        public void GetAverages(out float avgFrameTimeMs, out float avgFps,
            out int avgDrawCalls, out int avgSetPass, out float avgInkPassMs)
        {
            if (m_Count == 0)
            {
                avgFrameTimeMs = 0f; avgFps = 0f;
                avgDrawCalls = 0; avgSetPass = 0; avgInkPassMs = 0f;
                return;
            }
            float inv = 1f / m_Count;
            avgFrameTimeMs = m_SumFrameTime * inv;
            avgDrawCalls = (int)System.Math.Round(m_SumDrawCalls * inv);
            avgSetPass = (int)System.Math.Round(m_SumSetPass * inv);
            avgInkPassMs = m_SumInkPass * inv;
            avgFps = MsToFps(avgFrameTimeMs);
        }

        /// <summary>毫秒 -> FPS（帧时过小/非法时返回 0，避免除零 / NaN）。</summary>
        public static float MsToFps(float ms)
        {
            if (ms <= 1e-6f) return 0f;
            float fps = 1000f / ms;
            return float.IsInfinity(fps) || float.IsNaN(fps) ? 0f : fps;
        }

        /// <summary>FPS -> 毫秒（FPS 过小/非法时返回 0）。</summary>
        public static float FpsToMs(float fps)
        {
            if (fps <= 1e-6f) return 0f;
            float ms = 1000f / fps;
            return float.IsInfinity(ms) || float.IsNaN(ms) ? 0f : ms;
        }
    }

    /// <summary>
    /// 统一遥测服务（MonoBehaviour）。挂到常驻物体（如主相机，GreyboxBuilder 后续可自动挂）。
    /// 采集 FPS / 帧时 / DrawCall / SetPass / 墨韵耗时；提供结构化 GetFrameStats() 与 JSON 导出。
    /// </summary>
    [AddComponentMenu("MJ/Services/Telemetry")]
    public class Telemetry : MonoBehaviour
    {
        [Header("聚合窗口（滑动平均样本数）")]
        public int windowSize = 64;

        [Header("每 N 秒导出一次 JSON 到 persistentDataPath（0 = 不上报）")]
        public float exportIntervalSec = 0f;

        private FrameStatsAggregator m_Agg;
        private ProfilerRecorder m_DrawCallsRecorder;
        private ProfilerRecorder m_SetPassRecorder;
        private float m_InkPassMs;     // 由墨韵栈 ReportInkPassMs 上报
        private float m_ExportTimer;

        /// <summary>当前聚合器（供 BatchProbe / InkBudgetGate 直接读平均）。</summary>
        public FrameStatsAggregator Aggregator => m_Agg;

        void OnEnable()
        {
            if (m_Agg == null) m_Agg = new FrameStatsAggregator(windowSize > 0 ? windowSize : 64);
            m_DrawCallsRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
            m_SetPassRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls");
        }

        void OnDisable()
        {
            if (m_DrawCallsRecorder.Valid) m_DrawCallsRecorder.Dispose();
            if (m_SetPassRecorder.Valid) m_SetPassRecorder.Dispose();
        }

        /// <summary>墨韵栈每帧上报耗时（毫秒）。E1-S6 接实；本批次预留接入点。负值夹 0。</summary>
        public void ReportInkPassMs(float ms)
        {
            m_InkPassMs = ms < 0f ? 0f : ms;
        }

        void Update()
        {
            float frameTimeMs = Time.deltaTime * 1000f;
            int drawCalls = m_DrawCallsRecorder.Valid ? (int)m_DrawCallsRecorder.LastValue : 0;
            int setPass = m_SetPassRecorder.Valid ? (int)m_SetPassRecorder.LastValue : 0;
            m_Agg.Record(frameTimeMs, drawCalls, setPass, m_InkPassMs);

            if (exportIntervalSec > 0f)
            {
                m_ExportTimer += Time.deltaTime;
                if (m_ExportTimer >= exportIntervalSec)
                {
                    m_ExportTimer = 0f;
                    ExportJson();
                }
            }
        }

        /// <summary>当前结构化快照（瞬时 + 滑动平均）。供调试面板 / CI 冒烟解析。</summary>
        public FrameStats GetFrameStats()
        {
            FrameStats s = new FrameStats();
            float frameTimeMs = Time.deltaTime * 1000f;
            s.frameTimeMs = frameTimeMs;
            s.fps = FrameStatsAggregator.MsToFps(frameTimeMs);
            s.drawCalls = m_DrawCallsRecorder.Valid ? (int)m_DrawCallsRecorder.LastValue : 0;
            s.setPassCalls = m_SetPassRecorder.Valid ? (int)m_SetPassRecorder.LastValue : 0;
            s.inkPassMs = m_InkPassMs;
            m_Agg.GetAverages(out s.avgFrameTimeMs, out s.avgFps, out s.avgDrawCalls, out s.avgSetPassCalls, out s.avgInkPassMs);
            return s;
        }

        /// <summary>导出当前快照为 JSON 字符串（键名稳定，供 CI 解析）。</summary>
        public string ToJson()
        {
            return JsonUtility.ToJson(GetFrameStats(), true);
        }

        /// <summary>把当前快照写入 persistentDataPath/telemetry.json（CI 帧率冒烟读取点）。</summary>
        public void ExportJson()
        {
            try
            {
                string path = Path.Combine(Application.persistentDataPath, "telemetry.json");
                File.WriteAllText(path, ToJson());
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Telemetry] JSON 导出失败：" + e.Message);
            }
        }
    }
}
